using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Procurement.Domain.Entities;

/// <summary>
/// procurement.sorting_entries — Ayrıştırma kaydı (T4, docs/urun-tedarik-is-akisi.md §2.3): SİSTEMİN KALBİ.
/// "Bu varyanttan bu kadar saydım" beyanı. Sayım = gerçek (İ1): stok girişinin tek kaynağı budur
/// (yerleştirme T5'te bu kayıttan stok üretir). Partisiz de olabilir (İ2). K9: varyant MEVCUT karttan
/// eşlenir; kart yoksa kayıt açılamaz → MissingCardNotice düşülür.
/// </summary>
public class SortingEntry : BaseEntity
{
    public Guid? ReceiptBatchId { get; set; }
    public Guid VariantId { get; set; }                 // catalog.product_variants (gevşek referans)
    public decimal Quantity { get; set; }
    /// <summary>Alış maliyeti (opsiyonel, elle) — dönem raporu/fiyat revizyonu girdisi.</summary>
    public decimal? UnitCost { get; set; }
    public bool LabelPrinted { get; set; }
    public int LabelCount { get; set; }
    /// <summary>pending | placed — placed olunca stok girişi yapılmıştır (T5).</summary>
    public string PutawayStatus { get; set; } = "pending";
    public Guid? PlacedBinId { get; set; }
    public DateTime? PlacedAt { get; set; }
    public Guid? StockMovementId { get; set; }
    /// <summary>İlk kez satışa girdiği (stok + published) gün — T6 worker damgalar (İ5).</summary>
    public DateTime? OnSaleAt { get; set; }
}
