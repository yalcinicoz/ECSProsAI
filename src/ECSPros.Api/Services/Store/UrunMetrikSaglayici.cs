using ECSPros.Shared.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// Sıralama metrikleri (2026-08-10): puan/yorum/favori/sepet/görüntülenme/satış sayaçları.
/// Kaynak tablolar dört modül şemasına dağılı olduğundan (storefront, crm, catalog, order)
/// EF context'leriyle tek sorguda birleştirilemez — aynı fiziksel DB'de ham SQL ile
/// şema-ötesi GROUP BY çalıştırılır (MarketplaceAdminService cross-schema okuma kalıbı).
/// Firma-platform başına 10 dk IMemoryCache: liste sıralaması "güncel'e yakın" yeterli,
/// her istekte 5 aggregate sorgusu çalıştırılmaz.
/// </summary>
public sealed class UrunMetrikSaglayici(NpgsqlDataSource dataSource, IMemoryCache cache)
    : IProductMetricsProvider
{
    private static readonly TimeSpan CacheSuresi = TimeSpan.FromMinutes(10);
    /// <summary>Sepet metriği sosyal kanıt sayacıyla aynı pencereyi kullanır (terk edilmiş
    /// eski sepetler sıralamayı şişirmesin).</summary>
    private static readonly TimeSpan SepetPenceresi = TimeSpan.FromDays(30);

    public async Task<IReadOnlyDictionary<Guid, ProductMetrics>> GetAsync(
        Guid firmPlatformId, CancellationToken ct = default)
    {
        var anahtar = $"urun-metrik:{firmPlatformId}";
        if (cache.TryGetValue<IReadOnlyDictionary<Guid, ProductMetrics>>(anahtar, out var mevcut) && mevcut is not null)
            return mevcut;

        var puan = new Dictionary<Guid, (double Ortalama, int Sayi)>();
        var favori = new Dictionary<Guid, int>();
        var sepet = new Dictionary<Guid, int>();
        var goruntulenme = new Dictionary<Guid, int>();
        var satis = new Dictionary<Guid, int>();

        await using var baglanti = await dataSource.OpenConnectionAsync(ct);

        // Onaylı yorumlar + dış kanal özetleri: çok kaynaklı ağırlıklı ortalama + sayı
        // (ProductCode → ürün). Kart görünümüyle aynı kaynak (own + trendyol/amazon/...).
        await using (var cmd = new NpgsqlCommand("""
            WITH src AS (
                SELECT p."Id", SUM(r."Rating")::float8 AS toplam, COUNT(*)::int AS sayi
                FROM storefront.product_reviews r
                JOIN catalog.products p ON p."Code" = r."ProductCode" AND NOT p."IsDeleted"
                WHERE r."FirmPlatformId" = @firm AND r."Status" = 'approved' AND NOT r."IsDeleted"
                GROUP BY p."Id"
                UNION ALL
                SELECT p."Id", SUM(prs."AverageRating" * prs."ReviewCount")::float8 AS toplam,
                       SUM(prs."ReviewCount")::int AS sayi
                FROM storefront.product_rating_sources prs
                JOIN catalog.products p ON p."Code" = prs."ProductCode" AND NOT p."IsDeleted"
                WHERE prs."FirmPlatformId" = @firm AND NOT prs."IsDeleted" AND prs."ReviewCount" > 0
                GROUP BY p."Id"
            )
            SELECT "Id", (SUM(toplam) / NULLIF(SUM(sayi), 0))::float8, SUM(sayi)::int
            FROM src
            GROUP BY "Id"
            """, baglanti))
        {
            cmd.Parameters.AddWithValue("firm", firmPlatformId);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
                puan[rd.GetGuid(0)] = (rd.GetDouble(1), rd.GetInt32(2));
        }

        // Favoriler: farklı üye sayısı (renk bazlı favoriler tek üyeye iner)
        await using (var cmd = new NpgsqlCommand("""
            SELECT p."Id", COUNT(DISTINCT f."MemberId")::int
            FROM storefront.favorites f
            JOIN catalog.products p ON p."Code" = f."ProductCode" AND NOT p."IsDeleted"
            WHERE f."FirmPlatformId" = @firm AND NOT f."IsDeleted"
            GROUP BY p."Id"
            """, baglanti))
        {
            cmd.Parameters.AddWithValue("firm", firmPlatformId);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
                favori[rd.GetGuid(0)] = rd.GetInt32(1);
        }

        // Sepetler: son 30 günde ürünün varyantını içeren farklı sepet sayısı
        await using (var cmd = new NpgsqlCommand("""
            SELECT v."ProductId", COUNT(DISTINCT ci."CartId")::int
            FROM crm.crm_cart_items ci
            JOIN crm.crm_carts c ON c."Id" = ci."CartId" AND NOT c."IsDeleted" AND c."FirmPlatformId" = @firm
            JOIN catalog.product_variants v ON v."Id" = ci."VariantId" AND NOT v."IsDeleted"
            WHERE NOT ci."IsDeleted" AND ci."AddedAt" >= @kesim
            GROUP BY v."ProductId"
            """, baglanti))
        {
            cmd.Parameters.AddWithValue("firm", firmPlatformId);
            cmd.Parameters.AddWithValue("kesim", DateTime.UtcNow - SepetPenceresi);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
                sepet[rd.GetGuid(0)] = rd.GetInt32(1);
        }

        // Görüntülenme: viewed_products üye başına tek satır tutar → satır sayısı = farklı üye
        await using (var cmd = new NpgsqlCommand("""
            SELECT p."Id", COUNT(*)::int
            FROM storefront.viewed_products vp
            JOIN catalog.products p ON p."Code" = vp."ProductCode" AND NOT p."IsDeleted"
            WHERE vp."FirmPlatformId" = @firm AND NOT vp."IsDeleted"
            GROUP BY p."Id"
            """, baglanti))
        {
            cmd.Parameters.AddWithValue("firm", firmPlatformId);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
                goruntulenme[rd.GetGuid(0)] = rd.GetInt32(1);
        }

        // Satış: iptal olmayan siparişlerdeki toplam adet
        await using (var cmd = new NpgsqlCommand("""
            SELECT v."ProductId", SUM(oi."Quantity")::int
            FROM "order".ord_order_items oi
            JOIN "order".ord_orders o ON o."Id" = oi."OrderId" AND NOT o."IsDeleted"
                 AND o."FirmPlatformId" = @firm AND o."Status" <> 'cancelled'
            JOIN catalog.product_variants v ON v."Id" = oi."VariantId" AND NOT v."IsDeleted"
            WHERE NOT oi."IsDeleted"
            GROUP BY v."ProductId"
            """, baglanti))
        {
            cmd.Parameters.AddWithValue("firm", firmPlatformId);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
                satis[rd.GetGuid(0)] = rd.GetInt32(1);
        }

        var sonuc = new Dictionary<Guid, ProductMetrics>();
        foreach (var pid in puan.Keys.Concat(favori.Keys).Concat(sepet.Keys)
                     .Concat(goruntulenme.Keys).Concat(satis.Keys).Distinct())
        {
            var (ort, sayi) = puan.GetValueOrDefault(pid);
            sonuc[pid] = new ProductMetrics(
                Rating: ort, ReviewCount: sayi,
                FavoriteCount: favori.GetValueOrDefault(pid),
                CartCount: sepet.GetValueOrDefault(pid),
                ViewCount: goruntulenme.GetValueOrDefault(pid),
                SalesCount: satis.GetValueOrDefault(pid));
        }

        IReadOnlyDictionary<Guid, ProductMetrics> salt = sonuc;
        cache.Set(anahtar, salt, CacheSuresi);
        return salt;
    }
}
