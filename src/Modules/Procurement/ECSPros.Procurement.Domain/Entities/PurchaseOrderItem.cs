using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Procurement.Domain.Entities;

/// <summary>
/// procurement.purchase_order_items — SA kalemi. Satın alma listesi varyant düzeyi ayrıntı taşır
/// (model/renk/beden/fiyat/adet) ama VARYANT BAĞLAMAK ZORUNLU DEĞİLDİR: katalogda henüz olmayan ürün
/// serbest metinle yazılır, sonradan bağlanabilir (K4/İ3). Excel'den panoya yapıştırma bu kalemleri üretir.
/// </summary>
public class PurchaseOrderItem : BaseEntity
{
    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public Guid? VariantId { get; set; }          // catalog.product_variants (gevşek referans, FK yok)
    public string? ModelText { get; set; }
    public string? ColorText { get; set; }
    public string? SizeText { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
}
