using System.Text.Json;
using ECSPros.Api.Services.Marketplace.Reference;
using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECSPros.Api.Services.Marketplace.Mapping;

/// <summary>
/// Eşleme katmanı servis yüzeyi (§2): bizim taraf = definition.product_groups (yalnız OKUNUR —
/// definition altın kuralı) + definition.attribute_types/values; pazaryeri taraf = marketplace_ref
/// (ayrı DB, external id + snapshot köprüsü); eşleme kayıtları = integration şeması (EF).
/// </summary>
public sealed class MarketplaceMappingService(
    NpgsqlDataSource mainDb,
    MarketplaceRefDb refDb,
    IIntegrationDbContext db)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ── Kategori eşleme ──────────────────────────────────────────────────────

    public async Task<MappingOverviewDto> GetOverviewAsync(string marketplace, CancellationToken ct)
    {
        var groups = new List<(Guid Id, string Code, string Name, int ProductCount)>();
        await using (var cmd = mainDb.CreateCommand(
            """
            SELECT g."Id", g."Code", COALESCE(g."NameI18n"->>'tr', g."Code"),
                   (SELECT count(*) FROM catalog.products p
                    WHERE p."ProductGroupId" = g."Id" AND NOT p."IsDeleted")::int
            FROM definition.product_groups g
            WHERE NOT g."IsDeleted" AND g."IsActive"
            ORDER BY COALESCE(g."NameI18n"->>'tr', g."Code") COLLATE "tr-TR-x-icu"
            """))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                groups.Add((reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3)));

        var mappings = await db.MarketplaceCategoryMappings
            .Where(m => m.Marketplace == marketplace && m.FirmPlatformId == null)
            .ToListAsync(ct);
        var byGroup = mappings.ToDictionary(m => m.ProductGroupId, ToDto);

        var reviewCount =
            await db.MarketplaceCategoryMappings.CountAsync(m => m.Marketplace == marketplace && m.Status != "active", ct) +
            await db.MarketplaceAttributeMappings.CountAsync(m => m.Marketplace == marketplace && m.Status != "active", ct) +
            await db.MarketplaceValueMappings.CountAsync(m => m.Marketplace == marketplace && m.Status != "active", ct);

        var rows = groups.Select(g => new GroupRowDto(
            g.Id, g.Code, g.Name, g.ProductCount, byGroup.GetValueOrDefault(g.Id))).ToList();
        return new MappingOverviewDto(
            rows,
            MappedCount: rows.Count(r => r.Mapping is not null),
            UnmappedCount: rows.Count(r => r.Mapping is null),
            ReviewCount: reviewCount);
    }

    private static CategoryMappingDto ToDto(MarketplaceCategoryMapping m) => new(
        m.Id, m.MappingKind, m.TargetExternalId, m.TargetName, m.TargetPath,
        m.RulesJson is null ? [] : JsonSerializer.Deserialize<List<MappingRuleDto>>(m.RulesJson, JsonOpts) ?? [],
        m.PoolJson is null ? [] : JsonSerializer.Deserialize<List<PoolTargetDto>>(m.PoolJson, JsonOpts) ?? [],
        m.Status, m.StatusNote);

    public async Task<List<MpCategoryDto>> SearchMpCategoriesAsync(
        string marketplace, string query, int limit, CancellationToken ct)
    {
        var ds = await refDb.GetAsync(ct);
        if (ds is null) return [];

        await using var cmd = ds.CreateCommand(
            """
            SELECT external_id, name, path FROM mp_categories
            WHERE marketplace=$1 AND is_leaf AND removed_at IS NULL AND path ILIKE $2
            ORDER BY length(path) LIMIT $3
            """);
        cmd.Parameters.AddWithValue(marketplace);
        cmd.Parameters.AddWithValue($"%{query}%");
        cmd.Parameters.AddWithValue(limit);
        var result = new List<MpCategoryDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new MpCategoryDto(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return result;
    }

    public async Task<List<CategorySuggestionDto>> SuggestCategoriesAsync(
        string marketplace, Guid productGroupId, CancellationToken ct)
    {
        string? groupName = null;
        await using (var cmd = mainDb.CreateCommand(
            """SELECT COALESCE("NameI18n"->>'tr', "Code") FROM definition.product_groups WHERE "Id"=$1"""))
        {
            cmd.Parameters.AddWithValue(productGroupId);
            groupName = (string?)await cmd.ExecuteScalarAsync(ct);
        }
        if (string.IsNullOrWhiteSpace(groupName)) return [];

        var candidates = await LoadLeafCategoriesAsync(marketplace, ct);
        return OnerileriSkorla(groupName, candidates, 5);
    }

    /// <summary>Skor = kendi adıyla benzerlik YA DA üst kategori adlarından en iyisi (×0.85):
    /// "Spor Ayakkabı" grubu, adı hiç benzemese de "... > Spor Ayakkabı > Sneaker" gibi
    /// o dalın altındaki yaprakları aday göstermeli (bot/bağcıklı bot vakasının tersi yönü).</summary>
    private static List<CategorySuggestionDto> OnerileriSkorla(
        string groupName, List<MpCategoryDto> candidates, int adet)
        => candidates
            .Select(c =>
            {
                var nameScore = TextSimilarity.Score(groupName, c.Name);
                var segScore = c.Path.Split(" > ")[..^1]
                    .Select(seg => (int)Math.Round(TextSimilarity.Score(groupName, seg) * 0.85))
                    .DefaultIfEmpty(0).Max();
                return (c, Score: Math.Max(nameScore, segScore));
            })
            .Where(x => x.Score >= 40)
            .OrderByDescending(x => x.Score).ThenBy(x => x.c.Path.Length)
            .Take(adet)
            .Select(x => new CategorySuggestionDto(x.c.ExternalId, x.c.Name, x.c.Path, x.Score))
            .ToList();

    /// <summary>RF4 (2026-09-01): eşleme kampanyası — aktif eşlemesi OLMAYAN tüm grupların
    /// öneri listesi tek çağrıda (yaprak kategoriler bir kez yüklenir; grup başına ilk 3).
    /// Önerisiz gruplar da döner (skor eşiğini geçen aday yoksa boş liste — elle eşlenir).</summary>
    public async Task<List<GroupSuggestionRowDto>> SuggestAllAsync(string marketplace, CancellationToken ct)
    {
        var overview = await GetOverviewAsync(marketplace, ct);
        var essizler = overview.Groups
            .Where(g => g.Mapping is null || g.Mapping.Status != "active")
            .OrderByDescending(g => g.ProductCount)
            .ToList();
        if (essizler.Count == 0) return [];

        var candidates = await LoadLeafCategoriesAsync(marketplace, ct);
        return essizler
            .Select(g => new GroupSuggestionRowDto(
                g.ProductGroupId, g.Code, g.Name, g.ProductCount,
                OnerileriSkorla(g.Name, candidates, 3)))
            .ToList();
    }

    /// <summary>RF4: toplu birebir (direct) kategori eşleme — her öğe mevcut tekil kayıt
    /// yolundan geçer (doğrulama/audit aynı); hedef ad/yol referans DB'den çözülür.
    /// Kısmi hata toplu işi durdurmaz; hatalar öğe bazında raporlanır.</summary>
    public async Task<BulkCategoryMappingResult> BulkSaveCategoryMappingsAsync(
        string marketplace, List<BulkCategoryMappingItem> items, Guid? userId, CancellationToken ct)
    {
        var kategoriler = (await LoadLeafCategoriesAsync(marketplace, ct))
            .ToDictionary(c => c.ExternalId, c => c);
        int saved = 0, failed = 0;
        var errors = new List<string>();
        foreach (var item in items)
        {
            if (!kategoriler.TryGetValue(item.TargetExternalId, out var hedef))
            {
                failed++;
                errors.Add($"{item.ProductGroupId}: hedef kategori bulunamadı ({item.TargetExternalId}).");
                continue;
            }
            var (dto, error) = await SaveCategoryMappingAsync(new SaveCategoryMappingRequest(
                marketplace, item.ProductGroupId, "direct",
                hedef.ExternalId, hedef.Name, hedef.Path, null, null), userId, ct);
            if (dto is null) { failed++; errors.Add($"{item.ProductGroupId}: {error}"); }
            else saved++;
        }
        return new BulkCategoryMappingResult(saved, failed, errors.Take(20).ToList());
    }

    private async Task<List<MpCategoryDto>> LoadLeafCategoriesAsync(string marketplace, CancellationToken ct)
    {
        var ds = await refDb.GetAsync(ct);
        if (ds is null) return [];
        await using var cmd = ds.CreateCommand(
            "SELECT external_id, name, path FROM mp_categories WHERE marketplace=$1 AND is_leaf AND removed_at IS NULL");
        cmd.Parameters.AddWithValue(marketplace);
        var result = new List<MpCategoryDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new MpCategoryDto(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return result;
    }

    public async Task<(CategoryMappingDto? Dto, string? Error)> SaveCategoryMappingAsync(
        SaveCategoryMappingRequest req, Guid? userId, CancellationToken ct)
    {
        if (req.MappingKind is not ("direct" or "rules" or "pool"))
            return (null, "mappingKind direct | rules | pool olmalı.");
        if (req.MappingKind == "direct" && string.IsNullOrWhiteSpace(req.TargetExternalId))
            return (null, "Birebir eşlemede hedef kategori zorunlu.");
        if (req.MappingKind == "rules" && req.Rules is not { Count: > 0 })
            return (null, "Koşullu eşlemede en az bir kural gerekli.");
        if (req.MappingKind == "pool" && req.Pool is not { Count: > 1 })
            return (null, "Havuz eşlemesinde en az iki aday kategori seçilmeli.");

        var existing = await db.MarketplaceCategoryMappings.FirstOrDefaultAsync(
            m => m.Marketplace == req.Marketplace && m.ProductGroupId == req.ProductGroupId
                 && m.FirmPlatformId == null, ct);
        if (existing is null)
        {
            existing = new MarketplaceCategoryMapping
            {
                Marketplace = req.Marketplace,
                ProductGroupId = req.ProductGroupId,
                CreatedBy = userId
            };
            db.MarketplaceCategoryMappings.Add(existing);
        }
        else
        {
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = userId;
        }

        existing.MappingKind = req.MappingKind;
        existing.TargetExternalId = req.MappingKind == "pool" ? null : req.TargetExternalId;
        existing.TargetName = req.MappingKind == "pool" ? null : req.TargetName;
        existing.TargetPath = req.MappingKind == "pool" ? null : req.TargetPath;
        existing.RulesJson = req.MappingKind == "rules"
            ? JsonSerializer.Serialize(req.Rules!.OrderBy(r => r.Order).ToList(), JsonOpts) : null;
        existing.PoolJson = req.MappingKind == "pool"
            ? JsonSerializer.Serialize(req.Pool, JsonOpts) : null;
        // Personel kaydetti = gözden geçirilmiş sayılır.
        existing.Status = "active";
        existing.StatusNote = null;

        await db.SaveChangesAsync(ct);
        return (ToDto(existing), null);
    }

    public async Task<bool> DeleteCategoryMappingAsync(Guid id, Guid? userId, CancellationToken ct)
    {
        var m = await db.MarketplaceCategoryMappings.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is null) return false;
        m.IsDeleted = true;
        m.DeletedAt = DateTime.UtcNow;
        m.DeletedBy = userId;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Özellik sekmesinin bağlam dropdown'ı: eşlemelerde hedef olarak geçen pazaryeri kategorileri.</summary>
    public async Task<List<MappedTargetDto>> GetMappedTargetsAsync(string marketplace, CancellationToken ct)
    {
        var mappings = await db.MarketplaceCategoryMappings
            .Where(m => m.Marketplace == marketplace)
            .ToListAsync(ct);

        var groupNames = new Dictionary<Guid, string>();
        await using (var cmd = mainDb.CreateCommand(
            """SELECT "Id", COALESCE("NameI18n"->>'tr', "Code") FROM definition.product_groups WHERE NOT "IsDeleted" """))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                groupNames[reader.GetGuid(0)] = reader.GetString(1);

        var targets = new Dictionary<string, (string Name, string Path, HashSet<string> Groups)>();
        void Add(string? extId, string? name, string? path, Guid groupId)
        {
            if (string.IsNullOrEmpty(extId)) return;
            var t = targets.TryGetValue(extId, out var e) ? e : (name ?? extId, path ?? "", []);
            t.Groups.Add(groupNames.GetValueOrDefault(groupId, "?"));
            targets[extId] = t;
        }
        foreach (var m in mappings)
        {
            var dto = ToDto(m);
            Add(dto.TargetExternalId, dto.TargetName, dto.TargetPath, m.ProductGroupId);
            foreach (var r in dto.Rules) Add(r.TargetExternalId, r.TargetName, r.TargetPath, m.ProductGroupId);
            foreach (var p in dto.Pool) Add(p.ExternalId, p.Name, p.Path, m.ProductGroupId);
        }
        return targets
            .Select(kv => new MappedTargetDto(kv.Key, kv.Value.Name, kv.Value.Path, kv.Value.Groups.Order().ToList()))
            .OrderBy(t => t.Path)
            .ToList();
    }

    // ── Özellik eşleme ───────────────────────────────────────────────────────

    public async Task<AttributesViewDto> GetAttributesAsync(
        string marketplace, string mpCategoryId, CancellationToken ct)
    {
        // Pazaryeri özellikleri + değer sayıları (referans DB)
        var mpAttrs = new List<(string ExtId, string Name, bool Req, bool Ac, bool Va, string Vm, int ValCount)>();
        var ds = await refDb.GetAsync(ct);
        if (ds is not null)
        {
            await using var cmd = ds.CreateCommand(
                """
                SELECT a.attribute_external_id, a.name, a.is_required, a.allow_custom,
                       a.is_variant_axis, a.value_mode,
                       (SELECT count(*) FROM mp_attribute_values v
                        WHERE v.marketplace=a.marketplace AND v.category_external_id=a.category_external_id
                          AND v.attribute_external_id=a.attribute_external_id AND v.removed_at IS NULL)::int
                FROM mp_category_attributes a
                WHERE a.marketplace=$1 AND a.category_external_id=$2 AND a.removed_at IS NULL
                ORDER BY a.is_required DESC, a.name
                """);
            cmd.Parameters.AddWithValue(marketplace);
            cmd.Parameters.AddWithValue(mpCategoryId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                mpAttrs.Add((reader.GetString(0), reader.GetString(1), reader.GetBoolean(2),
                    reader.GetBoolean(3), reader.GetBoolean(4), reader.GetString(5), reader.GetInt32(6)));
        }

        var mappings = await db.MarketplaceAttributeMappings
            .Where(m => m.Marketplace == marketplace && m.MpCategoryExternalId == mpCategoryId && m.FirmPlatformId == null)
            .ToListAsync(ct);
        var mapByAttr = mappings.ToDictionary(m => m.MpAttributeExternalId);

        // Değer eşleme ilerlemesi: bizim değer sayısı (attribute type başına) + eşlenen sayı
        var mappedTypeIds = mappings.Where(m => m.AttributeTypeId is not null)
            .Select(m => m.AttributeTypeId!.Value).Distinct().ToList();
        var ownValueCounts = new Dictionary<Guid, int>();
        if (mappedTypeIds.Count > 0)
        {
            await using var cmd = mainDb.CreateCommand(
                """
                SELECT "AttributeTypeId", count(*)::int FROM definition.attribute_values
                WHERE "AttributeTypeId" = ANY($1) AND NOT "IsDeleted" AND "IsActive"
                GROUP BY 1
                """);
            cmd.Parameters.AddWithValue(mappedTypeIds.ToArray());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                ownValueCounts[reader.GetGuid(0)] = reader.GetInt32(1);
        }
        var mappedValueCounts = (await db.MarketplaceValueMappings
                .Where(v => v.Marketplace == marketplace && v.MpCategoryExternalId == mpCategoryId)
                .GroupBy(v => v.MpAttributeExternalId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.Count);

        var ownTypes = new List<OwnAttributeTypeDto>();
        await using (var cmd = mainDb.CreateCommand(
            """
            SELECT "Id", "Code", COALESCE("NameI18n"->>'tr', "Code") FROM definition.attribute_types
            WHERE NOT "IsDeleted" AND "IsActive"
            ORDER BY COALESCE("NameI18n"->>'tr', "Code") COLLATE "tr-TR-x-icu"
            """))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                ownTypes.Add(new OwnAttributeTypeDto(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));

        var rows = mpAttrs.Select(a =>
        {
            var m = mapByAttr.GetValueOrDefault(a.ExtId);
            return new MpAttributeRowDto(
                a.ExtId, a.Name, a.Req, a.Ac, a.Va, a.Vm, a.ValCount,
                m?.Id, m?.Strategy, m?.AttributeTypeId, m?.FixedValue, m?.Status, m?.StatusNote,
                OwnValueCount: m?.AttributeTypeId is Guid t ? ownValueCounts.GetValueOrDefault(t) : 0,
                MappedValueCount: mappedValueCounts.GetValueOrDefault(a.ExtId));
        }).ToList();

        return new AttributesViewDto(rows, ownTypes);
    }

    public async Task<string?> SaveAttributeMappingAsync(
        SaveAttributeMappingRequest req, Guid? userId, CancellationToken ct)
    {
        if (req.Strategy is not ("map_values" or "pass_literal" or "fixed_value"))
            return "strategy map_values | pass_literal | fixed_value olmalı.";
        if (req.Strategy is "map_values" or "pass_literal" && req.AttributeTypeId is null)
            return "Bu stratejide bizim özellik tipi (attributeTypeId) zorunlu.";
        if (req.Strategy == "fixed_value" && string.IsNullOrWhiteSpace(req.FixedValue))
            return "Sabit değer stratejisinde fixedValue zorunlu.";

        var existing = await db.MarketplaceAttributeMappings.FirstOrDefaultAsync(
            m => m.Marketplace == req.Marketplace && m.MpCategoryExternalId == req.MpCategoryExternalId
                 && m.MpAttributeExternalId == req.MpAttributeExternalId && m.FirmPlatformId == null, ct);
        if (existing is null)
        {
            existing = new MarketplaceAttributeMapping
            {
                Marketplace = req.Marketplace,
                MpCategoryExternalId = req.MpCategoryExternalId,
                MpAttributeExternalId = req.MpAttributeExternalId,
                CreatedBy = userId
            };
            db.MarketplaceAttributeMappings.Add(existing);
        }
        else
        {
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = userId;
        }
        existing.MpAttributeName = req.MpAttributeName;
        existing.Strategy = req.Strategy;
        existing.AttributeTypeId = req.Strategy == "fixed_value" ? null : req.AttributeTypeId;
        existing.FixedValue = req.Strategy == "fixed_value" ? req.FixedValue : null;
        existing.Status = "active";
        existing.StatusNote = null;
        await db.SaveChangesAsync(ct);
        return null;
    }

    // ── Değer eşleme ─────────────────────────────────────────────────────────

    public async Task<(ValuesViewDto? Dto, string? Error)> GetValuesAsync(
        string marketplace, string mpCategoryId, string mpAttributeId, CancellationToken ct)
    {
        var attrMapping = await db.MarketplaceAttributeMappings.FirstOrDefaultAsync(
            m => m.Marketplace == marketplace && m.MpCategoryExternalId == mpCategoryId
                 && m.MpAttributeExternalId == mpAttributeId && m.FirmPlatformId == null, ct);
        if (attrMapping?.AttributeTypeId is null)
            return (null, "Önce bu özelliği bizim bir özellik tipiyle eşleyin.");

        // Bizim değerler
        var ownValues = new List<OwnAttributeValueDto>();
        string? typeName = null;
        await using (var cmd = mainDb.CreateCommand(
            """
            SELECT v."Id", COALESCE(v."NameI18n"->>'tr', v."Id"::text),
                   COALESCE(t."NameI18n"->>'tr', t."Code")
            FROM definition.attribute_values v
            JOIN definition.attribute_types t ON t."Id" = v."AttributeTypeId"
            WHERE v."AttributeTypeId"=$1 AND NOT v."IsDeleted" AND v."IsActive"
            ORDER BY v."SortOrder", COALESCE(v."NameI18n"->>'tr', v."Id"::text) COLLATE "tr-TR-x-icu"
            """))
        {
            cmd.Parameters.AddWithValue(attrMapping.AttributeTypeId.Value);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                ownValues.Add(new OwnAttributeValueDto(reader.GetGuid(0), reader.GetString(1)));
                typeName ??= reader.GetString(2);
            }
        }

        // Pazaryeri değerleri (referans DB)
        var mpValues = new List<MpValueDto>();
        var ds = await refDb.GetAsync(ct);
        if (ds is not null)
        {
            await using var cmd = ds.CreateCommand(
                """
                SELECT value_external_id, value_code, value FROM mp_attribute_values
                WHERE marketplace=$1 AND category_external_id=$2 AND attribute_external_id=$3 AND removed_at IS NULL
                ORDER BY value COLLATE "tr-TR-x-icu"
                """);
            cmd.Parameters.AddWithValue(marketplace);
            cmd.Parameters.AddWithValue(mpCategoryId);
            cmd.Parameters.AddWithValue(mpAttributeId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                mpValues.Add(new MpValueDto(
                    reader.IsDBNull(0) ? null : reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    reader.GetString(2)));
        }

        var existing = (await db.MarketplaceValueMappings
                .Where(v => v.Marketplace == marketplace && v.MpCategoryExternalId == mpCategoryId
                            && v.MpAttributeExternalId == mpAttributeId)
                .ToListAsync(ct))
            .ToDictionary(v => v.AttributeValueId);

        var rows = ownValues.Select(ov =>
        {
            var ex = existing.GetValueOrDefault(ov.Id);
            string? sugId = null, sugVal = null;
            var sugScore = 0;
            if (ex is null)
            {
                var top = TextSimilarity.TopMatches(ov.Label, mpValues, v => v.Value, 1);
                if (top.Count > 0)
                {
                    sugId = top[0].Item.ExternalId;
                    sugVal = top[0].Item.Value;
                    sugScore = top[0].Score;
                }
            }
            return new ValueRowDto(
                ov.Id, ov.Label, ex?.TargetExternalId, ex?.TargetValue, ex?.Status ?? "unmapped",
                sugId, sugVal, sugScore);
        }).ToList();

        return (new ValuesViewDto(attrMapping.AttributeTypeId, typeName, rows, mpValues), null);
    }

    public async Task<int> SaveValueMappingsAsync(
        SaveValueMappingsRequest req, Guid? userId, CancellationToken ct)
    {
        var existing = await db.MarketplaceValueMappings
            .Where(v => v.Marketplace == req.Marketplace && v.MpCategoryExternalId == req.MpCategoryExternalId
                        && v.MpAttributeExternalId == req.MpAttributeExternalId)
            .ToListAsync(ct);
        var byValue = existing.ToDictionary(v => v.AttributeValueId);

        var changed = 0;
        foreach (var item in req.Items)
        {
            var current = byValue.GetValueOrDefault(item.AttributeValueId);
            var cleared = string.IsNullOrWhiteSpace(item.TargetExternalId)
                          && string.IsNullOrWhiteSpace(item.TargetValue);
            if (cleared)
            {
                if (current is null) continue;
                current.IsDeleted = true;
                current.DeletedAt = DateTime.UtcNow;
                current.DeletedBy = userId;
                changed++;
                continue;
            }
            if (current is null)
            {
                current = new MarketplaceValueMapping
                {
                    Marketplace = req.Marketplace,
                    MpCategoryExternalId = req.MpCategoryExternalId,
                    MpAttributeExternalId = req.MpAttributeExternalId,
                    AttributeValueId = item.AttributeValueId,
                    CreatedBy = userId
                };
                db.MarketplaceValueMappings.Add(current);
            }
            else
            {
                if (current.TargetExternalId == item.TargetExternalId
                    && current.TargetValue == (item.TargetValue ?? "")) continue; // dokunma
                current.UpdatedAt = DateTime.UtcNow;
                current.UpdatedBy = userId;
            }
            current.TargetExternalId = item.TargetExternalId;
            current.TargetCode = item.TargetCode;
            current.TargetValue = item.TargetValue ?? "";
            current.Status = "active";
            current.StatusNote = null;
            changed++;
        }
        await db.SaveChangesAsync(ct);
        return changed;
    }

    // ── Gözden geçir ─────────────────────────────────────────────────────────

    public async Task<List<ReviewRowDto>> GetReviewAsync(string? marketplace, CancellationToken ct)
    {
        var result = new List<ReviewRowDto>();

        var catMappings = await db.MarketplaceCategoryMappings
            .Where(m => m.Status != "active" && (marketplace == null || m.Marketplace == marketplace))
            .ToListAsync(ct);
        if (catMappings.Count > 0)
        {
            var groupNames = new Dictionary<Guid, string>();
            await using var cmd = mainDb.CreateCommand(
                """SELECT "Id", COALESCE("NameI18n"->>'tr', "Code") FROM definition.product_groups WHERE "Id" = ANY($1)""");
            cmd.Parameters.AddWithValue(catMappings.Select(m => m.ProductGroupId).Distinct().ToArray());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                groupNames[reader.GetGuid(0)] = reader.GetString(1);
            result.AddRange(catMappings.Select(m => new ReviewRowDto(
                m.Id, "category", m.Marketplace, m.Status,
                $"{groupNames.GetValueOrDefault(m.ProductGroupId, "?")} → {m.TargetName ?? m.MappingKind}",
                m.StatusNote, m.TargetExternalId, m.ProductGroupId)));
        }

        result.AddRange((await db.MarketplaceAttributeMappings
                .Where(m => m.Status != "active" && (marketplace == null || m.Marketplace == marketplace))
                .ToListAsync(ct))
            .Select(m => new ReviewRowDto(
                m.Id, "attribute", m.Marketplace, m.Status,
                $"Özellik: {m.MpAttributeName}", m.StatusNote, m.MpCategoryExternalId, null)));

        result.AddRange((await db.MarketplaceValueMappings
                .Where(m => m.Status != "active" && (marketplace == null || m.Marketplace == marketplace))
                .ToListAsync(ct))
            .Select(m => new ReviewRowDto(
                m.Id, "value", m.Marketplace, m.Status,
                $"Değer: {m.TargetValue}", m.StatusNote, m.MpCategoryExternalId, null)));

        return result.OrderBy(r => r.Status == "broken" ? 0 : 1).ThenBy(r => r.Title).ToList();
    }

    /// <summary>Gözden geçirme satırını onayla (durumu active'e çeker) — koşul gerçekten
    /// düzeldiyse kullanılmalı; sağlık job'ı bir sonraki turda hâlâ bozuksa yeniden işaretler.</summary>
    public async Task<bool> AcknowledgeAsync(string mappingType, Guid id, Guid? userId, CancellationToken ct)
    {
        int n;
        switch (mappingType)
        {
            case "category":
                n = await db.MarketplaceCategoryMappings.Where(m => m.Id == id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(m => m.Status, "active")
                        .SetProperty(m => m.StatusNote, (string?)null)
                        .SetProperty(m => m.UpdatedAt, DateTime.UtcNow)
                        .SetProperty(m => m.UpdatedBy, userId), ct);
                break;
            case "attribute":
                n = await db.MarketplaceAttributeMappings.Where(m => m.Id == id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(m => m.Status, "active")
                        .SetProperty(m => m.StatusNote, (string?)null)
                        .SetProperty(m => m.UpdatedAt, DateTime.UtcNow)
                        .SetProperty(m => m.UpdatedBy, userId), ct);
                break;
            case "value":
                n = await db.MarketplaceValueMappings.Where(m => m.Id == id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(m => m.Status, "active")
                        .SetProperty(m => m.StatusNote, (string?)null)
                        .SetProperty(m => m.UpdatedAt, DateTime.UtcNow)
                        .SetProperty(m => m.UpdatedBy, userId), ct);
                break;
            default:
                return false;
        }
        return n > 0;
    }
}
