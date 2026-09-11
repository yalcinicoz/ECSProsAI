using ECSPros.Order.Application.Queries.GetOrders;
using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Authorization;
using ECSPros.Shared.Kernel.Grid;
using Microsoft.EntityFrameworkCore;
using OrderEntity = ECSPros.Order.Domain.Entities.Order;

namespace ECSPros.Api.Services.AiReporting;

public sealed record OrderReportScope(DateTimeOffset From, DateTimeOffset To,
    Guid[]? Channels = null, string[]? Statuses = null);

// No customer identity/contact/address or free-form notes in this reporting source.
public sealed record OrderReportRow(Guid Id, string OrderNumber, DateTime CreatedAt,
    Guid FirmPlatformId, string Status, string PaymentStatus, string? PaymentMethod,
    string OrderType, decimal GrandTotal, string CurrencyCode);

public sealed record OrderReportSummaryRow
{
    public string CurrencyCode { get; init; } = "";
    public string? Status { get; init; }
    public string? PaymentStatus { get; init; }
    public Guid? FirmPlatformId { get; init; }
    public string? OrderType { get; init; }
    public long OrderCount { get; init; }
    public decimal OrderAmount { get; init; }
}

/// <summary>
/// Query-only foundation. Caller must use current SERVER authorization and a read-only transaction.
/// No execution, API endpoint, model call, audit write or pagination of aggregate input here.
/// OrderAmount is recorded order value (including cancelled/returned unless filtered), NOT net revenue/refund.
/// </summary>
public sealed class OrderReportSource
{
    private static readonly HashSet<string> FilterFields = new(StringComparer.Ordinal)
    { "orderNumber", "status", "paymentStatus", "paymentMethod", "orderType", "createdAt", "total", "paid", "firmPlatformId", "barcode", "productCode", "currencyCode" };
    private static readonly HashSet<string> SortFields = new(StringComparer.Ordinal)
    { "createdAt", "orderNumber", "total", "status", "paymentStatus", "paymentMethod", "orderType", "firmPlatformId", "currencyCode" };
    private static readonly GridSchema<OrderEntity> CurrencySchema = new GridSchema<OrderEntity>().Text("currencyCode", o => o.CurrencyCode);
    private static readonly GridSchema<OrderEntity> DetailSort = new GridSchema<OrderEntity>()
        .Sort("createdAt", o => o.CreatedAt).Sort("orderNumber", o => o.OrderNumber).Sort("total", o => o.GrandTotal)
        .Sort("status", o => o.Status).Sort("paymentStatus", o => o.PaymentStatus).Sort("paymentMethod", o => o.PaymentMethod)
        .Sort("orderType", o => o.OrderType).Sort("firmPlatformId", o => o.FirmPlatformId).Sort("currencyCode", o => o.CurrencyCode)
        .DefaultSort(o => o.CreatedAt).TieBreaker(o => o.Id);
    private static readonly HashSet<string> GroupFields = new(StringComparer.Ordinal)
    { "status", "paymentStatus", "firmPlatformId", "orderType" };
    private readonly IQueryable<OrderEntity> filtered;
    private readonly GridRequest grid;
    internal IQueryable<OrderEntity> Query => filtered;

    private OrderReportSource(IQueryable<OrderEntity> filtered, GridRequest grid)
    { this.filtered = filtered; this.grid = grid; }

    public static OrderReportSource Create(IQueryable<OrderEntity> source, EfektifYetkiler authorization,
        OrderReportScope scope, GridRequest? request = null, IOrderDbContext? db = null, ReportPredicate? predicate = null, bool forExport = false)
    {
        if (!authorization.Var(ReportDictionary.UsePermission) || !authorization.Var(Permissions.OrdersView))
            throw new UnauthorizedAccessException();
        if (scope.To <= scope.From || scope.To - scope.From > TimeSpan.FromDays(366)
            || scope.Channels is { Length: > 256 } || scope.Channels?.Contains(Guid.Empty) == true
            || scope.Statuses is { Length: > 16 }
            || scope.Statuses?.Any(s => !OrderGrid.Statuses.Contains(s)) == true)
            throw new ArgumentException("Sipariş raporu tarih/kanal/durum kapsamı geçerli değil (en çok366 gün).");
        request ??= new GridRequest();
        if (request.Page < 1 || request.Page > 1_000_000 || request.PageSize < 1 || request.PageSize > (forExport ? ReportExcelExport.MaxRows : 250)
            || request.Dir is not (null or "asc" or "desc") || request.Filters is null
            || request.Filters.Count > 16 || request.Filters.Select(f => f?.Field).Distinct().Count() != request.Filters.Count
            || request.Filters.Any(f => f is null || !FilterFields.Contains(f.Field) || f.Value is null
                || f.Value.Length > 256 || f.Value.Any(char.IsControl))
            || request.Search is { Length: > 256 } || request.Search?.Any(char.IsControl) == true
            || request.Sort is not null && !SortFields.Contains(request.Sort))
            throw new ArgumentException("Sipariş raporu tablo alanı veya sınırı geçerli değil.");

        if (request.Filters.Any(f => f.Field is "productCode" or "barcode")
            && (!authorization.Var(Permissions.CatalogProductsView) || authorization.Kanallar(Permissions.CatalogProductsView) is not null))
            throw new UnauthorizedAccessException();

        // Intersect BOTH permissions. Never trust a client's KanalKisiti, even when null.
        var channels = ReportSourceCatalog.ResolveChannels(OrderReportRecipe.Subject, authorization);
        var safeGrid = new GridRequest
        {
            Page = request.Page, PageSize = request.PageSize, Sort = request.Sort, Dir = request.Dir,
            Filters = request.Filters.ToList(), KanalKisiti = channels
        };
        var named = new OrderListFilters(CreatedFrom: scope.From.UtcDateTime, CreatedTo: scope.To.UtcDateTime,
            Statuses: scope.Statuses?.ToList());
        var query = OrderGrid.ApplyAll(source.AsNoTracking().Where(o => !o.IsDeleted), named, safeGrid.Without("currencyCode"), db: db);
        query = CurrencySchema.ApplyFilters(query, new GridRequest { Filters = safeGrid.Filters.Where(f => f.Field == "currencyCode").ToList() });
        if (scope.Channels is not null) query = query.Where(o => scope.Channels.Contains(o.FirmPlatformId));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // Unlike operational global search, never searches hidden customer/phone fields.
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(o => o.OrderNumber.ToLower().Contains(term));
        }
        if (predicate is not null) query = OrderReportPredicates.Apply(query, predicate, authorization, db);
        return new(query, safeGrid);
    }

    public IQueryable<OrderReportRow> Details() => DetailSort.ApplySort(filtered, grid)
        .Skip(checked((grid.Page - 1) * grid.PageSize)).Take(grid.PageSize)
        .Select(o => new OrderReportRow(o.Id, o.OrderNumber, o.CreatedAt, o.FirmPlatformId,
            o.Status, o.PaymentStatus, o.PaymentMethod, o.OrderType, o.GrandTotal, o.CurrencyCode));

    public IQueryable<OrderReportSummaryRow> Summary(params string[] dimensions)
    {
        if (dimensions is null || dimensions.Length > 4 || dimensions.Distinct().Count() != dimensions.Length
            || dimensions.Any(d => !GroupFields.Contains(d))) throw new ArgumentException("Sipariş kırılımı geçerli değil.");
        var status = dimensions.Contains("status"); var payment = dimensions.Contains("paymentStatus");
        var channel = dimensions.Contains("firmPlatformId"); var type = dimensions.Contains("orderType");
        // Currency is mandatory; no silent FX conversion or cross-currency sum. No one-to-many joins.
        return filtered.GroupBy(o => new
        {
            o.CurrencyCode, Status = status ? o.Status : null, PaymentStatus = payment ? o.PaymentStatus : null,
            FirmPlatformId = channel ? (Guid?)o.FirmPlatformId : null, OrderType = type ? o.OrderType : null
        }).Select(g => new OrderReportSummaryRow { CurrencyCode = g.Key.CurrencyCode, Status = g.Key.Status,
            PaymentStatus = g.Key.PaymentStatus, FirmPlatformId = g.Key.FirmPlatformId, OrderType = g.Key.OrderType,
            OrderCount = g.LongCount(), OrderAmount = g.Sum(o => o.GrandTotal) });
    }

    public ReportAggregateQuery DynamicSummary(ReportAggregatePlan plan, EfektifYetkiler authorization)
    {
        var channels = ReportSourceCatalog.ResolveChannels("orders", authorization);
        return ReportBusinessDictionary.Orders.Aggregates(
                o => !o.IsDeleted && (channels == null || channels.Contains(o.FirmPlatformId)))
            .Build(filtered, plan, ReportSourceCatalog.ResolvePermissions(authorization));
    }

    public ReportDetailQuery<OrderEntity> DynamicDetails(ReportDetailPlan plan, ReportGridState table, EfektifYetkiler authorization)
    {
        var channels = ReportSourceCatalog.ResolveChannels("orders", authorization);
        return ReportBusinessDictionary.Orders.Details(
                o => !o.IsDeleted && (channels == null || channels.Contains(o.FirmPlatformId)), o => o.Id)
            .Build(filtered, plan, table, ReportSourceCatalog.ResolvePermissions(authorization));
    }
}
