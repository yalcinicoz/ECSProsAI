using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

/// <summary>
/// Gönderim paketindeki tek satır (varyant düzeyi). Pazaryeri cevapları barkodla eşleşir.
/// Kısmi cevapta yalnız dönen item'lar çözülür; kalan pending aynı ExternalBatchId ile
/// sorgulanmaya devam eder.
/// </summary>
public class MarketplaceBatchItem : BaseEntity
{
    public Guid BatchId { get; set; }
    public Guid ProductId { get; set; }
    public Guid VariantId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;

    public string Status { get; set; } = "pending";            // pending | success | failed | unknown
    public string? ErrorRaw { get; set; }
    public string? ErrorCode { get; set; }                     // normalize (sınıflandırıcı)
    public string? SuggestedCategoryExternalId { get; set; }   // category_conflict: hata mesajından parse edilen beklenen kategori
    public DateTime? ResolvedAt { get; set; }

    // price_stock paketlerinde gönderilen hedef değerler — başarıda listing'e işlenir (F5)
    public decimal? SentPrice { get; set; }
    public int? SentStock { get; set; }
}
