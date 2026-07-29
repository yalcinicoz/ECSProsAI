using System.Text.Json;
using ECSPros.Api.Services.Marketplace.Reference;
using Npgsql;
using NpgsqlTypes;

namespace ECSPros.Api.Services.Marketplace.Mapping;

public sealed record ReadinessReason(string Code, string? Attr = null);

public sealed record RecomputeResult(int Total, int Ready, int Missing);

/// <summary>
/// Yükleme hazırlık denetimi motoru (§3): ürün × pazaryeri için kategori çözer
/// (istisna > kural > birebir; havuz atanmadıysa eksik) ve çözülen kategorinin zorunlu
/// özelliklerini denetler (ürün-özel değer > değer eşlemesi > sabit değer > serbest geçirme).
/// Sonuç integration.marketplace_product_readiness'a materialize edilir — yalnız değişen
/// satıra yazılır. Varyant ekseni özellikler denetim DIŞI (F4 gönderim payload'ının işi).
/// Tümü bellek-içi toplu hesap: 28,5K ürün + eşlemeler tek geçişte.
/// </summary>
public sealed class MarketplaceReadinessService(
    NpgsqlDataSource mainDb,
    MarketplaceRefDb refDb,
    ILogger<MarketplaceReadinessService> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static string ReasonLabel(ReadinessReason r) => r.Code switch
    {
        "no_category_mapping" => "Kategori eşlemesi yok",
        "pool_assignment_pending" => "Kategori ataması bekliyor (havuz)",
        "rule_no_match" => "Hiçbir kategori kuralı tutmadı",
        "broken_mapping" => "Kategori eşlemesi kırık",
        "required_attr_missing" => $"Zorunlu özellik eksik: {r.Attr}",
        "value_unmapped" => $"Değer eşlemesiz: {r.Attr}",
        "attrs_not_synced" => "Kategori özellikleri henüz indirilmedi (Referans Verisi → Özellikler senkronu)",
        _ => r.Code
    };

    public static List<ReadinessReason> ParseReasons(string? json) =>
        string.IsNullOrEmpty(json) ? [] :
        JsonSerializer.Deserialize<List<ReadinessReason>>(json, JsonOpts) ?? [];

    // ── Ana hesap ────────────────────────────────────────────────────────────

    public async Task<RecomputeResult> RecomputeAsync(
        string marketplace, IReadOnlyCollection<Guid>? productIds = null, CancellationToken ct = default)
    {
        await using var conn = await mainDb.OpenConnectionAsync(ct);

        // Özellik tipi kod → id (kural değerlendirmesi için)
        var typeIdByCode = new Dictionary<string, Guid>();
        await using (var cmd = new NpgsqlCommand(
            """SELECT "Code", "Id" FROM definition.attribute_types WHERE NOT "IsDeleted" """, conn))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
                typeIdByCode[r.GetString(0)] = r.GetGuid(1);

        // Grup eşlemeleri
        var mappings = new Dictionary<Guid, (string Kind, string? Target, string? TargetPath,
            string? RulesJson, string Status)>();
        await using (var cmd = new NpgsqlCommand(
            """
            SELECT "ProductGroupId", "MappingKind", "TargetExternalId", "TargetPath", "RulesJson", "Status"
            FROM integration.marketplace_category_mappings
            WHERE "Marketplace"=@m AND "FirmPlatformId" IS NULL AND NOT "IsDeleted"
            """, conn))
        {
            cmd.Parameters.AddWithValue("m", marketplace);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                mappings[r.GetGuid(0)] = (r.GetString(1),
                    r.IsDBNull(2) ? null : r.GetString(2),
                    r.IsDBNull(3) ? null : r.GetString(3),
                    r.IsDBNull(4) ? null : r.GetString(4),
                    r.GetString(5));
        }

        // Ürün istisnaları
        var overrides = new Dictionary<Guid, (string Cat, string Path)>();
        await using (var cmd = new NpgsqlCommand(
            """
            SELECT "ProductId", "CategoryExternalId", "CategoryPath"
            FROM integration.marketplace_product_category_overrides
            WHERE "Marketplace"=@m AND "FirmPlatformId" IS NULL AND NOT "IsDeleted"
            """, conn))
        {
            cmd.Parameters.AddWithValue("m", marketplace);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                overrides[r.GetGuid(0)] = (r.GetString(1), r.GetString(2));
        }

        // Ürünler
        var products = new List<(Guid Id, Guid GroupId)>();
        {
            var sql = """SELECT "Id", "ProductGroupId" FROM catalog.products WHERE NOT "IsDeleted" """;
            if (productIds is { Count: > 0 }) sql += """ AND "Id" = ANY(@pids)""";
            await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 60 };
            if (productIds is { Count: > 0 }) cmd.Parameters.AddWithValue("pids", productIds.ToArray());
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) products.Add((r.GetGuid(0), r.GetGuid(1)));
        }
        if (products.Count == 0) return new RecomputeResult(0, 0, 0);

        // Ürünlerin kendi özellik değerleri: (tip → değer id'leri, tip → serbest metin var mı)
        var ownValues = new Dictionary<Guid, Dictionary<Guid, List<Guid>>>();
        var ownLiterals = new Dictionary<Guid, HashSet<Guid>>();
        {
            var sql = """
                SELECT "ProductId", "AttributeTypeId", "AttributeValueId", ("CustomValue" IS NOT NULL)
                FROM catalog.product_attributes WHERE NOT "IsDeleted"
                """;
            if (productIds is { Count: > 0 }) sql += """ AND "ProductId" = ANY(@pids)""";
            await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 60 };
            if (productIds is { Count: > 0 }) cmd.Parameters.AddWithValue("pids", productIds.ToArray());
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var pid = r.GetGuid(0);
                var tid = r.GetGuid(1);
                if (!r.IsDBNull(2))
                {
                    if (!ownValues.TryGetValue(pid, out var byType)) ownValues[pid] = byType = [];
                    if (!byType.TryGetValue(tid, out var list)) byType[tid] = list = [];
                    list.Add(r.GetGuid(2));
                }
                else if (r.GetBoolean(3))
                {
                    if (!ownLiterals.TryGetValue(pid, out var set)) ownLiterals[pid] = set = [];
                    set.Add(tid);
                }
            }
        }

        // 1. geçiş: kategori çözümü — çözülen kategori kümesini topla
        var resolved = new (string? Cat, string? Path, ReadinessReason? Reason)[products.Count];
        var categorySet = new HashSet<string>();
        for (var i = 0; i < products.Count; i++)
        {
            var (pid, groupId) = products[i];
            resolved[i] = ResolveCategory(pid, groupId, mappings, overrides, ownValues, typeIdByCode);
            if (resolved[i].Cat is not null) categorySet.Add(resolved[i].Cat!);
        }

        // Çözülen kategorilerin zorunlu (varyant-dışı) özellikleri — referans DB.
        // Hiç özellik satırı olmayan kategori "denetlenemedi" sayılır (attrs_not_synced):
        // gereklilikler bilinmeden hazır denilemez — özellik senkronu koşulunca düzelir.
        var requiredAttrs = new Dictionary<string, List<(string AttrId, string Name)>>();
        var attrSyncedCategories = new HashSet<string>();
        var refDs = await refDb.GetAsync(ct);
        if (refDs is not null && categorySet.Count > 0)
        {
            await using (var known = refDs.CreateCommand(
                """
                SELECT DISTINCT category_external_id FROM mp_category_attributes
                WHERE marketplace=$1 AND category_external_id = ANY($2)
                """))
            {
                known.Parameters.AddWithValue(marketplace);
                known.Parameters.AddWithValue(categorySet.ToArray());
                await using var kr = await known.ExecuteReaderAsync(ct);
                while (await kr.ReadAsync(ct)) attrSyncedCategories.Add(kr.GetString(0));
            }

            await using var cmd = refDs.CreateCommand(
                """
                SELECT category_external_id, attribute_external_id, name
                FROM mp_category_attributes
                WHERE marketplace=$1 AND category_external_id = ANY($2)
                  AND is_required AND NOT is_variant_axis AND removed_at IS NULL
                """);
            cmd.Parameters.AddWithValue(marketplace);
            cmd.Parameters.AddWithValue(categorySet.ToArray());
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var cat = r.GetString(0);
                if (!requiredAttrs.TryGetValue(cat, out var list)) requiredAttrs[cat] = list = [];
                list.Add((r.GetString(1), r.GetString(2)));
            }
        }

        // Özellik + değer eşlemeleri (kategori kapsamlı)
        var attrMappings = new Dictionary<(string Cat, string Attr), (string Strategy, Guid? TypeId, string Status)>();
        await using (var cmd = new NpgsqlCommand(
            """
            SELECT "MpCategoryExternalId", "MpAttributeExternalId", "Strategy", "AttributeTypeId", "Status"
            FROM integration.marketplace_attribute_mappings
            WHERE "Marketplace"=@m AND "FirmPlatformId" IS NULL AND NOT "IsDeleted"
            """, conn))
        {
            cmd.Parameters.AddWithValue("m", marketplace);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                attrMappings[(r.GetString(0), r.GetString(1))] =
                    (r.GetString(2), r.IsDBNull(3) ? null : r.GetGuid(3), r.GetString(4));
        }

        var valueMappings = new HashSet<(string Cat, string Attr, Guid ValueId)>();
        await using (var cmd = new NpgsqlCommand(
            """
            SELECT "MpCategoryExternalId", "MpAttributeExternalId", "AttributeValueId"
            FROM integration.marketplace_value_mappings
            WHERE "Marketplace"=@m AND "FirmPlatformId" IS NULL AND NOT "IsDeleted" AND "Status" <> 'broken'
            """, conn))
        {
            cmd.Parameters.AddWithValue("m", marketplace);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                valueMappings.Add((r.GetString(0), r.GetString(1), r.GetGuid(2)));
        }

        // Ürün-özel pazaryeri değerleri
        var productMpValues = new HashSet<(Guid Pid, string Cat, string Attr)>();
        await using (var cmd = new NpgsqlCommand(
            """
            SELECT "ProductId", "MpCategoryExternalId", "MpAttributeExternalId"
            FROM integration.marketplace_product_attribute_values
            WHERE "Marketplace"=@m AND "FirmPlatformId" IS NULL AND NOT "IsDeleted"
            """, conn))
        {
            cmd.Parameters.AddWithValue("m", marketplace);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                productMpValues.Add((r.GetGuid(0), r.GetString(1), r.GetString(2)));
        }

        // 2. geçiş: özellik denetimi + satır üretimi
        var rows = new List<(Guid Pid, string Status, string Reasons, string? Cat, string? Path)>(products.Count);
        int ready = 0, missing = 0;
        for (var i = 0; i < products.Count; i++)
        {
            var (pid, _) = products[i];
            var (cat, path, catReason) = resolved[i];
            var reasons = new List<ReadinessReason>();
            if (catReason is not null) reasons.Add(catReason);

            if (cat is not null && !attrSyncedCategories.Contains(cat))
                reasons.Add(new ReadinessReason("attrs_not_synced"));
            else if (cat is not null && requiredAttrs.TryGetValue(cat, out var reqs))
            {
                var byType = ownValues.GetValueOrDefault(pid);
                var literals = ownLiterals.GetValueOrDefault(pid);
                foreach (var (attrId, attrName) in reqs)
                {
                    if (productMpValues.Contains((pid, cat, attrId))) continue; // ürün-özel değer var

                    if (attrMappings.TryGetValue((cat, attrId), out var am) && am.Status != "broken")
                    {
                        switch (am.Strategy)
                        {
                            case "fixed_value":
                                continue;
                            case "pass_literal":
                                if (am.TypeId is Guid lt &&
                                    (byType?.ContainsKey(lt) == true || literals?.Contains(lt) == true)) continue;
                                reasons.Add(new ReadinessReason("required_attr_missing", attrName));
                                continue;
                            case "map_values":
                                if (am.TypeId is Guid mt && byType?.TryGetValue(mt, out var vals) == true)
                                {
                                    if (vals.All(v => valueMappings.Contains((cat, attrId, v)))) continue;
                                    reasons.Add(new ReadinessReason("value_unmapped", attrName));
                                    continue;
                                }
                                reasons.Add(new ReadinessReason("required_attr_missing", attrName));
                                continue;
                        }
                    }
                    reasons.Add(new ReadinessReason("required_attr_missing", attrName));
                }
            }

            var status = reasons.Count == 0 && cat is not null ? "ready" : "missing_info";
            if (status == "ready") ready++; else missing++;
            rows.Add((pid, status, JsonSerializer.Serialize(reasons, JsonOpts), cat, path));
        }

        await WriteRowsAsync(conn, marketplace, rows, ct);

        logger.LogInformation("Readiness hesaplandı: {Marketplace} — {Total} ürün, {Ready} hazır, {Missing} eksik",
            marketplace, rows.Count, ready, missing);
        return new RecomputeResult(rows.Count, ready, missing);
    }

    private static (string? Cat, string? Path, ReadinessReason? Reason) ResolveCategory(
        Guid productId, Guid groupId,
        Dictionary<Guid, (string Kind, string? Target, string? TargetPath, string? RulesJson, string Status)> mappings,
        Dictionary<Guid, (string Cat, string Path)> overrides,
        Dictionary<Guid, Dictionary<Guid, List<Guid>>> ownValues,
        Dictionary<string, Guid> typeIdByCode)
    {
        // Öncelik: ürün istisnası (K4) — kaynağı ne olursa olsun kazanır
        if (overrides.TryGetValue(productId, out var ov))
            return (ov.Cat, ov.Path, null);

        if (!mappings.TryGetValue(groupId, out var m))
            return (null, null, new ReadinessReason("no_category_mapping"));
        if (m.Status == "broken")
            return (null, null, new ReadinessReason("broken_mapping"));

        switch (m.Kind)
        {
            case "direct":
                return (m.Target, m.TargetPath, null);

            case "rules":
                var rules = string.IsNullOrEmpty(m.RulesJson) ? [] :
                    JsonSerializer.Deserialize<List<MappingRuleDto>>(m.RulesJson, JsonOpts) ?? [];
                var byType = ownValues.GetValueOrDefault(productId);
                foreach (var rule in rules.OrderBy(r => r.Order))
                {
                    if (!typeIdByCode.TryGetValue(rule.AttributeTypeCode, out var typeId)) continue;
                    if (byType?.TryGetValue(typeId, out var vals) == true && vals.Contains(rule.ValueId))
                        return (rule.TargetExternalId, rule.TargetPath, null);
                }
                // hiçbir kural tutmadı → varsayılan hedef, o da yoksa eksik
                return m.Target is not null
                    ? (m.Target, m.TargetPath, null)
                    : (null, null, new ReadinessReason("rule_no_match"));

            case "pool":
                return (null, null, new ReadinessReason("pool_assignment_pending"));

            default:
                return (null, null, new ReadinessReason("no_category_mapping"));
        }
    }

    /// <summary>Yalnız değişen satıra dokunarak upsert eder. Filtered unique index +
    /// nullable FirmPlatformId nedeniyle ON CONFLICT kullanılamaz (NULL'lar çakışmaz);
    /// UPDATE-sonra-INSERT deseni kullanılır.</summary>
    private static async Task WriteRowsAsync(
        NpgsqlConnection conn, string marketplace,
        List<(Guid Pid, string Status, string Reasons, string? Cat, string? Path)> rows, CancellationToken ct)
    {
        await using var tx = await conn.BeginTransactionAsync(ct);

        const string dataExpr = """
            unnest(@pids::uuid[], @statuses::text[], @reasons::text[], @cats::text[], @paths::text[])
                AS d(pid, status, reasons, cat, path)
            """;

        void AddParams(NpgsqlCommand cmd)
        {
            cmd.Parameters.AddWithValue("m", marketplace);
            cmd.Parameters.AddWithValue("pids", rows.Select(x => x.Pid).ToArray());
            cmd.Parameters.AddWithValue("statuses", rows.Select(x => x.Status).ToArray());
            cmd.Parameters.AddWithValue("reasons", rows.Select(x => x.Reasons).ToArray());
            cmd.Parameters.Add(new NpgsqlParameter("cats", NpgsqlDbType.Array | NpgsqlDbType.Text)
                { Value = rows.Select(x => (object?)x.Cat ?? DBNull.Value).ToArray() });
            cmd.Parameters.Add(new NpgsqlParameter("paths", NpgsqlDbType.Array | NpgsqlDbType.Text)
                { Value = rows.Select(x => (object?)x.Path ?? DBNull.Value).ToArray() });
        }

        await using (var upd = new NpgsqlCommand($"""
            UPDATE integration.marketplace_product_readiness r
            SET "Status"=d.status, "ReasonsJson"=d.reasons::jsonb,
                "ResolvedCategoryExternalId"=d.cat, "ResolvedCategoryPath"=d.path,
                "ComputedAt"=now(), "UpdatedAt"=now()
            FROM {dataExpr}
            WHERE r."Marketplace"=@m AND r."FirmPlatformId" IS NULL AND NOT r."IsDeleted"
              AND r."ProductId"=d.pid
              AND (r."Status", r."ReasonsJson"::text, r."ResolvedCategoryExternalId")
                  IS DISTINCT FROM (d.status, d.reasons, d.cat)
            """, conn, tx) { CommandTimeout = 120 })
        {
            AddParams(upd);
            await upd.ExecuteNonQueryAsync(ct);
        }

        await using (var ins = new NpgsqlCommand($"""
            INSERT INTO integration.marketplace_product_readiness
                ("Id", "ProductId", "Marketplace", "FirmPlatformId", "Status", "ReasonsJson",
                 "ResolvedCategoryExternalId", "ResolvedCategoryPath", "ComputedAt", "CreatedAt", "IsDeleted")
            SELECT gen_random_uuid(), d.pid, @m, NULL, d.status, d.reasons::jsonb, d.cat, d.path, now(), now(), false
            FROM {dataExpr}
            WHERE NOT EXISTS (
                SELECT 1 FROM integration.marketplace_product_readiness r
                WHERE r."Marketplace"=@m AND r."FirmPlatformId" IS NULL AND NOT r."IsDeleted"
                  AND r."ProductId"=d.pid)
            """, conn, tx) { CommandTimeout = 120 })
        {
            AddParams(ins);
            await ins.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);

        await using var analyze = new NpgsqlCommand(
            "ANALYZE integration.marketplace_product_readiness", conn);
        await analyze.ExecuteNonQueryAsync(ct);
    }
}
