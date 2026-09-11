using System.Data;
using ECSPros.Iam.Application.Services;
using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECSPros.Api.Services.AiReporting;

public sealed class StaffActivityExecutor(NpgsqlDataSource source, IEtkinYetkiServisi authorization)
{
    private static readonly SemaphoreSlim Slots = ReportExecutionBudget.Slots;
    private OrderDbContext Context() => new(new DbContextOptionsBuilder<OrderDbContext>().UseNpgsql(source).Options);
    public void Validate(DynamicReportPlan plan, EfektifYetkiler effective)
    {
        using var db = Context();
        var rows = StaffActivitySource.Query(db, plan, effective);
        var permissions = StockReportExecutor.ResolvePermissions(effective);
        if (plan.Detail is not null) _ = StaffActivitySource.Dictionary.Details(_ => true, m => m.Id).Build(rows, plan.Detail, new(), permissions);
        else _ = StaffActivitySource.Dictionary.Aggregates(_ => true).Build(rows, plan.Aggregate!, permissions);
    }
    public async Task<StockReportResult> ExecuteAsync(Guid userId, DynamicReportPlan plan, ReportGridState grid, CancellationToken ct)
    {
        if (userId == Guid.Empty) throw new UnauthorizedAccessException();
        var effective = await authorization.GetirAsync(userId, ct);
        await using var db = Context();
        var period = plan.Scope();
        var rows = StaffActivitySource.Query(db, plan, effective, period);
        var permissions = StockReportExecutor.ResolvePermissions(effective);
        var detail = plan.Detail is null ? null : StaffActivitySource.Dictionary.Details(_ => true, m => m.Id).Build(rows, plan.Detail, grid, permissions);
        var aggregate = plan.Aggregate is null ? null : StaffActivitySource.Dictionary.Aggregates(_ => true).Build(rows, plan.Aggregate, permissions);
        var filtered = aggregate is null ? null : ReportAggregateGrid.Apply(aggregate, plan.Aggregate!, grid);
        var paged = filtered?.Skip(checked((grid.Page - 1) * grid.PageSize)).Take(grid.PageSize);
        if (!await Slots.WaitAsync(0, ct)) throw new InvalidOperationException("Rapor sistemi meşgul.");
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(30)); ct = deadline.Token;
            _ = detail is not null ? detail.Page.ToQueryString() : paged!.ToQueryString();
            ct.ThrowIfCancellationRequested();
            db.Database.SetCommandTimeout(15);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY; SET LOCAL statement_timeout = '15s'", ct);
            var count = detail is not null ? await detail.Filtered.LongCountAsync(ct) : await filtered!.LongCountAsync(ct);
            if (grid.ExportMode) ReportExcelExport.CheckCount(count);
            var result = detail is not null ? await detail.Page.ToListAsync(ct)
                : (await paged!.ToListAsync(ct)).Select(r => ReportAggregateGrid.Values(r, plan.Aggregate!.Dimensions!.Length, plan.Aggregate.Measures!.Length)).ToList();
            await tx.CommitAsync(ct);
            return new(detail?.Columns ?? aggregate!.Columns, result, DateTimeOffset.UtcNow, count, new Dictionary<string, decimal>()) { ResolvedPeriod = period };
        }
        finally { Slots.Release(); }
    }
}
