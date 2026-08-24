using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Procurement.Domain.Entities;

/// <summary>
/// procurement.receipt_batch_items — teslim evrakındaki KABA satırlar ("t-shirt, 1000 adet, 15 TL").
/// Opsiyoneldir, ayrıştırmayı hiçbir şekilde kısıtlamaz; yalnız mutabakat raporuna girdi.
/// </summary>
public class ReceiptBatchItem : BaseEntity
{
    public Guid ReceiptBatchId { get; set; }
    public ReceiptBatch ReceiptBatch { get; set; } = null!;

    public string DescriptionText { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public int SortOrder { get; set; }
}
