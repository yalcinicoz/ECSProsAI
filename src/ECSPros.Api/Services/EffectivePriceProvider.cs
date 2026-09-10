using ECSPros.Shared.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace ECSPros.Api.Services;

/// <summary>
/// Ürün başına KART fiyatı (varyant başına kanal fiyatı ?? BasePrice, en yükseği — kart
/// gösterimiyle aynı, bkz. KartFiyatGorunumu.KartTabanFiyati). Tek raw-SQL (cross-schema, aynı DB),
/// platform bazlı 2 dk cache.
/// Kartta ürün BasePrice'a düşen son basamak sözlükte YOKTUR — tüketici
/// GetValueOrDefault(id, product.BasePrice) ile kapatır.
/// </summary>
public sealed class EffectivePriceProvider(NpgsqlDataSource dataSource, IMemoryCache cache) : IEffectivePriceProvider
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    // Kart fiyat kuralı (GetStoreProducts / kategori listesiyle aynı, 2026-09-10): her varyantın
    // efektif fiyatı = kanal fiyatı (>0) yoksa BasePrice (>0); ürün fiyatı bunların EN YÜKSEĞİ.
    // ★ Kanal fiyatlarının ve taban fiyatların ayrı ayrı MIN/MAX'ı alınmaz — kanal fiyatı çoğu üründe
    // bedenlerin yalnız bir kısmında vardır; ayrı alınınca kart 299,99 gösterip S/M 399,99'a satıyordu.
    private const string Sql = @"
        SELECT v.""ProductId"",
               MAX(CASE WHEN cv.""Price"" > 0 THEN cv.""Price""
                        WHEN v.""BasePrice"" > 0 THEN v.""BasePrice"" END) AS fiyat
        FROM catalog.product_variants v
        LEFT JOIN storefront.channel_variants cv
               ON cv.""VariantId"" = v.""Id""
              AND cv.""FirmPlatformId"" = @platform
              AND cv.""IsActive""
        WHERE v.""IsActive"" AND NOT v.""IsDeleted""
        GROUP BY v.""ProductId""
        HAVING MAX(CASE WHEN cv.""Price"" > 0 THEN cv.""Price""
                        WHEN v.""BasePrice"" > 0 THEN v.""BasePrice"" END) IS NOT NULL";

    public async Task<Dictionary<Guid, decimal>> GetKartFiyatlariAsync(
        Guid firmPlatformId, CancellationToken ct = default)
    {
        return (await cache.GetOrCreateAsync($"kart-fiyatlari-{firmPlatformId}", async entry =>
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
