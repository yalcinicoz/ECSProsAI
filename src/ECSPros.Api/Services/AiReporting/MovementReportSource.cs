using ECSPros.Inventory.Domain.Entities;
using ECSPros.Shared.Kernel.Authorization;

namespace ECSPros.Api.Services.AiReporting;

public static class MovementReportSource
{
    public const string Id = "stockMovements";
    public static ReportEntityDefinition<StockMovement> Dictionary { get; } = new ReportEntityDefinition<StockMovement>(Permissions.InventoryView)
        .Text("movements.type", "Hareket tipi (kaynaktaki kod)", m => m.MovementType)
        .Text("movements.fromWarehouseId", "Kaynak depo kimliği", m => m.FromWarehouseId.HasValue ? m.FromWarehouseId.Value.ToString() : null)
        .Text("movements.toWarehouseId", "Hedef depo kimliği", m => m.ToWarehouseId.HasValue ? m.ToWarehouseId.Value.ToString() : null)
        .Text("movements.variantId", "Varyant kimliği (ürün kodu/barkod değildir)", m => m.VariantId.ToString())
        .Text("movements.referenceType", "Bağlı işlem tipi", m => m.ReferenceType)
        .Value("movements.createdAt", "Hareket kayıt tarihi", m => m.CreatedAt)
        .Count("movements.count", "Hareket kaydı sayısı")
        .Measure("movements.quantity", "Kayıtlı hareket adedi (net stok değişimi değildir)", m => (decimal)m.Quantity, "sum", filterable: true)
        .Seal();

    public static IQueryable<StockMovement> Apply(IQueryable<StockMovement> rows, DynamicReportPlan plan, EfektifYetkiler effective, OrderReportScope? resolvedPeriod = null)
    {
        if (plan.Source != Id) throw new ArgumentException("Hareket kaynağı bekleniyor.");
        var permissions = StockReportExecutor.ResolvePermissions(effective);
        // Inventory permission has no warehouse-scope implementation: scoped grants must fail closed.
        if (!permissions.Contains(Permissions.InventoryView)) throw new UnauthorizedAccessException();
        var period = resolvedPeriod ?? plan.Scope();
        var from = period.From.UtcDateTime; var to = period.To.UtcDateTime;
        rows = rows.Where(m => m.CreatedAt >= from && m.CreatedAt < to);
        return plan.Predicate is null ? rows : Dictionary.Predicates(_ => true).Apply(rows, plan.Predicate, permissions);
    }
}
