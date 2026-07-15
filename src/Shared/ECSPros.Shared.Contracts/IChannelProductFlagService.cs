namespace ECSPros.Shared.Contracts;

/// <summary>
/// B11 (K8): kanal ürünü bayrakları — Catalog listelemelerinin Storefront'a doğrudan
/// referans vermeden "öne çıkar" bilgisine erişmesi için (IChannelPricingService deseni).
/// </summary>
public interface IChannelProductFlagService
{
    /// <summary>Şu an öne çıkarma penceresi içinde olan ürün Id'leri (platform başına az sayıda).</summary>
    Task<HashSet<Guid>> GetFeaturedProductIdsAsync(Guid firmPlatformId, CancellationToken ct = default);

    /// <summary>
    /// Satış görünürlüğü M2/M3 opt-out deny-set: bu platformda kanaldan ÇIKARILMIŞ
    /// (IsActive=false) VEYA ŞU AN durdurulmuş (SaleStopped penceresi içinde) ürün Id'leri.
    /// Storefront listeleri/arama/detay/sepet bu kümeyi eler. Satır yok VEYA seçili+durdurulmamış
    /// → satılır (opt-out). Platform başına küçük küme (çıkarılan/durdurulan azınlık).
    /// </summary>
    Task<HashSet<Guid>> GetChannelExcludedProductIdsAsync(Guid firmPlatformId, CancellationToken ct = default);
}
