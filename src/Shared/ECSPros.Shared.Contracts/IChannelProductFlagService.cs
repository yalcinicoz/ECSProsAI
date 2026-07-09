namespace ECSPros.Shared.Contracts;

/// <summary>
/// B11 (K8): kanal ürünü bayrakları — Catalog listelemelerinin Storefront'a doğrudan
/// referans vermeden "öne çıkar" bilgisine erişmesi için (IChannelPricingService deseni).
/// </summary>
public interface IChannelProductFlagService
{
    /// <summary>Şu an öne çıkarma penceresi içinde olan ürün Id'leri (platform başına az sayıda).</summary>
    Task<HashSet<Guid>> GetFeaturedProductIdsAsync(Guid firmPlatformId, CancellationToken ct = default);
}
