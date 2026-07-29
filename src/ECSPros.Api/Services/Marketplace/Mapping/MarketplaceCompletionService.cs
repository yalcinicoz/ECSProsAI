using ECSPros.Api.Services.Marketplace.Reference;
using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECSPros.Api.Services.Marketplace.Mapping;

public sealed record CompletionAttrDto(
    string ExternalId,
    string Name,
    bool AllowCustom,
    string ValueMode,
    string ReasonCode,                 // required_attr_missing | value_unmapped
    List<MpValueDto> Values,           // liste özelliğiyse referans DB'den seçenekler
    string? CurrentValueExternalId,
    string? CurrentValueText);

public sealed record CompletionViewDto(
    Guid ProductId,
    string ProductCode,
    string? ProductName,
    string GroupName,
    string Status,
    List<string> ReasonLabels,
    string? ResolvedCategoryExternalId,
    string? ResolvedCategoryPath,
    bool NeedsCategory,
    string MappingKind,                // grup eşlemesinin kipi (pool ise adaylar gösterilir)
    List<PoolTargetDto> PoolCandidates,
    List<CategorySuggestionDto> Suggestions,
    List<CompletionAttrDto> MissingAttributes);

public sealed record CompletionCategoryDto(string ExternalId, string Name, string Path, string Source);

public sealed record CompletionValueDto(
    string MpAttributeExternalId, string MpAttributeName, string? ValueExternalId, string? ValueText);

public sealed record SaveCompletionRequest(
    string Marketplace,
    List<Guid> ProductIds,
    CompletionCategoryDto? Category,
    string? MpCategoryExternalId,      // özellik değerlerinin kategori kapsamı (Category verilmişse onunki)
    List<CompletionValueDto>? Values);

/// <summary>
/// Tamamlama ekranı servis yüzeyi (§3): eksik üründe kategori ataması (istisna tablosuna,
/// K4) ve zorunlu özellik doldurma (ürün-özel pazaryeri değerlerine, K6 — kendi kataloğa
/// yazılmaz). Kayıt sonrası ilgili ürünlerin readiness'i anında yeniden hesaplanır.
/// Toplu tamamlama aynı uçtan (ProductIds çoklu).
/// </summary>
public sealed class MarketplaceCompletionService(
    NpgsqlDataSource mainDb,
    MarketplaceRefDb refDb,
    IIntegrationDbContext db,
    MarketplaceMappingService mappingService,
    MarketplaceReadinessService readiness)
{
    public async Task<(CompletionViewDto? Dto, string? Error)> GetAsync(
        string marketplace, Guid productId, CancellationToken ct)
    {
        // Ürün + grup
        string? code = null, name = null, groupName = "?";
        Guid groupId = default;
        await using (var cmd = mainDb.CreateCommand(
            """
            SELECT p."Code", p."NameI18n"->>'tr', p."ProductGroupId",
                   COALESCE(g."NameI18n"->>'tr', g."Code")
            FROM catalog.products p
            JOIN definition.product_groups g ON g."Id" = p."ProductGroupId"
            WHERE p."Id"=$1 AND NOT p."IsDeleted"
            """))
        {
            cmd.Parameters.AddWithValue(productId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return (null, "Ürün bulunamadı.");
            code = r.GetString(0);
            name = r.IsDBNull(1) ? null : r.GetString(1);
            groupId = r.GetGuid(2);
            groupName = r.GetString(3);
        }

        // Denetimi HER açılışta bu ürün için tazele — eşleme/referans verisi ekran açıkken
        // değişmiş olabilir; bayat satır boş/yanlış form üretir (tek ürün: milisaniyeler).
        await readiness.RecomputeAsync(marketplace, [productId], ct);
        var row = await db.MarketplaceProductReadiness.AsNoTracking().FirstOrDefaultAsync(
            x => x.Marketplace == marketplace && x.ProductId == productId && x.FirmPlatformId == null, ct);
        if (row is null) return (null, "Denetim hesaplanamadı.");

        var reasons = MarketplaceReadinessService.ParseReasons(row.ReasonsJson);
        var needsCategory = row.ResolvedCategoryExternalId is null;

        // Grup eşlemesinin kipi + havuz adayları
        var mapping = await db.MarketplaceCategoryMappings.AsNoTracking().FirstOrDefaultAsync(
            m => m.Marketplace == marketplace && m.ProductGroupId == groupId && m.FirmPlatformId == null, ct);
        var kind = mapping?.MappingKind ?? "none";
        var pool = mapping?.PoolJson is null ? [] :
            System.Text.Json.JsonSerializer.Deserialize<List<PoolTargetDto>>(
                mapping.PoolJson, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) ?? [];

        var suggestions = needsCategory
            ? await mappingService.SuggestCategoriesAsync(marketplace, groupId, ct)
            : [];

        // Eksik özellik formu — çözülen kategori varsa
        var missingAttrs = new List<CompletionAttrDto>();
        if (row.ResolvedCategoryExternalId is string cat)
        {
            var wanted = reasons
                .Where(r => r.Code is "required_attr_missing" or "value_unmapped" && r.Attr is not null)
                .ToDictionary(r => r.Attr!, r => r.Code);
            if (wanted.Count > 0)
            {
                var existing = await db.MarketplaceProductAttributeValues.AsNoTracking()
                    .Where(v => v.Marketplace == marketplace && v.ProductId == productId
                                && v.MpCategoryExternalId == cat && v.FirmPlatformId == null)
                    .ToListAsync(ct);
                var existingByAttr = existing.ToDictionary(v => v.MpAttributeExternalId);

                var ds = await refDb.GetAsync(ct);
                if (ds is not null)
                {
                    await using var cmd = ds.CreateCommand(
                        """
                        SELECT a.attribute_external_id, a.name, a.allow_custom, a.value_mode
                        FROM mp_category_attributes a
                        WHERE a.marketplace=$1 AND a.category_external_id=$2
                          AND a.is_required AND NOT a.is_variant_axis AND a.removed_at IS NULL
                        ORDER BY a.name
                        """);
                    cmd.Parameters.AddWithValue(marketplace);
                    cmd.Parameters.AddWithValue(cat);
                    var attrs = new List<(string Id, string Name, bool Ac, string Vm)>();
                    await using (var r = await cmd.ExecuteReaderAsync(ct))
                        while (await r.ReadAsync(ct))
                            attrs.Add((r.GetString(0), r.GetString(1), r.GetBoolean(2), r.GetString(3)));

                    foreach (var a in attrs.Where(a => wanted.ContainsKey(a.Name)))
                    {
                        var values = new List<MpValueDto>();
                        await using var vc = ds.CreateCommand(
                            """
                            SELECT value_external_id, value_code, value FROM mp_attribute_values
                            WHERE marketplace=$1 AND category_external_id=$2 AND attribute_external_id=$3
                              AND removed_at IS NULL
                            ORDER BY value COLLATE "tr-TR-x-icu" LIMIT 1000
                            """);
                        vc.Parameters.AddWithValue(marketplace);
                        vc.Parameters.AddWithValue(cat);
                        vc.Parameters.AddWithValue(a.Id);
                        await using (var vr = await vc.ExecuteReaderAsync(ct))
                            while (await vr.ReadAsync(ct))
                                values.Add(new MpValueDto(
                                    vr.IsDBNull(0) ? null : vr.GetString(0),
                                    vr.IsDBNull(1) ? null : vr.GetString(1),
                                    vr.GetString(2)));

                        var cur = existingByAttr.GetValueOrDefault(a.Id);
                        missingAttrs.Add(new CompletionAttrDto(
                            a.Id, a.Name, a.Ac, a.Vm, wanted[a.Name], values,
                            cur?.ValueExternalId, cur?.ValueText));
                    }
                }
            }
        }

        return (new CompletionViewDto(
            productId, code!, name, groupName, row.Status,
            reasons.Select(MarketplaceReadinessService.ReasonLabel).ToList(),
            row.ResolvedCategoryExternalId, row.ResolvedCategoryPath,
            needsCategory, kind, pool, suggestions, missingAttrs), null);
    }

    public async Task<(RecomputeResult? Result, string? Error)> SaveAsync(
        SaveCompletionRequest req, Guid? userId, CancellationToken ct)
    {
        if (req.ProductIds is not { Count: > 0 })
            return (null, "En az bir ürün gerekli.");
        if (req.Category is null && req.Values is not { Count: > 0 })
            return (null, "Kategori ataması veya en az bir özellik değeri verilmeli.");
        if (req.Category is not null &&
            (string.IsNullOrWhiteSpace(req.Category.ExternalId) || string.IsNullOrWhiteSpace(req.Category.Path)))
            return (null, "Kategori ataması için geçerli bir hedef (externalId + path) gerekli.");
        var categoryScope = req.Category?.ExternalId ?? req.MpCategoryExternalId;
        if (req.Values is { Count: > 0 } && string.IsNullOrEmpty(categoryScope))
            return (null, "Özellik değerleri için kategori kapsamı (mpCategoryExternalId) gerekli.");

        if (req.Category is not null)
        {
            var existing = await db.MarketplaceProductCategoryOverrides
                .Where(o => o.Marketplace == req.Marketplace && o.FirmPlatformId == null
                            && req.ProductIds.Contains(o.ProductId))
                .ToListAsync(ct);
            var byProduct = existing.ToDictionary(o => o.ProductId);
            foreach (var pid in req.ProductIds)
            {
                var ov = byProduct.GetValueOrDefault(pid);
                if (ov is null)
                {
                    ov = new MarketplaceProductCategoryOverride
                    {
                        ProductId = pid,
                        Marketplace = req.Marketplace,
                        CreatedBy = userId
                    };
                    db.MarketplaceProductCategoryOverrides.Add(ov);
                }
                else
                {
                    ov.UpdatedAt = DateTime.UtcNow;
                    ov.UpdatedBy = userId;
                }
                ov.CategoryExternalId = req.Category.ExternalId;
                ov.CategoryName = req.Category.Name;
                ov.CategoryPath = req.Category.Path;
                ov.Source = req.Category.Source is "pool_assignment" or "manual" or "rejection" or "remote"
                    ? req.Category.Source : "manual";
            }
        }

        if (req.Values is { Count: > 0 })
        {
            var existing = await db.MarketplaceProductAttributeValues
                .Where(v => v.Marketplace == req.Marketplace && v.FirmPlatformId == null
                            && v.MpCategoryExternalId == categoryScope
                            && req.ProductIds.Contains(v.ProductId))
                .ToListAsync(ct);
            var byKey = existing.ToDictionary(v => (v.ProductId, v.MpAttributeExternalId));

            foreach (var pid in req.ProductIds)
                foreach (var val in req.Values)
                {
                    var cleared = string.IsNullOrWhiteSpace(val.ValueExternalId)
                                  && string.IsNullOrWhiteSpace(val.ValueText);
                    var cur = byKey.GetValueOrDefault((pid, val.MpAttributeExternalId));
                    if (cleared)
                    {
                        if (cur is null) continue;
                        cur.IsDeleted = true;
                        cur.DeletedAt = DateTime.UtcNow;
                        cur.DeletedBy = userId;
                        continue;
                    }
                    if (cur is null)
                    {
                        cur = new MarketplaceProductAttributeValue
                        {
                            ProductId = pid,
                            Marketplace = req.Marketplace,
                            MpCategoryExternalId = categoryScope!,
                            MpAttributeExternalId = val.MpAttributeExternalId,
                            CreatedBy = userId
                        };
                        db.MarketplaceProductAttributeValues.Add(cur);
                    }
                    else
                    {
                        cur.UpdatedAt = DateTime.UtcNow;
                        cur.UpdatedBy = userId;
                    }
                    cur.MpAttributeName = val.MpAttributeName;
                    cur.ValueExternalId = val.ValueExternalId;
                    cur.ValueText = val.ValueText;
                }
        }

        await db.SaveChangesAsync(ct);

        // Kaydedilen ürünlerin denetimi anında tazelenir — liste doğru çipe düşer.
        var result = await readiness.RecomputeAsync(req.Marketplace, req.ProductIds, ct);
        return (result, null);
    }
}
