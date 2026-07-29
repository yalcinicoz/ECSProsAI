using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

/// <summary>
/// Asenkron pazaryeri gönderim paketi (§4, K7): pazaryerine tek istekte gönderilen ürün/stok
/// kümesi. Sonuç hemen dönmez — ExternalBatchId ile ayrı uçtan sorgulanır; kısmi cevap
/// normaldir, item'lar tek tek çözülür. Kesilen sorgulamayı worker backoff'la sürdürür;
/// zaman aşımında kalan item'lar 'unknown' olur ve KÖRLEMESİNE yeniden gönderilmez
/// (duplicate riski) — mutabakat senkronu doğrular (F5).
/// </summary>
public class MarketplaceBatch : BaseEntity
{
    public string Marketplace { get; set; } = string.Empty;
    public Guid FirmPlatformId { get; set; }
    public Guid FirmIntegrationId { get; set; }
    public string? ExternalBatchId { get; set; }               // pazaryerinin batchRequestId'si
    public string BatchType { get; set; } = "product_upsert";  // product_upsert | price_stock

    /// <summary>submitted → polling → completed | completed_with_errors | timed_out | failed
    /// (failed = gönderim isteği hiç kabul edilmedi).</summary>
    public string Status { get; set; } = "submitted";

    public int ItemCount { get; set; }
    public int ResolvedCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }

    public int PollAttempts { get; set; }
    public DateTime? NextPollAt { get; set; }
    public DateTime? LastPolledAt { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public string? Error { get; set; }
}
