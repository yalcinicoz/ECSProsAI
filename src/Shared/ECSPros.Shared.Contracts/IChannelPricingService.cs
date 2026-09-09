namespace ECSPros.Shared.Contracts;

public record ChannelVariantPrice(decimal? Price, decimal? CompareAtPrice);

public interface IChannelPricingService
{
    /// <summary>Bir satış kanalındaki tüm aktif varyant fiyat override'larını döner (variantId → fiyat).
    /// UYARI (Faz 2 P0): sıcak yollarda KULLANMA — sayfadaki varyantlarla sınırlı overload'u tercih et.</summary>
    Task<Dictionary<Guid, ChannelVariantPrice>> GetActiveVariantPricesAsync(Guid firmPlatformId, CancellationToken ct = default);

    /// <summary>Faz 2 P0 (kod optimizasyon raporu #1): yalnız verilen varyantların kanal fiyatları —
    /// sayfa başına DB'den taşınan satır/allocation platform kataloğundan bağımsızlaşır.</summary>
    Task<Dictionary<Guid, ChannelVariantPrice>> GetActiveVariantPricesAsync(
        Guid firmPlatformId, IReadOnlyCollection<Guid> variantIds, CancellationToken ct = default);

    /// <summary>
    /// M3 (2026-09-09): ÜRÜN düzeyinde çizili (indirim öncesi) referans fiyat — ürünün aktif
    /// varyantlarındaki EN YÜKSEK <c>CompareAtPrice</c>. Liste kartı ve ürün detayı bu kuralı
    /// kullanır; sepet satırı da aynı referansı gösterebilsin diye buraya taşındı (varyantın
    /// KENDİ çizili fiyatı yoksa kartta indirim görünüp sepette görünmüyordu).
    /// Referansı olmayan ürün sözlükte yer almaz.
    /// </summary>
    Task<Dictionary<Guid, decimal>> GetProductCompareAtPricesAsync(
        Guid firmPlatformId, IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);
}
