using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Authorization;

namespace ECSPros.Api.Services.AiReporting;

public static class ReturnReportSource
{
    public const string Id = "returns";
    public static ReportEntityDefinition<Return> Dictionary { get; } = new ReportEntityDefinition<Return>(Permissions.OrdersReturnsView)
        .Text("returns.orderNumber", "Sipariş no", r => r.Order.OrderNumber)
        .Text("returns.status", "İade kaydı durumu", r => r.Status)
        .Text("returns.type", "İade tipi (kayıtlı kod)", r => r.ReturnType)
        .Text("returns.refundStatus", "Geri ödeme kaydı durumu", r => r.RefundStatus)
        .Text("returns.currencyCode", "Bağlı sipariş para birimi", r => r.Order.CurrencyCode)
        .Value("returns.createdAt", "İade kaydı oluşturulma tarihi", r => r.CreatedAt)
        .Count("returns.count", "İade kaydı sayısı (sipariş sayısı değildir)")
        .Measure("returns.amount", "Kayıtlı iade tutarı (ödenmiş tutar değildir)", r => r.RefundAmount, "sum", "returns.currencyCode", filterable: true)
        .Seal();

    public static IQueryable<Return> Apply(IQueryable<Return> rows, DynamicReportPlan plan, EfektifYetkiler effective, OrderReportScope? resolvedPeriod = null)
    {
        if (plan.Source != Id) throw new ArgumentException("İade kaynağı bekleniyor.");
        var permissions = StockReportExecutor.ResolvePermissions(effective);
        if (!permissions.Contains(Permissions.OrdersReturnsView)) throw new UnauthorizedAccessException();
        var channels = ReportSourceCatalog.ResolveChannels(Id, effective);
        var period = resolvedPeriod ?? plan.Scope();
        var from = period.From.UtcDateTime; var to = period.To.UtcDateTime;
        // Same ownership as the returns screen; explicit soft-delete checks also cover in-memory queries.
        rows = rows.Where(r => !r.IsDeleted && r.Order != null && !r.Order.IsDeleted && r.CreatedAt >= from && r.CreatedAt < to);
        if (channels is not null) rows = rows.Where(r => channels.Contains(r.Order.FirmPlatformId));
        return plan.Predicate is null ? rows : Dictionary.Predicates(_ => true).Apply(rows, plan.Predicate, permissions);
    }
}
