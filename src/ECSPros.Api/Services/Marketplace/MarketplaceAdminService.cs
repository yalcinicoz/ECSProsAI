using System.Text.Json;
using Npgsql;

namespace ECSPros.Api.Services.Marketplace;

// ── Overview DTO'ları ────────────────────────────────────────────────────────
public sealed record MarketplaceStoreDto(
    Guid Id,
    Guid FirmId,
    string FirmCode,
    Dictionary<string, string> FirmNameI18n,
    Guid PlatformTypeId,
    string PlatformTypeCode,
    Dictionary<string, string> PlatformTypeNameI18n,
    string Code,
    Dictionary<string, string> NameI18n,
    bool IsActive,
    bool HasCredentials,
    Guid? IntegrationId,
    string? ServiceCode,
    int UploadedListings,
    int PendingListings,
    int FailedListings,
    int DeactivatedListings,
    int ToUploadProducts,
    int OpenOrders,
    int TodayOrders,
    DateTime? LastSyncAt,
    int OpenIssues);

public sealed record IssueRowDto(
    Guid Id,
    string IssueType,
    string Title,
    string? Detail,
    string? SuggestedAction,
    string Status,
    DateTime CreatedAt,
    DateTime LastSeenAt);

// Birleşik ürün satırı: kind=listing → pazaryerine gönderilmiş varyant kaydı;
// kind=candidate → kanalda açık ama pazaryerine hiç gönderilmemiş ürün ("yüklenecek").
public sealed record MarketplaceProductRowDto(
    string Kind,
    Guid? MarketplaceProductId,
    Guid? VariantId,
    Guid ProductId,
    string ProductCode,
    string? ProductName,
    string? Sku,
    string? Barcode,
    int VariantCount,
    string? ExternalId,
    string? SyncStatus,
    decimal? MarketplacePrice,
    int? MarketplaceStock,
    DateTime? LastSyncedAt,
    string? LastSyncError,
    // Yükleme hazırlık denetimi (yalnız kind=candidate satırlarında dolar, F3)
    string? ReadinessStatus = null,       // ready | missing_info | null (denetlenmedi)
    List<string>? ReadinessLabels = null,
    // Gönderim reddi sınıflandırması (yalnız kind=listing failed satırlarında dolar, F4)
    string? LastErrorCode = null,
    string? SuggestedCategoryExternalId = null,
    string? SuggestedCategoryPath = null);

public sealed record ReadinessCountsDto(string Marketplace, int Ready, int Missing, int Unchecked);

public sealed record BatchFailedItemDto(
    string Barcode, string? ErrorCode, string? ErrorRaw, string? SuggestedCategoryExternalId);

public sealed record BatchRowDto(
    Guid Id,
    string? ExternalBatchId,
    string BatchType,
    string Status,
    int ItemCount,
    int ResolvedCount,
    int SuccessCount,
    int FailedCount,
    DateTime SubmittedAt,
    DateTime? LastPolledAt,
    DateTime? NextPollAt,
    string? Error,
    List<BatchFailedItemDto> FailedItems);

public sealed record StoreIntegrationInfo(Guid IntegrationId, string ServiceCode);

public sealed record SyncPayloadRow(
    Guid VariantId,
    Guid ProductId,
    string? Barcode,
    string Sku,
    string ProductCode,
    string Title,
    string Description,
    decimal Price);

/// <summary>
/// Pazaryeri yönetim ekranlarının cross-schema okuma katmanı (EffectivePriceProvider kalıbı):
/// mağaza = core_firm_platforms (PlatformType.IsMarketplace), listing = integration.
/// marketplace_products, aday = storefront.channel_products − listing, sipariş = order.ord_orders.
/// Aynı DB'de olduğundan tek tek raw-SQL sorguları; cache yok (yönetim ekranı, güncel sayı beklenir).
/// </summary>
public sealed class MarketplaceAdminService(
    NpgsqlDataSource dataSource,
    Reference.MarketplaceRefDb refDb)
{
    private static Dictionary<string, string> I18n(string? json) =>
        string.IsNullOrEmpty(json)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();

    // F1 kapsam+karar kümesi (docs/satis-kanali-ortak-kurgu.md §3.1, K10): storefront deny-set ile AYNI anlam.
    // Kanal kapsamı filter|mixed → yalnız InScope satırı olan ürünler; all (ya da kapsam yok) → satır yoksa kanalda
    // (opt-out). Her durumda: IsExcluded değil, IsActive (satır varsa) ve durdurma penceresi şu anı kapsamıyor.
    // Kullanım: FROM catalog.products p LEFT JOIN storefront.channel_products cp ON (kanal, ürün, NOT IsDeleted)
    //           LEFT JOIN storefront.channel_scopes sc ON sc.FirmPlatformId = @platform AND NOT sc.IsDeleted
    private const string ChannelVisibleWhere = @"
        (CASE WHEN sc.""FillType"" IN ('filter','mixed')
              THEN cp.""Id"" IS NOT NULL AND cp.""InScope"" AND NOT cp.""IsExcluded""
              ELSE cp.""Id"" IS NULL OR NOT cp.""IsExcluded"" END)
        AND (cp.""Id"" IS NULL OR cp.""IsActive"")
        AND NOT (cp.""SaleStoppedFrom"" IS NOT NULL AND cp.""SaleStoppedFrom"" <= now()
                 AND (cp.""SaleStoppedUntil"" IS NULL OR cp.""SaleStoppedUntil"" >= now()))";

    // Listing'in mağazası: kayıttaki doğrudan bağ, yoksa sözleşmenin platformu.
    private const string ListingPlatformExpr =
        @"COALESCE(mp.""FirmPlatformId"", fpi.""FirmPlatformId"")";

    public async Task<List<MarketplaceStoreDto>> GetOverviewAsync(CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        // 1) Mağazalar + en uygun sözleşme (platforma özel > firma geneli)
        const string storesSql = @"
            SELECT fp.""Id"", fp.""FirmId"", f.""Code"", f.""NameI18n""::text,
                   pt.""Id"", pt.""Code"", pt.""NameI18n""::text,
                   fp.""Code"", fp.""NameI18n""::text, fp.""IsActive"",
                   (fp.""Credentials""::text <> '{}') AS has_creds,
                   i.""IntId"", i.""ServiceCode""
            FROM core.core_firm_platforms fp
            JOIN core.core_platform_types pt ON pt.""Id"" = fp.""PlatformTypeId"" AND pt.""IsMarketplace""
            JOIN core.core_firms f ON f.""Id"" = fp.""FirmId"" AND NOT f.""IsDeleted""
            LEFT JOIN LATERAL (
                SELECT fpi.""Id"" AS ""IntId"", s.""Code"" AS ""ServiceCode""
                FROM core.core_firm_platform_integrations fpi
                JOIN definition.integration_services s
                     ON s.""Id"" = fpi.""IntegrationServiceId"" AND s.""ServiceType"" = 'marketplace'
                WHERE NOT fpi.""IsDeleted"" AND fpi.""IsActive""
                  AND fpi.""FirmId"" = fp.""FirmId""
                  AND (fpi.""FirmPlatformId"" = fp.""Id"" OR fpi.""FirmPlatformId"" IS NULL)
                ORDER BY (fpi.""FirmPlatformId"" IS NOT NULL) DESC, fpi.""CreatedAt"" DESC
                LIMIT 1
            ) i ON TRUE
            WHERE NOT fp.""IsDeleted""
            ORDER BY pt.""Code"", fp.""Code""";

        var stores = new List<(Guid Id, Guid FirmId, string FirmCode, string FirmName, Guid PtId,
            string PtCode, string PtName, string Code, string Name, bool IsActive, bool HasCreds,
            Guid? IntId, string? ServiceCode)>();

        await using (var cmd = new NpgsqlCommand(storesSql, conn) { CommandTimeout = 30 })
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
                stores.Add((r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3),
                    r.GetGuid(4), r.GetString(5), r.GetString(6), r.GetString(7), r.GetString(8),
                    r.GetBoolean(9), r.GetBoolean(10),
                    r.IsDBNull(11) ? null : r.GetGuid(11),
                    r.IsDBNull(12) ? null : r.GetString(12)));
        }

        if (stores.Count == 0) return [];
        var platformIds = stores.Select(s => s.Id).ToArray();

        // 2) Listing sayıları (varyant düzeyi) + son senkron
        const string listingSql = $@"
            SELECT pid,
                   COUNT(*) FILTER (WHERE st = 'synced')::int,
                   COUNT(*) FILTER (WHERE st = 'pending')::int,
                   COUNT(*) FILTER (WHERE st = 'failed')::int,
                   COUNT(*) FILTER (WHERE st = 'deactivated')::int,
                   MAX(ls)
            FROM (
                SELECT {ListingPlatformExpr} AS pid, mp.""SyncStatus"" AS st, mp.""LastSyncedAt"" AS ls
                FROM integration.marketplace_products mp
                LEFT JOIN core.core_firm_platform_integrations fpi ON fpi.""Id"" = mp.""FirmIntegrationId""
                WHERE NOT mp.""IsDeleted""
            ) x
            WHERE pid = ANY(@ids)
            GROUP BY pid";

        var listing = new Dictionary<Guid, (int Ok, int Pend, int Fail, int Deact, DateTime? Last)>();
        await using (var cmd = new NpgsqlCommand(listingSql, conn) { CommandTimeout = 30 })
        {
            cmd.Parameters.AddWithValue("ids", platformIds);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                listing[r.GetGuid(0)] = (r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4),
                    r.IsDBNull(5) ? null : r.GetDateTime(5));
        }

        // 3) Yüklenecek ürünler (ürün düzeyi): kanalda açık, hiç varyantı gönderilmemiş
        const string toUploadSql = @"
            SELECT fp.""Id"", COUNT(*)::int
            FROM core.core_firm_platforms fp
            CROSS JOIN catalog.products p
            LEFT JOIN storefront.channel_products cp ON cp.""FirmPlatformId"" = fp.""Id"" AND cp.""ProductId"" = p.""Id"" AND NOT cp.""IsDeleted""
            LEFT JOIN storefront.channel_scopes sc ON sc.""FirmPlatformId"" = fp.""Id"" AND NOT sc.""IsDeleted""
            WHERE fp.""Id"" = ANY(@ids) AND NOT p.""IsDeleted"" AND p.""IsSaleOpen""
              AND EXISTS (SELECT 1 FROM catalog.product_images img WHERE img.""ProductId"" = p.""Id"" AND NOT img.""IsDeleted"")
              AND " + ChannelVisibleWhere + @"
              AND NOT EXISTS (
                  SELECT 1 FROM integration.marketplace_products mp
                  JOIN catalog.product_variants v ON v.""Id"" = mp.""VariantId""
                  WHERE NOT mp.""IsDeleted"" AND mp.""FirmPlatformId"" = fp.""Id""
                    AND v.""ProductId"" = p.""Id"")
            GROUP BY fp.""Id""";

        var toUpload = new Dictionary<Guid, int>();
        await using (var cmd = new NpgsqlCommand(toUploadSql, conn) { CommandTimeout = 30 })
        {
            cmd.Parameters.AddWithValue("ids", platformIds);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) toUpload[r.GetGuid(0)] = r.GetInt32(1);
        }

        // 4) Sipariş sayıları — "bugün" Türkiye günü (UTC+3)
        var todayStartUtc = DateTime.UtcNow.AddHours(3).Date.AddHours(-3);
        const string ordersSql = @"
            SELECT o.""FirmPlatformId"",
                   COUNT(*) FILTER (WHERE o.""Status"" IN ('pending','confirmed','processing'))::int,
                   COUNT(*) FILTER (WHERE o.""CreatedAt"" >= @today)::int
            FROM ""order"".ord_orders o
            WHERE NOT o.""IsDeleted"" AND o.""FirmPlatformId"" = ANY(@ids)
            GROUP BY o.""FirmPlatformId""";

        var orders = new Dictionary<Guid, (int Open, int Today)>();
        await using (var cmd = new NpgsqlCommand(ordersSql, conn) { CommandTimeout = 30 })
        {
            cmd.Parameters.AddWithValue("ids", platformIds);
            cmd.Parameters.AddWithValue("today", todayStartUtc);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) orders[r.GetGuid(0)] = (r.GetInt32(1), r.GetInt32(2));
        }

        // 5) Son pazaryeri log zamanı (sipariş çekme / stok güncelleme de senkron sayılır)
        const string lastLogSql = @"
            SELECT fpi.""FirmPlatformId"", MAX(l.""CreatedAt"")
            FROM integration.integration_logs l
            JOIN core.core_firm_platform_integrations fpi ON fpi.""Id"" = l.""FirmIntegrationId""
            WHERE NOT l.""IsDeleted"" AND l.""ServiceType"" = 'marketplace'
              AND fpi.""FirmPlatformId"" = ANY(@ids)
            GROUP BY fpi.""FirmPlatformId""";

        var lastLog = new Dictionary<Guid, DateTime>();
        await using (var cmd = new NpgsqlCommand(lastLogSql, conn) { CommandTimeout = 30 })
        {
            cmd.Parameters.AddWithValue("ids", platformIds);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) lastLog[r.GetGuid(0)] = r.GetDateTime(1);
        }

        // 6) Açık sorun sayıları (F5 kuyruğu — sağlık şeridini besler)
        var openIssues = new Dictionary<Guid, int>();
        await using (var cmd = new NpgsqlCommand(@"
            SELECT ""FirmPlatformId"", COUNT(*)::int FROM integration.marketplace_issues
            WHERE ""FirmPlatformId"" = ANY(@ids) AND ""Status"" = 'open' AND NOT ""IsDeleted""
            GROUP BY ""FirmPlatformId""", conn) { CommandTimeout = 30 })
        {
            cmd.Parameters.AddWithValue("ids", platformIds);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) openIssues[r.GetGuid(0)] = r.GetInt32(1);
        }

        return stores.Select(s =>
        {
            var l = listing.GetValueOrDefault(s.Id);
            var o = orders.GetValueOrDefault(s.Id);
            DateTime? lastSync = l.Last;
            if (lastLog.TryGetValue(s.Id, out var ll) && (lastSync is null || ll > lastSync)) lastSync = ll;
            return new MarketplaceStoreDto(
                s.Id, s.FirmId, s.FirmCode, I18n(s.FirmName),
                s.PtId, s.PtCode, I18n(s.PtName),
                s.Code, I18n(s.Name), s.IsActive, s.HasCreds,
                s.IntId, s.ServiceCode,
                l.Ok, l.Pend, l.Fail, l.Deact,
                toUpload.GetValueOrDefault(s.Id),
                o.Open, o.Today, lastSync,
                openIssues.GetValueOrDefault(s.Id));
        }).ToList();
    }

    /// <summary>Mağazanın sorunları (F5) — varsayılan yalnız açıklar.</summary>
    public async Task<List<IssueRowDto>> GetIssuesAsync(
        Guid firmPlatformId, string status, int limit, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var sql = @"
            SELECT ""Id"", ""IssueType"", ""Title"", ""Detail"", ""SuggestedAction"", ""Status"",
                   ""CreatedAt"", ""LastSeenAt""
            FROM integration.marketplace_issues
            WHERE ""FirmPlatformId"" = @platform AND NOT ""IsDeleted""" +
            (status == "all" ? "" : @" AND ""Status"" = @status") + @"
            ORDER BY ""LastSeenAt"" DESC LIMIT @limit";
        await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 30 };
        cmd.Parameters.AddWithValue("platform", firmPlatformId);
        if (status != "all") cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("limit", limit);
        var rows = new List<IssueRowDto>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            rows.Add(new IssueRowDto(
                r.GetGuid(0), r.GetString(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.GetString(5), r.GetDateTime(6), r.GetDateTime(7)));
        return rows;
    }

    /// <summary>Mağazanın ürünleri — status: to_upload | synced | pending | failed | deactivated | null (tüm gönderilmişler).</summary>
    public async Task<(List<MarketplaceProductRowDto> Items, int TotalCount)> GetProductsAsync(
        Guid firmPlatformId, string? status, string? search, int page, int pageSize, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var skip = (Math.Max(page, 1) - 1) * pageSize;
        var items = new List<MarketplaceProductRowDto>();
        var total = 0;

        if (status is "to_upload" or "to_upload_ready" or "to_upload_missing")
        {
            // Denetim (readiness) firma geneli, pazaryeri koduyla tutulur — mağazanın kodunu çöz.
            var marketplace = await GetMarketplaceCodeAsync(conn, firmPlatformId, ct);

            var readinessFilter = status switch
            {
                "to_upload_ready" => @" AND rd.""Status"" = 'ready'",
                "to_upload_missing" => @" AND (rd.""Status"" IS NULL OR rd.""Status"" <> 'ready')",
                _ => ""
            };

            var sql = @"
                SELECT p.""Id"", p.""Code"", p.""NameI18n""->>'tr',
                       COUNT(v.""Id"") FILTER (WHERE v.""IsActive"" AND NOT v.""IsDeleted"")::int,
                       COUNT(*) OVER()::int,
                       rd.""Status"", rd.""ReasonsJson""::text
                FROM catalog.products p
                LEFT JOIN storefront.channel_products cp ON cp.""FirmPlatformId"" = @platform AND cp.""ProductId"" = p.""Id"" AND NOT cp.""IsDeleted""
                LEFT JOIN storefront.channel_scopes sc ON sc.""FirmPlatformId"" = @platform AND NOT sc.""IsDeleted""
                LEFT JOIN catalog.product_variants v ON v.""ProductId"" = p.""Id""
                LEFT JOIN integration.marketplace_product_readiness rd
                       ON rd.""Marketplace"" = @mp AND rd.""ProductId"" = p.""Id""
                      AND rd.""FirmPlatformId"" IS NULL AND NOT rd.""IsDeleted""
                WHERE NOT p.""IsDeleted"" AND p.""IsSaleOpen""
                  AND EXISTS (SELECT 1 FROM catalog.product_images img WHERE img.""ProductId"" = p.""Id"" AND NOT img.""IsDeleted"")
                  AND " + ChannelVisibleWhere + @"
                  AND NOT EXISTS (
                      SELECT 1 FROM integration.marketplace_products mp
                      JOIN catalog.product_variants v2 ON v2.""Id"" = mp.""VariantId""
                      WHERE NOT mp.""IsDeleted"" AND mp.""FirmPlatformId"" = @platform
                        AND v2.""ProductId"" = p.""Id"")"
                + readinessFilter
                + (string.IsNullOrWhiteSpace(search) ? "" :
                    @" AND (p.""Code"" ILIKE @s OR p.""NameI18n""->>'tr' ILIKE @s)") + @"
                GROUP BY p.""Id"", p.""Code"", p.""NameI18n"", rd.""Status"", rd.""ReasonsJson""
                ORDER BY p.""Code""
                LIMIT @take OFFSET @skip";

            await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("platform", firmPlatformId);
            cmd.Parameters.AddWithValue("mp", marketplace ?? "");
            cmd.Parameters.AddWithValue("take", pageSize);
            cmd.Parameters.AddWithValue("skip", skip);
            if (!string.IsNullOrWhiteSpace(search))
                cmd.Parameters.AddWithValue("s", $"%{search.Trim()}%");

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                total = r.GetInt32(4);
                var readinessStatus = r.IsDBNull(5) ? null : r.GetString(5);
                List<string>? labels = null;
                if (readinessStatus is null)
                    labels = ["Henüz denetlenmedi"];
                else if (readinessStatus != "ready")
                    labels = Mapping.MarketplaceReadinessService
                        .ParseReasons(r.IsDBNull(6) ? null : r.GetString(6))
                        .Select(Mapping.MarketplaceReadinessService.ReasonLabel).ToList();
                items.Add(new MarketplaceProductRowDto(
                    "candidate", null, null,
                    r.GetGuid(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2),
                    null, null, r.GetInt32(3),
                    null, null, null, null, null, null,
                    readinessStatus, labels));
            }
        }
        else
        {
            var sql = $@"
                SELECT mp.""Id"", mp.""VariantId"", v.""Sku"", v.""Barcode"",
                       p.""Id"", p.""Code"", p.""NameI18n""->>'tr',
                       mp.""ExternalId"", mp.""SyncStatus"", mp.""MarketplacePrice"", mp.""MarketplaceStock"",
                       mp.""LastSyncedAt"", mp.""LastSyncError"",
                       COUNT(*) OVER()::int,
                       mp.""LastErrorCode"", mp.""SuggestedCategoryExternalId""
                FROM integration.marketplace_products mp
                LEFT JOIN core.core_firm_platform_integrations fpi ON fpi.""Id"" = mp.""FirmIntegrationId""
                JOIN catalog.product_variants v ON v.""Id"" = mp.""VariantId""
                JOIN catalog.products p ON p.""Id"" = v.""ProductId""
                WHERE NOT mp.""IsDeleted"" AND {ListingPlatformExpr} = @platform"
                + (string.IsNullOrWhiteSpace(status) ? "" : @" AND mp.""SyncStatus"" = @st")
                + (string.IsNullOrWhiteSpace(search) ? "" :
                    @" AND (p.""Code"" ILIKE @s OR p.""NameI18n""->>'tr' ILIKE @s
                            OR v.""Sku"" ILIKE @s OR v.""Barcode"" ILIKE @s)") + @"
                ORDER BY mp.""LastSyncedAt"" DESC NULLS LAST, p.""Code""
                LIMIT @take OFFSET @skip";

            await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("platform", firmPlatformId);
            cmd.Parameters.AddWithValue("take", pageSize);
            cmd.Parameters.AddWithValue("skip", skip);
            if (!string.IsNullOrWhiteSpace(status)) cmd.Parameters.AddWithValue("st", status);
            if (!string.IsNullOrWhiteSpace(search))
                cmd.Parameters.AddWithValue("s", $"%{search.Trim()}%");

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                total = r.GetInt32(13);
                items.Add(new MarketplaceProductRowDto(
                    "listing", r.GetGuid(0), r.GetGuid(1),
                    r.GetGuid(4), r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6),
                    r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), 1,
                    r.GetString(7), r.GetString(8),
                    r.IsDBNull(9) ? null : r.GetDecimal(9),
                    r.IsDBNull(10) ? null : r.GetInt32(10),
                    r.IsDBNull(11) ? null : r.GetDateTime(11),
                    r.IsDBNull(12) ? null : r.GetString(12),
                    LastErrorCode: r.IsDBNull(14) ? null : r.GetString(14),
                    SuggestedCategoryExternalId: r.IsDBNull(15) ? null : r.GetString(15)));
            }

            items = await EnrichSuggestedCategoriesAsync(items, firmPlatformId, ct);
        }

        return (items, total);
    }

    /// <summary>Redde düşen satırların önerilen kategorilerine path ekler (referans DB'den) —
    /// "istisna oluştur + yeniden gönder" aksiyonu ad/path ister (K3 snapshot kuralı).</summary>
    private async Task<List<MarketplaceProductRowDto>> EnrichSuggestedCategoriesAsync(
        List<MarketplaceProductRowDto> items, Guid firmPlatformId, CancellationToken ct)
    {
        var suggestedIds = items
            .Where(i => i.SuggestedCategoryExternalId is not null)
            .Select(i => i.SuggestedCategoryExternalId!)
            .Distinct().ToArray();
        if (suggestedIds.Length == 0) return items;

        var ds = await refDb.GetAsync(ct);
        if (ds is null) return items;
        var marketplace = await GetMarketplaceCodeAsync(firmPlatformId, ct);
        if (marketplace is null) return items;

        var paths = new Dictionary<string, string>();
        await using (var cmd = ds.CreateCommand(
            "SELECT external_id, path FROM mp_categories WHERE marketplace=$1 AND external_id = ANY($2)"))
        {
            cmd.Parameters.AddWithValue(marketplace);
            cmd.Parameters.AddWithValue(suggestedIds);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) paths[r.GetString(0)] = r.GetString(1);
        }
        return items.Select(i => i.SuggestedCategoryExternalId is string sid && paths.TryGetValue(sid, out var p)
            ? i with { SuggestedCategoryPath = p }
            : i).ToList();
    }

    /// <summary>Mağazanın senkron kullanacağı sözleşme: platforma özel aktif > firma geneli aktif.</summary>
    public async Task<StoreIntegrationInfo?> ResolveIntegrationAsync(Guid firmPlatformId, CancellationToken ct)
    {
        const string sql = @"
            SELECT fpi.""Id"", s.""Code""
            FROM core.core_firm_platforms fp
            JOIN core.core_firm_platform_integrations fpi
                 ON fpi.""FirmId"" = fp.""FirmId"" AND NOT fpi.""IsDeleted"" AND fpi.""IsActive""
                AND (fpi.""FirmPlatformId"" = fp.""Id"" OR fpi.""FirmPlatformId"" IS NULL)
            JOIN definition.integration_services s
                 ON s.""Id"" = fpi.""IntegrationServiceId"" AND s.""ServiceType"" = 'marketplace'
            WHERE fp.""Id"" = @platform AND NOT fp.""IsDeleted""
            ORDER BY (fpi.""FirmPlatformId"" IS NOT NULL) DESC, fpi.""CreatedAt"" DESC
            LIMIT 1";

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 30 };
        cmd.Parameters.AddWithValue("platform", firmPlatformId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new StoreIntegrationInfo(r.GetGuid(0), r.GetString(1));
    }

    /// <summary>Mağazayla ilişkili tüm pazaryeri sözleşme Id'leri (log filtresi için).</summary>
    public async Task<List<Guid>> GetIntegrationIdsAsync(Guid firmPlatformId, CancellationToken ct)
    {
        const string sql = @"
            SELECT fpi.""Id""
            FROM core.core_firm_platforms fp
            JOIN core.core_firm_platform_integrations fpi
                 ON fpi.""FirmId"" = fp.""FirmId"" AND NOT fpi.""IsDeleted""
                AND (fpi.""FirmPlatformId"" = fp.""Id"" OR fpi.""FirmPlatformId"" IS NULL)
            JOIN definition.integration_services s
                 ON s.""Id"" = fpi.""IntegrationServiceId"" AND s.""ServiceType"" = 'marketplace'
            WHERE fp.""Id"" = @platform AND NOT fp.""IsDeleted""";

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 30 };
        cmd.Parameters.AddWithValue("platform", firmPlatformId);
        var ids = new List<Guid>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) ids.Add(r.GetGuid(0));
        return ids;
    }

    /// <summary>
    /// Gönderim payload'ı için varyant verisi. Fiyat önceliği kart gösterimiyle aynı:
    /// kanal fiyatı > varyant BasePrice > ürün BasePrice (0'lar atlanır).
    /// variantIds VEYA productIds verilir; productIds ürünün aktif varyantlarına açılır.
    /// </summary>
    public async Task<List<SyncPayloadRow>> GetSyncPayloadsAsync(
        Guid firmPlatformId, IReadOnlyCollection<Guid>? variantIds, IReadOnlyCollection<Guid>? productIds,
        CancellationToken ct)
    {
        var byVariant = variantIds is { Count: > 0 };
        var sql = @"
            SELECT v.""Id"", v.""ProductId"", v.""Barcode"", v.""Sku"", p.""Code"",
                   COALESCE(p.""NameI18n""->>'tr', p.""Code""),
                   COALESCE(p.""ShortDescriptionI18n""->>'tr', p.""NameI18n""->>'tr', p.""Code""),
                   COALESCE(NULLIF(cv.""Price"", 0), NULLIF(v.""BasePrice"", 0), p.""BasePrice"")
            FROM catalog.product_variants v
            JOIN catalog.products p ON p.""Id"" = v.""ProductId"" AND NOT p.""IsDeleted""
            LEFT JOIN storefront.channel_variants cv
                   ON cv.""VariantId"" = v.""Id"" AND cv.""FirmPlatformId"" = @platform
                  AND cv.""IsActive"" AND NOT cv.""IsDeleted""
            WHERE NOT v.""IsDeleted"" AND v.""IsActive"" AND "
            + (byVariant ? @"v.""Id"" = ANY(@ids)" : @"v.""ProductId"" = ANY(@ids)") + @"
            ORDER BY p.""Code"", v.""Sku""";

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 30 };
        cmd.Parameters.AddWithValue("platform", firmPlatformId);
        cmd.Parameters.AddWithValue("ids", (byVariant ? variantIds! : productIds!).ToArray());

        var rows = new List<SyncPayloadRow>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            rows.Add(new SyncPayloadRow(
                r.GetGuid(0), r.GetGuid(1),
                r.IsDBNull(2) ? null : r.GetString(2),
                r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6),
                r.IsDBNull(7) ? 0 : r.GetDecimal(7)));
        return rows;
    }

    /// <summary>Online satılabilir serbest stok (InStockProductProvider formülü, varyant bazında toplam).</summary>
    public async Task<Dictionary<Guid, int>> GetSellableStocksAsync(
        IReadOnlyCollection<Guid> variantIds, CancellationToken ct)
    {
        if (variantIds.Count == 0) return [];
        const string sql = @"
            SELECT s.""VariantId"", GREATEST(SUM(s.""Quantity"" - s.""ReservedQuantity""), 0)::int
            FROM inventory.inv_stocks s
            JOIN inventory.inv_warehouse_sections sec ON sec.""Id"" = s.""SectionId""
            JOIN inventory.inv_warehouses w ON w.""Id"" = s.""WarehouseId""
            WHERE s.""BinId"" IS NOT NULL AND sec.""IsSellableOnline"" AND w.""IsActive""
              AND s.""VariantId"" = ANY(@ids)
            GROUP BY s.""VariantId""";

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 30 };
        cmd.Parameters.AddWithValue("ids", variantIds.ToArray());
        var map = new Dictionary<Guid, int>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) map[r.GetGuid(0)] = r.GetInt32(1);
        return map;
    }

    /// <summary>Mağazanın senkronlanmış varyant Id listesi (toplu stok güncelleme hedefi).</summary>
    public async Task<List<Guid>> GetSyncedVariantIdsAsync(Guid firmPlatformId, CancellationToken ct)
    {
        const string sql = $@"
            SELECT mp.""VariantId""
            FROM integration.marketplace_products mp
            LEFT JOIN core.core_firm_platform_integrations fpi ON fpi.""Id"" = mp.""FirmIntegrationId""
            WHERE NOT mp.""IsDeleted"" AND mp.""SyncStatus"" = 'synced'
              AND {ListingPlatformExpr} = @platform";

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 30 };
        cmd.Parameters.AddWithValue("platform", firmPlatformId);
        var ids = new List<Guid>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) ids.Add(r.GetGuid(0));
        return ids;
    }

    /// <summary>Mağazanın pazaryeri kodu (platform tipi kodu) — readiness/eşleme anahtarı.</summary>
    public async Task<string?> GetMarketplaceCodeAsync(Guid firmPlatformId, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return await GetMarketplaceCodeAsync(conn, firmPlatformId, ct);
    }

    private static async Task<string?> GetMarketplaceCodeAsync(
        NpgsqlConnection conn, Guid firmPlatformId, CancellationToken ct)
    {
        const string sql = @"
            SELECT pt.""Code"" FROM core.core_firm_platforms fp
            JOIN core.core_platform_types pt ON pt.""Id"" = fp.""PlatformTypeId""
            WHERE fp.""Id"" = @id";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", firmPlatformId);
        return (string?)await cmd.ExecuteScalarAsync(ct);
    }

    /// <summary>Mağazanın gönderim paketleri + hatalı satır özetleri (F4 — Senkron Geçmişi).</summary>
    public async Task<List<BatchRowDto>> GetBatchesAsync(Guid firmPlatformId, int limit, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var batches = new List<BatchRowDto>();
        await using (var cmd = new NpgsqlCommand(@"
            SELECT ""Id"", ""ExternalBatchId"", ""BatchType"", ""Status"", ""ItemCount"", ""ResolvedCount"",
                   ""SuccessCount"", ""FailedCount"", ""SubmittedAt"", ""LastPolledAt"", ""NextPollAt"", ""Error""
            FROM integration.marketplace_batches
            WHERE ""FirmPlatformId"" = @platform AND NOT ""IsDeleted""
            ORDER BY ""SubmittedAt"" DESC LIMIT @limit", conn))
        {
            cmd.Parameters.AddWithValue("platform", firmPlatformId);
            cmd.Parameters.AddWithValue("limit", limit);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                batches.Add(new BatchRowDto(
                    r.GetGuid(0),
                    r.IsDBNull(1) ? null : r.GetString(1),
                    r.GetString(2), r.GetString(3),
                    r.GetInt32(4), r.GetInt32(5), r.GetInt32(6), r.GetInt32(7),
                    r.GetDateTime(8),
                    r.IsDBNull(9) ? null : r.GetDateTime(9),
                    r.IsDBNull(10) ? null : r.GetDateTime(10),
                    r.IsDBNull(11) ? null : r.GetString(11),
                    []));
        }
        if (batches.Count == 0) return batches;

        var failedByBatch = new Dictionary<Guid, List<BatchFailedItemDto>>();
        await using (var cmd = new NpgsqlCommand(@"
            SELECT ""BatchId"", ""Barcode"", ""ErrorCode"", ""ErrorRaw"", ""SuggestedCategoryExternalId""
            FROM integration.marketplace_batch_items
            WHERE ""BatchId"" = ANY(@ids) AND ""Status"" = 'failed' AND NOT ""IsDeleted""
            ORDER BY ""Barcode""", conn))
        {
            cmd.Parameters.AddWithValue("ids", batches.Select(b => b.Id).ToArray());
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var bid = r.GetGuid(0);
                if (!failedByBatch.TryGetValue(bid, out var list)) failedByBatch[bid] = list = [];
                if (list.Count < 20)
                    list.Add(new BatchFailedItemDto(
                        r.GetString(1),
                        r.IsDBNull(2) ? null : r.GetString(2),
                        r.IsDBNull(3) ? null : r.GetString(3),
                        r.IsDBNull(4) ? null : r.GetString(4)));
            }
        }
        return batches
            .Select(b => b with { FailedItems = failedByBatch.GetValueOrDefault(b.Id, []) })
            .ToList();
    }

    /// <summary>Mağazanın yüklenecek adayları için denetim sayıları (Hazır/Eksik çipleri, F3).</summary>
    public async Task<ReadinessCountsDto?> GetReadinessCountsAsync(Guid firmPlatformId, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var marketplace = await GetMarketplaceCodeAsync(conn, firmPlatformId, ct);
        if (marketplace is null) return null;

        var sql = @"
            SELECT COUNT(*) FILTER (WHERE rd.""Status"" = 'ready')::int,
                   COUNT(*) FILTER (WHERE rd.""Status"" IS NOT NULL AND rd.""Status"" <> 'ready')::int,
                   COUNT(*) FILTER (WHERE rd.""Status"" IS NULL)::int
            FROM catalog.products p
            LEFT JOIN storefront.channel_products cp ON cp.""FirmPlatformId"" = @platform AND cp.""ProductId"" = p.""Id"" AND NOT cp.""IsDeleted""
            LEFT JOIN storefront.channel_scopes sc ON sc.""FirmPlatformId"" = @platform AND NOT sc.""IsDeleted""
            LEFT JOIN integration.marketplace_product_readiness rd
                   ON rd.""Marketplace"" = @mp AND rd.""ProductId"" = p.""Id""
                  AND rd.""FirmPlatformId"" IS NULL AND NOT rd.""IsDeleted""
            WHERE NOT p.""IsDeleted"" AND p.""IsSaleOpen""
              AND EXISTS (SELECT 1 FROM catalog.product_images img WHERE img.""ProductId"" = p.""Id"" AND NOT img.""IsDeleted"")
              AND " + ChannelVisibleWhere + @"
              AND NOT EXISTS (
                  SELECT 1 FROM integration.marketplace_products mp
                  JOIN catalog.product_variants v2 ON v2.""Id"" = mp.""VariantId""
                  WHERE NOT mp.""IsDeleted"" AND mp.""FirmPlatformId"" = @platform
                    AND v2.""ProductId"" = p.""Id"")";

        await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 30 };
        cmd.Parameters.AddWithValue("platform", firmPlatformId);
        cmd.Parameters.AddWithValue("mp", marketplace);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        return new ReadinessCountsDto(marketplace, r.GetInt32(0), r.GetInt32(1), r.GetInt32(2));
    }
}
