using ECSPros.Shared.Contracts.Channels;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace ECSPros.Api.Services;

/// <summary>
/// F2 Listeleme durumu hesaplayıcısı (docs/satis-kanali-ortak-kurgu.md §3.2): bir ürünün seçili kanalda
/// fiilen yayında/satışta olup olmadığı — değilse NEDEN — sorusunun tek cevabı. Kanal yeteneğine göre
/// kural seti (light / light_price / full+push). Cross-schema raw-SQL okuma katmanı (MarketplaceAdminService
/// kalıbı) + 2 dk IMemoryCache anlık görüntü; denormalize kolon YOK — hibrit kapsam (all=örtük satır)
/// denormalizasyonu anlamsız kılar, id-kümeleriyle bellek içi hesap yeterli (plan §2.2'den sapma, F2 notu).
///
/// Durumlar: published | ready | missing_info | blocked | pending | failed | deactivated
/// Sebep kodları (§3.2 kataloğu): channel_excluded, sale_stopped, sale_closed, out_of_stock, price_zero,
/// no_channel_price, readiness_unknown, no_category_mapping/required_attr_missing/... (readiness ReasonsJson),
/// push_pending, push_failed:&lt;kod&gt;, deactivated.
/// </summary>
public sealed class ChannelListingStatusService(
    NpgsqlDataSource dataSource,
    IMemoryCache cache,
    IChannelCapabilityResolver capabilityResolver,
    IChannelStockCalculator stockCalculator)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    public sealed record ListingStatusDto(string Status, List<string> Reasons);
    public sealed record ListingSummaryDto(
        Dictionary<string, int> StatusCounts,
        Dictionary<string, int> ReasonCounts,
        int Total);

    /// <summary>Kanalın anlık görüntüsü: id-kümeleri + pazaryeri haritaları. 2 dk cache.</summary>
    private sealed record Snapshot(
        ChannelCapabilities Caps,
        HashSet<Guid> Base,               // kapsam+görselli ürünler (yönetilen evren)
        HashSet<Guid> ChannelExcluded,    // IsActive=false || IsExcluded
        HashSet<Guid> SaleStopped,        // an itibarıyla durdurulmuş
        HashSet<Guid> SaleClosed,         // Product.IsSaleOpen=false
        HashSet<Guid> PriceZero,          // BasePrice <= 0
        HashSet<Guid> InChannelStock,     // K17: stockQuantity >= 1 (kanal minStock)
        HashSet<Guid>? ChannelPriced,     // yalnız light_price: kanal fiyatı olan ürünler
        Dictionary<Guid, (string Status, List<string> Reasons)>? Readiness, // yalnız full
        Dictionary<Guid, (int Pending, int Synced, int Failed, int Deactivated, string? ErrorCode)>? Push);

    public void Invalidate(Guid firmPlatformId) => cache.Remove(Key(firmPlatformId));
    private static string Key(Guid id) => $"listing-status:{id:N}";

    public async Task<ListingStatusDto> ComputeAsync(Guid firmPlatformId, Guid productId, CancellationToken ct)
        => (await ComputeManyAsync(firmPlatformId, new[] { productId }, ct))[productId];

    public async Task<Dictionary<Guid, ListingStatusDto>> ComputeManyAsync(
        Guid firmPlatformId, IReadOnlyCollection<Guid> productIds, CancellationToken ct)
    {
        var s = await GetSnapshotAsync(firmPlatformId, ct);
        return productIds.Distinct().ToDictionary(id => id, id => Compute(s, id));
    }

    public async Task<ListingSummaryDto> GetSummaryAsync(Guid firmPlatformId, CancellationToken ct)
    {
        var s = await GetSnapshotAsync(firmPlatformId, ct);
        var statusCounts = new Dictionary<string, int>();
        var reasonCounts = new Dictionary<string, int>();
        foreach (var id in s.Base)
        {
            var r = Compute(s, id);
            statusCounts[r.Status] = statusCounts.GetValueOrDefault(r.Status) + 1;
            foreach (var reason in r.Reasons)
                reasonCounts[reason] = reasonCounts.GetValueOrDefault(reason) + 1;
        }
        return new ListingSummaryDto(statusCounts, reasonCounts, s.Base.Count);
    }

    /// <summary>Verilen duruma/sebebe uyan ürün Id'leri (F3 liste filtresi için).</summary>
    public async Task<HashSet<Guid>> GetProductIdsByStatusAsync(
        Guid firmPlatformId, string? status, string? reason, CancellationToken ct)
    {
        var s = await GetSnapshotAsync(firmPlatformId, ct);
        var result = new HashSet<Guid>();
        foreach (var id in s.Base)
        {
            var r = Compute(s, id);
            if (status is not null && r.Status != status) continue;
            if (reason is not null && !r.Reasons.Any(x => x == reason || x.StartsWith(reason + ":"))) continue;
            result.Add(id);
        }
        return result;
    }

    // ── Durum çözümü (§3.2 sırası) ────────────────────────────────────────────
    private static ListingStatusDto Compute(Snapshot s, Guid id)
    {
        var reasons = new List<string>();

        // 1) Engelli (blocked): kanal kararı kapalı / durdurulmuş / satış kapalı / stok yok (K17)
        if (s.ChannelExcluded.Contains(id)) reasons.Add("channel_excluded");
        if (s.SaleStopped.Contains(id)) reasons.Add("sale_stopped");
        if (s.SaleClosed.Contains(id)) reasons.Add("sale_closed");
        if (!s.InChannelStock.Contains(id)) reasons.Add("out_of_stock");
        var blocked = reasons.Count > 0;

        // 2) Eksik bilgi sebepleri (hazırlık)
        if (s.PriceZero.Contains(id)) reasons.Add("price_zero");
        if (s.ChannelPriced is not null && !s.ChannelPriced.Contains(id)) reasons.Add("no_channel_price");

        string? readinessStatus = null;
        if (s.Readiness is not null)
        {
            if (s.Readiness.TryGetValue(id, out var rd))
            {
                readinessStatus = rd.Status;
                if (rd.Status != "ready") reasons.AddRange(rd.Reasons.DefaultIfEmpty("readiness_unknown"));
            }
            else { readinessStatus = null; reasons.Add("readiness_unknown"); }
        }

        // 3) Push kanalı: yükleme durumu her şeyi belirler (engel yoksa)
        if (s.Push is not null)
        {
            s.Push.TryGetValue(id, out var push);
            if (push.Failed > 0) reasons.Add(push.ErrorCode is null ? "push_failed" : $"push_failed:{push.ErrorCode}");

            if (blocked) return new ListingStatusDto("blocked", reasons);
            if (push.Deactivated > 0 && push.Synced == 0 && push.Pending == 0 && push.Failed == 0)
                return new ListingStatusDto("deactivated", reasons.Append("deactivated").ToList());
            if (push.Failed > 0) return new ListingStatusDto("failed", reasons);
            if (push.Pending > 0) return new ListingStatusDto("pending", reasons.Append("push_pending").ToList());
            if (push.Synced > 0) return new ListingStatusDto("published", reasons);
            // hiç yüklenmemiş: hazırsa ready, değilse missing_info
            var hazir = readinessStatus == "ready"
                        && !reasons.Contains("price_zero") && !reasons.Contains("no_channel_price");
            return new ListingStatusDto(hazir ? "ready" : "missing_info", reasons);
        }

        // 4) Web/dropship: engel → blocked; hazırlık sebebi → missing_info; yoksa yayında
        if (blocked) return new ListingStatusDto("blocked", reasons);
        return new ListingStatusDto(reasons.Count > 0 ? "missing_info" : "published", reasons);
    }

    // ── Anlık görüntü ─────────────────────────────────────────────────────────
    private async Task<Snapshot> GetSnapshotAsync(Guid firmPlatformId, CancellationToken ct)
    {
        return (await cache.GetOrCreateAsync(Key(firmPlatformId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return await BuildSnapshotAsync(firmPlatformId, ct);
        }))!;
    }

    private async Task<Snapshot> BuildSnapshotAsync(Guid firmPlatformId, CancellationToken ct)
    {
        var caps = await capabilityResolver.GetAsync(firmPlatformId, ct);
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        // Kapsam evreni: görselli ürünler; filter|mixed kanalda InScope && !IsExcluded satırları
        const string baseSql = @"
            SELECT p.""Id"",
                   NOT p.""IsSaleOpen"" AS sale_closed,
                   (p.""BasePrice"" IS NULL OR p.""BasePrice"" <= 0) AS price_zero,
                   (cp.""Id"" IS NOT NULL AND (NOT cp.""IsActive"" OR cp.""IsExcluded"")) AS ch_excluded,
                   (cp.""SaleStoppedFrom"" IS NOT NULL AND cp.""SaleStoppedFrom"" <= now()
                    AND (cp.""SaleStoppedUntil"" IS NULL OR cp.""SaleStoppedUntil"" >= now())) AS stopped
            FROM catalog.products p
            LEFT JOIN storefront.channel_products cp
                   ON cp.""FirmPlatformId"" = @platform AND cp.""ProductId"" = p.""Id"" AND NOT cp.""IsDeleted""
            LEFT JOIN storefront.channel_scopes sc ON sc.""FirmPlatformId"" = @platform AND NOT sc.""IsDeleted""
            WHERE NOT p.""IsDeleted""
              AND EXISTS (SELECT 1 FROM catalog.product_images img WHERE img.""ProductId"" = p.""Id"" AND NOT img.""IsDeleted"")
              AND (CASE WHEN sc.""FillType"" IN ('filter','mixed')
                        THEN cp.""Id"" IS NOT NULL AND cp.""InScope"" AND NOT cp.""IsExcluded""
                        ELSE TRUE END)";

        var baseSet = new HashSet<Guid>();
        var chExcluded = new HashSet<Guid>();
        var stopped = new HashSet<Guid>();
        var saleClosed = new HashSet<Guid>();
        var priceZero = new HashSet<Guid>();
        await using (var cmd = new NpgsqlCommand(baseSql, conn) { CommandTimeout = 60 })
        {
            cmd.Parameters.AddWithValue("platform", firmPlatformId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var id = r.GetGuid(0);
                baseSet.Add(id);
                if (r.GetBoolean(1)) saleClosed.Add(id);
                if (r.GetBoolean(2)) priceZero.Add(id);
                if (r.GetBoolean(3)) chExcluded.Add(id);
                if (r.GetBoolean(4)) stopped.Add(id);
            }
        }

        // K17: kanal stok kümesi (minStock kanal yeteneğinden)
        var inStock = await stockCalculator.GetProductIdsWithChannelStockAsync(caps.MinStock, ct);

        // light_price: kanal fiyatı olan ürünler
        HashSet<Guid>? channelPriced = null;
        if (caps.ReadinessLevel is ChannelCapabilities.ReadinessLevels.LightPrice or ChannelCapabilities.ReadinessLevels.Full
            && caps.PriceSource == ChannelCapabilities.PriceSources.ChannelPriceList)
        {
            channelPriced = new HashSet<Guid>();
            const string priceSql = @"
                SELECT DISTINCT v.""ProductId""
                FROM storefront.channel_variants cv
                JOIN catalog.product_variants v ON v.""Id"" = cv.""VariantId"" AND NOT v.""IsDeleted""
                WHERE cv.""FirmPlatformId"" = @platform AND cv.""IsActive"" AND NOT cv.""IsDeleted"" AND cv.""Price"" > 0";
            await using var cmd = new NpgsqlCommand(priceSql, conn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("platform", firmPlatformId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) channelPriced.Add(r.GetGuid(0));
        }

        // full: hazırlık (readiness) + push durumu
        Dictionary<Guid, (string, List<string>)>? readiness = null;
        Dictionary<Guid, (int, int, int, int, string?)>? push = null;
        if (caps.ReadinessLevel == ChannelCapabilities.ReadinessLevels.Full || caps.PushListing)
        {
            var marketplace = await GetMarketplaceCodeAsync(conn, firmPlatformId, ct);
            if (caps.ReadinessLevel == ChannelCapabilities.ReadinessLevels.Full && marketplace is not null)
            {
                readiness = new Dictionary<Guid, (string, List<string>)>();
                const string rdSql = @"
                    SELECT rd.""ProductId"", rd.""Status"", rd.""ReasonsJson""::text
                    FROM integration.marketplace_product_readiness rd
                    WHERE rd.""Marketplace"" = @mp AND NOT rd.""IsDeleted""
                      AND (rd.""FirmPlatformId"" IS NULL OR rd.""FirmPlatformId"" = @platform)";
                await using var cmd = new NpgsqlCommand(rdSql, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("mp", marketplace);
                cmd.Parameters.AddWithValue("platform", firmPlatformId);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    var codes = Marketplace.Mapping.MarketplaceReadinessService
                        .ParseReasons(r.IsDBNull(2) ? null : r.GetString(2))
                        .Select(x => x.Code).Distinct().ToList();
                    readiness[r.GetGuid(0)] = (r.GetString(1), codes);
                }
            }

            if (caps.PushListing)
            {
                push = new Dictionary<Guid, (int, int, int, int, string?)>();
                const string pushSql = @"
                    SELECT v.""ProductId"",
                           COUNT(*) FILTER (WHERE mp.""SyncStatus"" = 'pending')::int,
                           COUNT(*) FILTER (WHERE mp.""SyncStatus"" = 'synced')::int,
                           COUNT(*) FILTER (WHERE mp.""SyncStatus"" = 'failed')::int,
                           COUNT(*) FILTER (WHERE mp.""SyncStatus"" = 'deactivated')::int,
                           MAX(mp.""LastErrorCode"") FILTER (WHERE mp.""SyncStatus"" = 'failed')
                    FROM integration.marketplace_products mp
                    JOIN catalog.product_variants v ON v.""Id"" = mp.""VariantId""
                    WHERE NOT mp.""IsDeleted"" AND mp.""FirmPlatformId"" = @platform
                    GROUP BY v.""ProductId""";
                await using var cmd = new NpgsqlCommand(pushSql, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("platform", firmPlatformId);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                    push[r.GetGuid(0)] = (r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4),
                                          r.IsDBNull(5) ? null : r.GetString(5));
            }
        }

        return new Snapshot(caps, baseSet, chExcluded, stopped, saleClosed, priceZero, inStock,
            channelPriced, readiness, push);
    }

    private static async Task<string?> GetMarketplaceCodeAsync(NpgsqlConnection conn, Guid firmPlatformId, CancellationToken ct)
    {
        const string sql = @"
            SELECT pt.""Code"" FROM core.core_firm_platforms fp
            JOIN core.core_platform_types pt ON pt.""Id"" = fp.""PlatformTypeId""
            WHERE fp.""Id"" = @id";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", firmPlatformId);
        return (string?)await cmd.ExecuteScalarAsync(ct);
    }


    // ── F3 çekmece: ürünün pazaryeri varyant detayı ───────────────────────────
    public sealed record PushVariantDto(Guid VariantId, string? Sku, string? ExternalId, string SyncStatus,
        string? LastErrorCode, string? LastSyncError, DateTime? LastSyncedAt);
    public sealed record ProductListingDetailDto(
        string Status, List<string> Reasons, bool IsPushChannel, string? MarketplaceCode, List<PushVariantDto> Variants);

    public async Task<ProductListingDetailDto> GetProductDetailAsync(Guid firmPlatformId, Guid productId, CancellationToken ct)
    {
        var status = await ComputeAsync(firmPlatformId, productId, ct);
        var caps = await capabilityResolver.GetAsync(firmPlatformId, ct);
        var variants = new List<PushVariantDto>();
        string? marketplace = null;
        if (caps.PushListing)
        {
            await using var conn = await dataSource.OpenConnectionAsync(ct);
            marketplace = await GetMarketplaceCodeAsync(conn, firmPlatformId, ct);
            const string sql = @"
                SELECT mp.""VariantId"", v.""Sku"", mp.""ExternalId"", mp.""SyncStatus"",
                       mp.""LastErrorCode"", mp.""LastSyncError"", mp.""LastSyncedAt""
                FROM integration.marketplace_products mp
                JOIN catalog.product_variants v ON v.""Id"" = mp.""VariantId""
                WHERE NOT mp.""IsDeleted"" AND mp.""FirmPlatformId"" = @platform AND v.""ProductId"" = @product
                ORDER BY v.""Sku""";
            await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("platform", firmPlatformId);
            cmd.Parameters.AddWithValue("product", productId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                variants.Add(new PushVariantDto(
                    r.GetGuid(0),
                    r.IsDBNull(1) ? null : r.GetString(1),
                    r.IsDBNull(2) ? null : r.GetString(2),
                    r.GetString(3),
                    r.IsDBNull(4) ? null : r.GetString(4),
                    r.IsDBNull(5) ? null : r.GetString(5),
                    r.IsDBNull(6) ? null : r.GetDateTime(6)));
        }
        return new ProductListingDetailDto(status.Status, status.Reasons, caps.PushListing, marketplace, variants);
    }

    /// <summary>Sebep kodu → kullanıcı etiketi (rehber/panel ortak).</summary>
    public static string ReasonLabel(string code) => code switch
    {
        "channel_excluded" => "Kanaldan çıkarıldı",
        "sale_stopped" => "Satış durduruldu",
        "sale_closed" => "Ürün satışa kapalı",
        "out_of_stock" => "Kanal stoğu yok",
        "price_zero" => "Satış fiyatı 0",
        "no_channel_price" => "Bu kanalda fiyatı yok",
        "readiness_unknown" => "Hazırlık hesaplanmadı",
        "push_pending" => "Yükleme bekliyor",
        "deactivated" => "Listeden düşürüldü",
        _ when code.StartsWith("push_failed") => "Yükleme hatası" + (code.Contains(':') ? $" ({code.Split(':', 2)[1]})" : ""),
        "no_category_mapping" => "Kategori eşlemesi yok",
        "pool_assignment_pending" => "Kategori ataması bekliyor",
        "rule_no_match" => "Kategori kuralı tutmadı",
        "broken_mapping" => "Kategori eşlemesi kırık",
        "required_attr_missing" => "Zorunlu özellik eksik",
        "value_unmapped" => "Değer eşlemesiz",
        "attrs_not_synced" => "Kategori özellikleri indirilmedi",
        _ => code
    };
}
