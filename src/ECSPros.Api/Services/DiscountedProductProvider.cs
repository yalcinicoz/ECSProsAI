using ECSPros.Shared.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace ECSPros.Api.Services;

/// <summary>
/// H10: kanalda indirimli ürün Id kümesi — vitrin kaynak bayrağı "discountedOnly".
/// İndirim kuralı ürün detayla aynı (StoreUrunDetayBuilder): CompareAtPrice &gt; Price.
/// Tek raw-SQL (channel_variants ↔ product_variants cross-schema, aynı DB), platform
/// bazlı IMemoryCache 2 dk (InStockProductProvider kalıbı).
/// </summary>
public sealed class DiscountedProductProvider(NpgsqlDataSource dataSource, IMemoryCache cache) : IDiscountedProductProvider
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    private const string Sql = @"
        SELECT DISTINCT v.""ProductId""
        FROM storefront.channel_variants cv
        JOIN catalog.product_variants v ON v.""Id"" = cv.""VariantId""
        WHERE cv.""FirmPlatformId"" = @platform
          AND cv.""IsActive""
          AND cv.""Price"" > 0
          AND cv.""CompareAtPrice"" > cv.""Price""";

    public async Task<HashSet<Guid>> GetDiscountedProductIdsAsync(Guid firmPlatformId, CancellationToken ct = default)
    {
        return (await cache.GetOrCreateAsync($"discounted-product-ids-{firmPlatformId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            var set = new HashSet<Guid>();
            await using var conn = await dataSource.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(Sql, conn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("platform", firmPlatformId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) set.Add(r.GetGuid(0));
            return set;
        }))!;
    }
}
