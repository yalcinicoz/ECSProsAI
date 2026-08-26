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
}
