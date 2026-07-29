using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

public class MarketplaceProduct : BaseEntity
{
    public Guid FirmIntegrationId { get; set; }
    // Mağaza (core_firm_platforms) bağlantısı — ekranlar mağaza bazlı çalışır; sözleşme
    // (FirmIntegrationId) firma-geneli olabileceğinden kayıt hangi mağazaya aitse burada tutulur.
    public Guid? FirmPlatformId { get; set; }
    public Guid VariantId { get; set; }
    public string ExternalId { get; set; } = string.Empty;      // pazaryerindeki ID
    public string? ExternalBarcode { get; set; }
    public string SyncStatus { get; set; } = "pending";         // pending, synced, failed, deactivated
    public DateTime? LastSyncedAt { get; set; }
    public string? LastSyncError { get; set; }
    public decimal? MarketplacePrice { get; set; }
    public int? MarketplaceStock { get; set; }
    public DateTime? StockSyncedAt { get; set; }

    // F4 — diff-based gönderim + hata sınıflandırma
    public string? LastSentPayloadHash { get; set; }           // değişmeyen içerik yeniden gönderilmez
    public string? LastErrorCode { get; set; }                 // normalize hata (category_conflict, ...)
    public string? SuggestedCategoryExternalId { get; set; }   // reddin işaret ettiği beklenen kategori
}
