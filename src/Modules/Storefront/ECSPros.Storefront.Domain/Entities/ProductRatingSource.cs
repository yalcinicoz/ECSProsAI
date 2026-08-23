using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// Çok kaynaklı ürün puan özeti — ürün × kanal başına bir satır (kalıcı, pazaryeri
/// agnostik). "own" kanalının özeti product_reviews tablosundan türetilir (ayrı satır
/// tutulmaz); dış kanalların (trendyol/amazon/hepsiburada/n11) özeti senkron sırasında
/// buraya yazılır. Vitrinde gösterilen ortalama/dağılım, platformun görünüm ayarına
/// (ProductReviewDisplaySettings) göre bu satırlar + kendi-site yorumları birleştirilerek
/// üretilir.
/// </summary>
public class ProductRatingSource : BaseEntity
{
    public Guid FirmPlatformId { get; set; }

    /// <summary>catalog.products.Code ile eşleşir (vitrin/kart puanları Code üzerinden birleşir).</summary>
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>Kaynak kanal: own | trendyol | amazon | hepsiburada | n11</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>Kanalın kendi ürün kimliği (contentId/ASIN/...).</summary>
    public string? ExternalProductId { get; set; }

    /// <summary>Kanaldaki ortalama puan (örn. 4.70).</summary>
    public decimal AverageRating { get; set; }

    /// <summary>Kanaldaki toplam yorum sayısı.</summary>
    public int ReviewCount { get; set; }

    /// <summary>Kanaldaki yorum/değerlendirme sayfası bağlantısı (varsa).</summary>
    public string? ExternalUrl { get; set; }

    /// <summary>Son senkron zamanı (null = henüz senkron edilmedi).</summary>
    public DateTime? LastSyncedAt { get; set; }
}
