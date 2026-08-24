using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Procurement.Domain.Entities;

/// <summary>
/// procurement.receipt_batches — Mal Kabul Partisi (T2, docs/urun-tedarik-is-akisi.md §2.2).
/// "Koli geldi" kaydı: AÇILIRKEN HİÇBİR KALEM BİLGİSİ ZORUNLU DEĞİL (İ2) — ayrıştırma hemen başlayabilir.
/// Kaba evrak kalemleri (varsa) ve gevşek SA bağları yalnız dönemsel mutabakat raporuna girdidir (İ3/İ4).
/// completed = personelin "bu partide ayrıştırılacak bir şey kalmadı" beyanı; elle verilir, geri açılabilir.
/// </summary>
public class ReceiptBatch : BaseEntity
{
    public string Code { get; set; } = string.Empty;              // MK-YYYYAAGG-0001
    public Guid SupplierId { get; set; }                          // accounts.current_accounts.Id (cari)
    public DateTime ReceivedAt { get; set; }
    public Guid WarehouseId { get; set; }                         // inventory.inv_warehouses (gevşek referans)
    public int? PackageCount { get; set; }                        // koli sayısı
    public string? DeliveryNoteNumber { get; set; }               // irsaliye no
    public Guid? SupplierInvoiceId { get; set; }                  // finance.fin_supplier_invoices (gevşek, opsiyonel)
    /// <summary>received | sorting | completed</summary>
    public string Status { get; set; } = "received";
    public Guid? ReceivedBy { get; set; }
    public string? Notes { get; set; }

    public ICollection<ReceiptBatchItem> Items { get; set; } = new List<ReceiptBatchItem>();
    public ICollection<ReceiptBatchPurchaseOrder> PurchaseOrders { get; set; } = new List<ReceiptBatchPurchaseOrder>();

    public static readonly string[] Statuses = ["received", "sorting", "completed"];
}
