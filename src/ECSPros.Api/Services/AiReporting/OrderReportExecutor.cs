using System.Data;
using ECSPros.Iam.Application.Services;
using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Grid;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECSPros.Api.Services.AiReporting;

public sealed record ReportCurrencyTotal(string CurrencyCode, long Count, decimal Amount);

public sealed class OrderReportExecutor(NpgsqlDataSource dataSource, IEtkinYetkiServisi authorization)
{
    public async Task ValidateDynamicPlanAsync(string json, ECSPros.Shared.Kernel.Authorization.EfektifYetkiler effective, CancellationToken ct)
    {
        var plan = DynamicReportPlan.Parse(json);
        if (plan.Source == "stock") await new DynamicStockExecutor(dataSource, authorization).ValidateAsync(plan, effective, ct);
        else ValidateDynamicPlan(json, effective);
    }
    private static readonly SemaphoreSlim Slots = ReportExecutionBudget.Slots;
    public static readonly GridSchema<OrderReportSummaryRow> SummarySchema = new GridSchema<OrderReportSummaryRow>()
        .Text("orders.currencyCode", r => r.CurrencyCode).Text("orders.status", r => r.Status)
        .Text("orders.paymentStatus", r => r.PaymentStatus).Text("orders.orderType", r => r.OrderType)
        .Guid("orders.firmPlatformId", r => r.FirmPlatformId).Kanal(r => r.FirmPlatformId)
        .Number("orders.count", r => r.OrderCount).Number("orders.amount", r => r.OrderAmount)
        .Sort("orders.currencyCode", r => r.CurrencyCode).Sort("orders.status", r => r.Status)
        .Sort("orders.paymentStatus", r => r.PaymentStatus).Sort("orders.orderType", r => r.OrderType)
        .Sort("orders.firmPlatformId", r => r.FirmPlatformId).Sort("orders.count", r => r.OrderCount)
        .Sort("orders.amount", r => r.OrderAmount).DefaultSort(r => r.CurrencyCode, false);

    public async Task<StockReportResult> ExecuteAsync(Guid userId, string json, ReportGridState grid, CancellationToken ct)
    {
        if (userId == Guid.Empty) throw new UnauthorizedAccessException();
        var effective = await authorization.GetirAsync(userId, ct);
        var permissions = StockReportExecutor.ResolvePermissions(effective);
        var validation = ReportDefinitionValidator.Parse(json, permissions);
        if (!validation.IsValid) throw new ArgumentException(validation.Error);
        var definition = validation.Definition!;
        var plan = OrderReportRecipe.Parse(definition, permissions);
        var columns = definition.Dimensions!.Concat(definition.Metrics!).ToArray();
        var request = ValidateGrid(definition, grid, plan.Details);
        if (!await Slots.WaitAsync(0, ct)) throw new InvalidOperationException("Rapor sistemi meşgul.");
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            ct = deadline.Token;
            await using var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>().UseNpgsql(dataSource).Options);
            db.Database.SetCommandTimeout(15);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY; SET LOCAL statement_timeout = '15s'", ct);
            var basis = OrderReportSource.Create(db.Orders, effective, plan.Scope, plan.BaseFilters, db);
            List<object?[]> rows;
            List<ReportCurrencyTotal> totals;
            long count;
            if (plan.Details)
            {
                var filtered = OrderReportSource.Create(basis.Query, effective, plan.Scope, request, db, forExport: grid.ExportMode);
                totals = await filtered.Summary().Select(r => new ReportCurrencyTotal(r.CurrencyCode, r.OrderCount, r.OrderAmount)).ToListAsync(ct);
                count = totals.Sum(r => r.Count);
                if (grid.ExportMode) ReportExcelExport.CheckCount(count);
                var page = await filtered.Details().ToListAsync(ct);
                rows = page.Select(r => columns.Select(c => DetailValue(r, c)).ToArray()).ToList();
            }
            else
            {
                var summary = SummarySchema.ApplyFilters(basis.Summary(definition.Dimensions!.Where(d => d != "orders.currencyCode").Select(d => d[7..]).ToArray()), request);
                if (!string.IsNullOrWhiteSpace(grid.Search))
                {
                    var term = grid.Search.Trim().ToLowerInvariant();
                    summary = summary.Where(r => r.CurrencyCode.ToLower().Contains(term)
                        || (r.Status != null && r.Status.ToLower().Contains(term))
                        || (r.PaymentStatus != null && r.PaymentStatus.ToLower().Contains(term))
                        || (r.OrderType != null && r.OrderType.ToLower().Contains(term)));
                }
                count = await summary.LongCountAsync(ct);
                if (grid.ExportMode) ReportExcelExport.CheckCount(count);
                totals = await summary.GroupBy(r => r.CurrencyCode).Select(g => new ReportCurrencyTotal(g.Key, g.Sum(r => r.OrderCount), g.Sum(r => r.OrderAmount))).ToListAsync(ct);
                var sorted = (IOrderedQueryable<OrderReportSummaryRow>)SummarySchema.ApplySort(summary, request);
                var page = await sorted.ThenBy(r => r.CurrencyCode).ThenBy(r => r.Status).ThenBy(r => r.PaymentStatus)
                    .ThenBy(r => r.FirmPlatformId).ThenBy(r => r.OrderType)
                    .Skip((grid.Page - 1) * grid.PageSize).Take(grid.PageSize).ToListAsync(ct);
                rows = page.Select(r => columns.Select(c => SummaryValue(r, c)).ToArray()).ToList();
            }
            await tx.CommitAsync(ct);
            // Never mix monetary currencies in a scalar total.
            return new(columns, rows, DateTimeOffset.UtcNow, count, new Dictionary<string, decimal>(), totals);
        }
        finally { Slots.Release(); }
    }

    public void ValidateDynamicPlan(string json, ECSPros.Shared.Kernel.Authorization.EfektifYetkiler effective)
    {
        var plan = DynamicReportPlan.Parse(json);
        if (plan.Source == MovementReportSource.Id) { new MovementReportExecutor(dataSource, authorization).Validate(plan, effective); return; }
        if (plan.Source == ReturnReportSource.Id) { new ReturnReportExecutor(dataSource, authorization).Validate(plan, effective); return; }
        if (plan.Source == CustomerReportSource.Id) { new CustomerReportExecutor(dataSource, authorization).Validate(plan, effective); return; }
        if (plan.Source == StaffActivitySource.Id) { new StaffActivityExecutor(dataSource, authorization).Validate(plan, effective); return; }
        if (plan.Source == ProductCardReportSource.Id) { new ProductCardReportExecutor(dataSource, authorization).Validate(plan, effective); return; }
        using var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>().UseNpgsql(dataSource).Options);
        var source = OrderReportSource.Create(db.Orders, effective, plan.Scope(), db: db, predicate: plan.Predicate);
        if (plan.Detail is not null) _ = source.DynamicDetails(plan.Detail, new(), effective);
        else _ = source.DynamicSummary(plan.Aggregate!, effective); // Build expressions only; never connect or execute.
    }

    public async Task<StockReportResult> ExecuteDynamicAsync(Guid userId, string json, ReportGridState grid, CancellationToken ct)
    {
        if (userId == Guid.Empty) throw new UnauthorizedAccessException();
        var effective = await authorization.GetirAsync(userId, ct);
        var plan = DynamicReportPlan.Parse(json);
        if (plan.Source == MovementReportSource.Id) return await new MovementReportExecutor(dataSource, authorization).ExecuteAsync(userId, plan, grid, ct);
        if (plan.Source == ReturnReportSource.Id) return await new ReturnReportExecutor(dataSource, authorization).ExecuteAsync(userId, plan, grid, ct);
        if (plan.Source == CustomerReportSource.Id) return await new CustomerReportExecutor(dataSource, authorization).ExecuteAsync(userId, plan, grid, ct);
        if (plan.Source == StaffActivitySource.Id) return await new StaffActivityExecutor(dataSource, authorization).ExecuteAsync(userId, plan, grid, ct);
        if (plan.Source == ProductCardReportSource.Id) return await new ProductCardReportExecutor(dataSource, authorization).ExecuteAsync(userId, plan, grid, ct);
        if (plan.Source == "stock") return await new DynamicStockExecutor(dataSource, authorization).ExecuteAsync(userId, plan, grid, ct);
        await using var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>().UseNpgsql(dataSource).Options);
        // Validate structure before reserving a slot; translate under the shared concurrency limit.
        var period = plan.Scope();
        var source = OrderReportSource.Create(db.Orders, effective, period, db: db, predicate: plan.Predicate);
        if (plan.Detail is not null)
            return (await ExecuteDynamicDetailsAsync(db, source.DynamicDetails(plan.Detail, grid, effective), ct, grid.ExportMode)) with { ResolvedPeriod = period };
        var aggregate = source.DynamicSummary(plan.Aggregate!, effective);
        var filtered = ReportAggregateGrid.Apply(aggregate, plan.Aggregate!, grid);
        var paged = filtered.Skip(checked((grid.Page - 1) * grid.PageSize)).Take(grid.PageSize);
        if (!await Slots.WaitAsync(0, ct)) throw new InvalidOperationException("Rapor sistemi meşgul.");
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            ct = deadline.Token;
            _ = paged.ToQueryString();
            ct.ThrowIfCancellationRequested();
            db.Database.SetCommandTimeout(15);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY; SET LOCAL statement_timeout = '15s'", ct);
            var count = await filtered.LongCountAsync(ct);
            if (grid.ExportMode) ReportExcelExport.CheckCount(count);
            var page = await paged.ToListAsync(ct);
            await tx.CommitAsync(ct);
            // No fabricated totals: average/min/max cannot be summed across groups or currencies.
            return new(aggregate.Columns, page.Select(r => ReportAggregateGrid.Values(r, plan.Aggregate!.Dimensions!.Length,
                plan.Aggregate.Measures!.Length)).ToList(), DateTimeOffset.UtcNow, count, new Dictionary<string, decimal>()) { ResolvedPeriod = period };
        }
        finally { Slots.Release(); }
    }

    private static async Task<StockReportResult> ExecuteDynamicDetailsAsync(OrderDbContext db,
        ReportDetailQuery<ECSPros.Order.Domain.Entities.Order> query, CancellationToken ct, bool forExport = false)
    {
        if (!await Slots.WaitAsync(0, ct)) throw new InvalidOperationException("Rapor sistemi meşgul.");
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            ct = deadline.Token;
            _ = query.Page.ToQueryString();
            ct.ThrowIfCancellationRequested();
            db.Database.SetCommandTimeout(15);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY; SET LOCAL statement_timeout = '15s'", ct);
            var count = await query.Filtered.LongCountAsync(ct);
            if (forExport) ReportExcelExport.CheckCount(count);
            var rows = await query.Page.ToListAsync(ct);
            await tx.CommitAsync(ct);
            return new(query.Columns, rows, DateTimeOffset.UtcNow, count, new Dictionary<string, decimal>());
        }
        finally { Slots.Release(); }
    }

    public static GridRequest ValidateGrid(ReportDefinition definition, ReportGridState grid, bool details)
    {
        var columns = definition.Dimensions!.Concat(definition.Metrics!).ToHashSet();
        if (grid.Page is < 1 or > 1_000_000 || grid.PageSize < 1 || grid.PageSize > grid.MaxPageSize || grid.Dir is not (null or "asc" or "desc")
            || grid.Search is { Length: > 256 } || grid.Search?.Any(char.IsControl) == true || grid.Filters is { Length: > 16 }
            || grid.Sort is not null && !columns.Contains(grid.Sort)
            || grid.Filters?.Any(f => f is null || !columns.Contains(f.Field) || f.Value is null || f.Value.Length > 256 || f.Value.Any(char.IsControl)) == true
            || grid.Filters is not null && grid.Filters.Select(f => f.Field).Distinct().Count() != grid.Filters.Length)
            throw new ArgumentException("Sipariş tablo filtresi veya sınırı geçerli değil.");
        string Map(string id) => id == "orders.amount" ? "total" : id[7..];
        if (details && (grid.Sort == "orders.count" || grid.Filters?.Any(f => f.Field == "orders.count") == true))
            throw new ArgumentException("Detay satırında sipariş sayısı filtresi/sıralaması kullanılmaz.");
        return new GridRequest { Page = grid.Page, PageSize = grid.PageSize, Search = grid.Search,
            Sort = grid.Sort is null ? null : details ? Map(grid.Sort) : grid.Sort, Dir = grid.Dir,
            Filters = (grid.Filters ?? []).Select(f => new GridFilter(details ? Map(f.Field) : f.Field, f.Op, f.Value)).ToList() };
    }
    private static object? DetailValue(OrderReportRow r, string c) => c switch
    {
        "orders.orderNumber" => r.OrderNumber, "orders.createdAt" => r.CreatedAt,
        "orders.currencyCode" => r.CurrencyCode, "orders.status" => r.Status, "orders.paymentStatus" => r.PaymentStatus,
        "orders.paymentMethod" => r.PaymentMethod, "orders.firmPlatformId" => r.FirmPlatformId,
        "orders.orderType" => r.OrderType, "orders.amount" => r.GrandTotal, "orders.count" => 1L,
        _ => throw new ArgumentException("Sipariş alanı geçerli değil.")
    };
    private static object? SummaryValue(OrderReportSummaryRow r, string c) => c switch
    {
        "orders.currencyCode" => r.CurrencyCode, "orders.status" => r.Status, "orders.paymentStatus" => r.PaymentStatus,
        "orders.firmPlatformId" => r.FirmPlatformId, "orders.orderType" => r.OrderType,
        "orders.amount" => r.OrderAmount, "orders.count" => r.OrderCount,
        _ => throw new ArgumentException("Sipariş alanı geçerli değil.")
    };
}
