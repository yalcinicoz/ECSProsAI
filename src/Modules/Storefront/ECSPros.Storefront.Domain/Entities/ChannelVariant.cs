using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// Bir varyantın belirli bir satış kanalındaki (site, pazaryeri, bayi) fiyat override'ı.
/// Kanala özel veri olduğu için Catalog değil Storefront şemasında tutulur.
/// </summary>
public class ChannelVariant : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public Guid VariantId { get; set; }
    public string? PriceType { get; set; } // manual, multiplier
    public decimal? PriceMultiplier { get; set; }
    public decimal? Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public bool IsActive { get; set; } = true;

    // Ürün URL aktarımı (2026-07-15): bu varyantın bu platformdaki gerçek (legacy) URL slug'ı —
    // eski sitenin SEO/bookmark URL'leri yeni sitede çalışsın diye. plurunler.urunUrl'den taşınır;
    // (platform × varyant) bazında ayrı. null = aktarılmamış (o ürün /urun/{code}'ta kalır).
    public string? Slug { get; set; }
}
