using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Procurement.Domain.Entities;

/// <summary>
/// procurement.receipt_batch_purchase_orders — parti ↔ satın alma GEVŞEK bağı (İ3, çoktan-çoğa):
/// "bu partide şu SA'lar var (sanıyoruz)". Kalem düzeyinde eşleşme zorlanmaz; bağ bilgi amaçlıdır.
/// </summary>
public class ReceiptBatchPurchaseOrder : BaseEntity
{
    public Guid ReceiptBatchId { get; set; }
    public ReceiptBatch ReceiptBatch { get; set; } = null!;
    public Guid PurchaseOrderId { get; set; }
}
