using ECSPros.Shared.Contracts;

namespace ECSPros.Api.Services;

public static class StockCacheInvalidation
{
    private static readonly string[] KnownKeys =
    [
        "in-stock-product-ids",
        "in-stock-variant-ids",
        "channel-stock:variant-net"
    ];

    public static void Bust(ICacheBustPublisher publisher)
    {
        ArgumentNullException.ThrowIfNull(publisher);
        foreach (var key in KnownKeys)
            publisher.Bust(key);
    }
}
