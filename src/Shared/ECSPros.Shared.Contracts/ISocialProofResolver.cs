namespace ECSPros.Shared.Contracts;

/// <summary>
/// Ürün Kartı — sosyal kanıt sayaçları (2026-08-10): "X kişinin sepetinde" /
/// "X kişinin favorisi" satırlarının canlı sayıları. Sepet sayısı son 30 günde
/// ürünün herhangi bir varyantını içeren FARKLI sepet sayısıdır (üye + misafir);
/// favori sayısı ürün kodunu favorileyen farklı üye sayısıdır (renk bazlı favoriler
/// tek üyeye iner). Kart query'lerinde puan/kampanya/mesaj gibi cache SONRASI eklenir.
/// </summary>
public record SocialProofCounts(int CartCount, int FavoriteCount);

/// <summary>
/// Sayfa ürünleri için sosyal kanıt sayaçlarını çözer. Implementasyon API host'undadır
/// (sepet CRM'de, favori Storefront'ta, varyant→ürün eşlemesi Catalog'da — üç modülü
/// yalnız host görür); süreç-içi kısa TTL cache ile her istekte DB'ye gitmez.
/// </summary>
public interface ISocialProofResolver
{
    /// <param name="productCodesById">sayfadaki ürünler: ProductId → ürün kodu</param>
    /// <returns>ProductId → sayaçlar (iki sayacı da 0 olan ürün yer almaz)</returns>
    Task<Dictionary<Guid, SocialProofCounts>> ResolveForProductsAsync(
        Guid firmPlatformId,
        IReadOnlyDictionary<Guid, string> productCodesById,
        CancellationToken ct = default);
}
