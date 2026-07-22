using ECSPros.Shared.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace ECSPros.Api.Services;

/// <summary>
/// Ürün başına efektif min fiyat (kanal override → varyant BasePrice önceliği — kart
/// gösterimiyle aynı). Tek raw-SQL (cross-schema, aynı DB), platform bazlı 2 dk cache.
/// Kartta ürün BasePrice'a düşen son basamak sözlükte YOKTUR — tüketici
/// GetValueOrDefault(id, product.BasePrice) ile kapatır.
/// </summary>
public sealed class EffectivePriceProvider(NpgsqlDataSource dataSource, IMemoryCache cache) : IEffectivePriceProvider
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    // Kart fiyat önceliği (GetStoreProducts): kanal fiyatı olan varyantların min'i;
    // hiç kanal fiyatı yoksa varyant BasePrice min'i (0'lar sayılmaz).
    private const string Sql = @"
        SELECT v.""ProductId"",
               COALESCE(
                   MIN(cv.""Price"") FILTER (WHERE cv.""Price"" > 0),
                   MIN(v.""BasePrice"") FILTER (WHERE v.""BasePrice"" > 0)
               ) AS fiyat
        FROM catalog.product_variants v
        LEFT JOIN storefront.channel_variants cv
               ON cv.""VariantId"" = v.""Id""
              AND cv.""FirmPlatformId"" = @platform
              AND cv.""IsActive""
        WHERE v.""IsActive"" AND NOT v.""IsDeleted""
        GROUP BY v.""ProductId""
        HAVING COALESCE(
                   MIN(cv.""Price"") FILTER (WHERE cv.""Price"" > 0),
                   MIN(v.""BasePrice"") FILTER (WHERE v.""BasePrice"" > 0)) IS NOT NULL";

    public async Task<Dictionary<Guid, decimal>> GetMinEffectivePricesAsync(
        Guid firmPlatformId, CancellationToken ct = default)
    {
        return (await cache.GetOrCreateAsync($"effective-min-prices-{firmPlatformId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            var map = new Dictionary<Guid, decimal>();
            await using var conn = await dataSource.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(Sql, conn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("platform", firmPlatformId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) map[r.GetGuid(0)] = r.GetDecimal(1);
            return map;
        }))!;
    }
}
