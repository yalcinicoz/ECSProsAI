using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

/// <summary>
/// Pazaryeri sorun kuyruğu (§6): otomatik açılır, koşul ortadan kalkınca OTOMATİK kapanır —
/// kuyruk çöplüğe dönmez. ConditionKey aynı koşul için duplicate açılmasını önler
/// (açık kayıtta unique); koşul yeniden oluşursa yeni kayıt açılır (geçmiş korunur).
/// Eşleme sağlığı bu kuyruğa GİRMEZ (onun yeri Eşleştirme → Gözden Geçir) — burası
/// operasyonel sorunlar içindir: gönderim reddi, zaman aşımı, fiyat/stok sapması,
/// pazaryerinde kaybolan ürün.
/// </summary>
public class MarketplaceIssue : BaseEntity
{
    public string Marketplace { get; set; } = string.Empty;
    public Guid FirmPlatformId { get; set; }                 // mağaza — sorunlar mağaza bağlamlıdır

    /// <summary>price_drift | stock_drift | missing_on_marketplace | unlisted_remote |
    /// batch_timed_out | upload_failed</summary>
    public string IssueType { get; set; } = string.Empty;
    public string ConditionKey { get; set; } = string.Empty; // ör. "price_drift:{barcode}"

    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? SuggestedAction { get; set; }
    public string? ReferenceType { get; set; }               // product | variant | batch
    public Guid? ReferenceId { get; set; }

    public string Status { get; set; } = "open";             // open | resolved | dismissed
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
