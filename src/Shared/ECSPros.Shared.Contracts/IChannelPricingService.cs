namespace ECSPros.Shared.Contracts;

public record ChannelVariantPrice(decimal? Price, decimal? CompareAtPrice);

public interface IChannelPricingService
{
    /// <summary>Bir satış kanalındaki tüm aktif varyant fiyat override'larını döner (variantId → fiyat).</summary>
    Task<Dictionary<Guid, ChannelVariantPrice>> GetActiveVariantPricesAsync(Guid firmPlatformId, CancellationToken ct = default);
}
