using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECSPros.Api.Services.Marketplace.Reference;
using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECSPros.Api.Services.Marketplace.Send;

public sealed record SendResultDto(
    int Submitted, int BatchCount, List<string> BatchIds,
    int SkippedNotReady, int SkippedUnchanged, int SkippedNoBarcode);

/// <summary>
/// Gerçek Trendyol ürün gönderimi (F4 §4): readiness'i HAZIR ürünlerin varyantlarını
/// Trendyol createProducts payload'ına çevirir, ≤100'lük paketlerle gönderir, batch +
/// item kayıtlarını açar. Sonuç asenkrondur — MarketplaceBatchWorker sorgular.
/// Kategori: readiness'in çözdüğü kategori (istisna > kural > birebir, K4).
/// Özellik değeri önceliği: ürün-özel > değer eşlemesi > sabit > serbest (K6 zinciri).
/// Diff: içerik hash'i değişmemiş ve synced olan varyant yeniden gönderilmez (§4.4).
/// </summary>
public sealed class MarketplaceSendService(
    NpgsqlDataSource mainDb,
    MarketplaceRefDb refDb,
    IIntegrationDbContext db,
    ICatalogDbContext catalogDb,
    MarketplaceAdminService admin,
    TrendyolSellerClient trendyol,
    ILogger<MarketplaceSendService> logger)
{
    private const int ChunkSize = 100;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<(SendResultDto? Result, string? Error)> SubmitProductsAsync(
        Guid firmPlatformId, Guid firmIntegrationId,
        IReadOnlyCollection<Guid>? variantIds, IReadOnlyCollection<Guid>? productIds,
        CancellationToken ct)
    {
        var marketplace = await admin.GetMarketplaceCodeAsync(firmPlatformId, ct);
        if (marketplace is null) return (null, "Mağaza bulunamadı.");

        var (config, cfgError) = await trendyol.ResolveConfigAsync(firmIntegrationId, ct);
        if (config is null) return (null, cfgError);
        if (config.BrandId is null || config.CargoCompanyId is null)
            return (null, "Sözleşme ayarlarında brandId ve cargoCompanyId dolu olmalı (Trendyol zorunlu alanları).");

        var payloads = await admin.GetSyncPayloadsAsync(firmPlatformId, variantIds, productIds, ct);
        if (payloads.Count == 0) return (null, "Gönderilecek aktif varyant bulunamadı.");

        var allProductIds = payloads.Select(p => p.ProductId).Distinct().ToList();

        // Readiness — yalnız HAZIR ürünler gönderilir (eksikler tamamlama ekranına)
        var readiness = await db.MarketplaceProductReadiness.AsNoTracking()
            .Where(r => r.Marketplace == marketplace && r.FirmPlatformId == null
                        && allProductIds.Contains(r.ProductId))
            .ToDictionaryAsync(r => r.ProductId, ct);
        var readyProducts = allProductIds
            .Where(p => readiness.TryGetValue(p, out var r) && r.Status == "ready"
                        && r.ResolvedCategoryExternalId is not null)
            .ToHashSet();
        var skippedNotReady = allProductIds.Count - readyProducts.Count;
        var rows = payloads.Where(p => readyProducts.Contains(p.ProductId)).ToList();
        if (rows.Count == 0)
            return (null, $"Seçilen ürünlerin hiçbiri denetimden geçmiş değil ({skippedNotReady} ürün Eksik/denetimsiz). Önce 'Denetle' çalıştırıp eksikleri tamamlayın.");

        var skippedNoBarcode = rows.RemoveAll(p => string.IsNullOrWhiteSpace(p.Barcode ?? p.Sku));

        var categoryOf = readyProducts.ToDictionary(p => p, p => readiness[p].ResolvedCategoryExternalId!);
        var context = await LoadBuildContextAsync(marketplace, readyProducts, categoryOf, rows, ct);

        // Diff — değişmeyen synced varyantlar elenir
        var variantList = rows.Select(r => r.VariantId).ToList();
        var existing = await db.MarketplaceProducts
            .Where(mp => mp.FirmIntegrationId == firmIntegrationId && variantList.Contains(mp.VariantId))
            .ToDictionaryAsync(mp => mp.VariantId, ct);

        var items = new List<(SyncPayloadRow Row, object Payload, string Hash)>();
        var skippedUnchanged = 0;
        foreach (var row in rows)
        {
            var payload = BuildItem(row, config, categoryOf[row.ProductId], context);
            var hash = Sha256(JsonSerializer.Serialize(payload, JsonOpts));
            if (existing.TryGetValue(row.VariantId, out var mp)
                && mp.SyncStatus == "synced" && mp.LastSentPayloadHash == hash)
            {
                skippedUnchanged++;
                continue;
            }
            items.Add((row, payload, hash));
        }
        if (items.Count == 0)
            return (new SendResultDto(0, 0, [], skippedNotReady, skippedUnchanged, skippedNoBarcode),
                null);

        // ≤100'lük paketler halinde gönder; her paket kendi batch kaydını açar
        var batchIds = new List<string>();
        foreach (var chunk in items.Chunk(ChunkSize))
        {
            ct.ThrowIfCancellationRequested();
            var externalBatchId = await trendyol.CreateProductsAsync(
                config, new { items = chunk.Select(c => c.Payload).ToList() }, ct);

            var batch = new MarketplaceBatch
            {
                Marketplace = marketplace,
                FirmPlatformId = firmPlatformId,
                FirmIntegrationId = firmIntegrationId,
                ExternalBatchId = externalBatchId,
                BatchType = "product_upsert",
                Status = "submitted",
                ItemCount = chunk.Length,
                NextPollAt = DateTime.UtcNow.AddMinutes(1)
            };
            db.MarketplaceBatches.Add(batch);

            foreach (var (row, _, hash) in chunk)
            {
                db.MarketplaceBatchItems.Add(new MarketplaceBatchItem
                {
                    BatchId = batch.Id,
                    ProductId = row.ProductId,
                    VariantId = row.VariantId,
                    Barcode = (row.Barcode ?? row.Sku).Trim(),
                    PayloadHash = hash
                });

                if (!existing.TryGetValue(row.VariantId, out var mp))
                {
                    mp = new MarketplaceProduct
                    {
                        FirmIntegrationId = firmIntegrationId,
                        FirmPlatformId = firmPlatformId,
                        VariantId = row.VariantId,
                        ExternalId = (row.Barcode ?? row.Sku).Trim()
                    };
                    db.MarketplaceProducts.Add(mp);
                    existing[row.VariantId] = mp;
                }
                mp.ExternalBarcode = (row.Barcode ?? row.Sku).Trim();
                mp.SyncStatus = "pending";
                mp.LastSyncError = null;
                mp.LastErrorCode = null;
                mp.SuggestedCategoryExternalId = null;
                mp.LastSentPayloadHash = hash;
                mp.UpdatedAt = DateTime.UtcNow;
            }
            batchIds.Add(externalBatchId);
        }
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Trendyol gönderimi: {Items} varyant, {Batches} paket ({Marketplace}); atlanan: {NotReady} eksik, {Unchanged} değişmemiş",
            items.Count, batchIds.Count, marketplace, skippedNotReady, skippedUnchanged);
        return (new SendResultDto(items.Count, batchIds.Count, batchIds,
            skippedNotReady, skippedUnchanged, skippedNoBarcode), null);
    }

    /// <summary>Fiyat-stok hızlı kanalı (F5 §5): yalnız synced listing'ler, diff-based —
    /// pazaryerindeki bilinen değerle aynıysa gönderilmez. Aynı batch takip altyapısından geçer;
    /// başarıda hedef değerler (SentPrice/SentStock) listing'e işlenir.</summary>
    public async Task<(SendResultDto? Result, string? Error)> SubmitPriceStockAsync(
        Guid firmPlatformId, Guid firmIntegrationId,
        IReadOnlyCollection<Guid>? variantIds, CancellationToken ct)
    {
        var marketplace = await admin.GetMarketplaceCodeAsync(firmPlatformId, ct);
        if (marketplace is null) return (null, "Mağaza bulunamadı.");
        var (config, cfgError) = await trendyol.ResolveConfigAsync(firmIntegrationId, ct);
        if (config is null) return (null, cfgError);

        var listings = await db.MarketplaceProducts
            .Where(mp => mp.FirmIntegrationId == firmIntegrationId && mp.SyncStatus == "synced"
                         && mp.ExternalBarcode != null)
            .ToListAsync(ct);
        if (variantIds is { Count: > 0 })
        {
            var set = variantIds.ToHashSet();
            listings = listings.Where(l => set.Contains(l.VariantId)).ToList();
        }
        if (listings.Count == 0)
            return (null, "Fiyat-stok güncellenecek yüklü (synced) ürün yok.");

        var ids = listings.Select(l => l.VariantId).ToList();
        var payloads = await admin.GetSyncPayloadsAsync(firmPlatformId, ids, null, ct);
        var priceByVariant = payloads.ToDictionary(p => p.VariantId, p => p.Price);
        var productByVariant = payloads.ToDictionary(p => p.VariantId, p => p.ProductId);
        var stocks = await admin.GetSellableStocksAsync(ids, ct);

        var items = new List<(MarketplaceProduct Listing, decimal Price, int Stock)>();
        var skippedUnchanged = 0;
        foreach (var listing in listings)
        {
            if (!priceByVariant.TryGetValue(listing.VariantId, out var price) || price <= 0) continue;
            var stock = stocks.GetValueOrDefault(listing.VariantId);
            if (listing.MarketplacePrice == price && listing.MarketplaceStock == stock)
            {
                skippedUnchanged++;
                continue;
            }
            items.Add((listing, price, stock));
        }
        if (items.Count == 0)
            return (new SendResultDto(0, 0, [], 0, skippedUnchanged, 0), null);

        var batchIds = new List<string>();
        foreach (var chunk in items.Chunk(500))
        {
            ct.ThrowIfCancellationRequested();
            var payload = new
            {
                items = chunk.Select(c => new
                {
                    barcode = c.Listing.ExternalBarcode,
                    quantity = c.Stock,
                    salePrice = c.Price,
                    listPrice = c.Price
                }).ToList()
            };
            var externalBatchId = await trendyol.UpdatePriceInventoryAsync(config, payload, ct);

            var batch = new MarketplaceBatch
            {
                Marketplace = marketplace,
                FirmPlatformId = firmPlatformId,
                FirmIntegrationId = firmIntegrationId,
                ExternalBatchId = externalBatchId,
                BatchType = "price_stock",
                Status = "submitted",
                ItemCount = chunk.Length,
                NextPollAt = DateTime.UtcNow.AddMinutes(1)
            };
            db.MarketplaceBatches.Add(batch);
            foreach (var (listing, price, stock) in chunk)
                db.MarketplaceBatchItems.Add(new MarketplaceBatchItem
                {
                    BatchId = batch.Id,
                    ProductId = productByVariant.GetValueOrDefault(listing.VariantId),
                    VariantId = listing.VariantId,
                    Barcode = listing.ExternalBarcode!,
                    PayloadHash = Sha256($"{listing.ExternalBarcode}|{price}|{stock}"),
                    SentPrice = price,
                    SentStock = stock
                });
            batchIds.Add(externalBatchId);
        }
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Fiyat-stok gönderimi: {Items} varyant, {Batches} paket ({Marketplace}); {Unchanged} değişmemiş atlandı",
            items.Count, batchIds.Count, marketplace, skippedUnchanged);
        return (new SendResultDto(items.Count, batchIds.Count, batchIds, 0, skippedUnchanged, 0), null);
    }

    // ── Payload kurulum bağlamı ──────────────────────────────────────────────

    private sealed record BuildContext(
        Dictionary<Guid, int> Stocks,                                                // varyant → satılabilir stok
        Dictionary<Guid, int> TaxRates,
        Dictionary<Guid, List<string>> ImageUrls,
        Dictionary<string, List<RefAttr>> RefAttrs,                                  // kategori → özellikler
        Dictionary<(string Cat, string Attr), (string Strategy, Guid? TypeId, string? Fixed)> AttrMappings,
        Dictionary<(string Cat, string Attr, Guid OwnValue), string> ValueMappings,  // → hedef external id
        Dictionary<(Guid Product, string Cat, string Attr), (string? ExtId, string? Text)> ProductMpValues,
        Dictionary<Guid, Dictionary<Guid, List<Guid>>> OwnValues,                    // ürün → tip → değerler
        Dictionary<Guid, Dictionary<Guid, string>> OwnLiterals,                      // ürün → tip → serbest metin
        Dictionary<Guid, Dictionary<Guid, Guid>> VariantAttrs,                       // varyant → tip → değer
        Dictionary<Guid, string> ValueLabels);                                       // kendi değer id → tr etiket

    private sealed record RefAttr(string ExternalId, bool IsVariantAxis);

    private object BuildItem(
        SyncPayloadRow row, TrendyolSellerConfig config, string category, BuildContext ctx)
    {
        var attributes = new List<object>();

        foreach (var attr in ctx.RefAttrs.GetValueOrDefault(category, []))
        {
            if (!long.TryParse(attr.ExternalId, out var attrIdNum)) continue;
            var mapping = ctx.AttrMappings.GetValueOrDefault((category, attr.ExternalId));

            if (attr.IsVariantAxis)
            {
                // Varyant ekseni: varyantın kendi değeri → değer eşlemesi
                if (mapping.TypeId is Guid vt
                    && ctx.VariantAttrs.GetValueOrDefault(row.VariantId)?.TryGetValue(vt, out var vval) == true
                    && ctx.ValueMappings.TryGetValue((category, attr.ExternalId, vval), out var vTarget)
                    && long.TryParse(vTarget, out var vTargetNum))
                    attributes.Add(new { attributeId = attrIdNum, attributeValueId = vTargetNum });
                continue;
            }

            // 1) Ürün-özel pazaryeri değeri (K6 — en yüksek öncelik)
            if (ctx.ProductMpValues.TryGetValue((row.ProductId, category, attr.ExternalId), out var pv))
            {
                if (pv.ExtId is not null && long.TryParse(pv.ExtId, out var pvNum))
                    attributes.Add(new { attributeId = attrIdNum, attributeValueId = pvNum });
                else if (!string.IsNullOrWhiteSpace(pv.Text))
                    attributes.Add(new { attributeId = attrIdNum, customAttributeValue = pv.Text });
                continue;
            }

            // 2) Özellik eşlemesi stratejileri
            switch (mapping.Strategy)
            {
                case "fixed_value" when !string.IsNullOrWhiteSpace(mapping.Fixed):
                    attributes.Add(new { attributeId = attrIdNum, customAttributeValue = mapping.Fixed });
                    break;
                case "map_values" when mapping.TypeId is Guid mt:
                    var own = ctx.OwnValues.GetValueOrDefault(row.ProductId)?.GetValueOrDefault(mt);
                    var mapped = own?.Select(v => ctx.ValueMappings.GetValueOrDefault((category, attr.ExternalId, v)))
                        .FirstOrDefault(t => t is not null);
                    if (mapped is not null && long.TryParse(mapped, out var mappedNum))
                        attributes.Add(new { attributeId = attrIdNum, attributeValueId = mappedNum });
                    break;
                case "pass_literal" when mapping.TypeId is Guid lt:
                    var literal = ctx.OwnLiterals.GetValueOrDefault(row.ProductId)?.GetValueOrDefault(lt)
                        ?? ctx.OwnValues.GetValueOrDefault(row.ProductId)?.GetValueOrDefault(lt)
                            ?.Select(v => ctx.ValueLabels.GetValueOrDefault(v)).FirstOrDefault(x => x is not null);
                    if (!string.IsNullOrWhiteSpace(literal))
                        attributes.Add(new { attributeId = attrIdNum, customAttributeValue = literal });
                    break;
            }
        }

        var barcode = (row.Barcode ?? row.Sku).Trim();
        return new
        {
            barcode,
            title = row.Title.Length > 100 ? row.Title[..100] : row.Title,
            productMainId = row.ProductCode,
            brandId = config.BrandId!.Value,
            categoryId = long.Parse(category),
            quantity = ctx.Stocks.GetValueOrDefault(row.VariantId),
            stockCode = row.Sku,
            description = string.IsNullOrWhiteSpace(row.Description) ? row.Title : row.Description,
            currencyType = "TRY",
            listPrice = row.Price,
            salePrice = row.Price,
            vatRate = ctx.TaxRates.GetValueOrDefault(row.ProductId, 20),
            cargoCompanyId = config.CargoCompanyId!.Value,
            images = ctx.ImageUrls.GetValueOrDefault(row.ProductId, []).Select(u => new { url = u }).ToList(),
            attributes
        };
    }

    private async Task<BuildContext> LoadBuildContextAsync(
        string marketplace, HashSet<Guid> products, Dictionary<Guid, string> categoryOf,
        List<SyncPayloadRow> rows, CancellationToken ct)
    {
        var productArr = products.ToArray();
        var categories = categoryOf.Values.Distinct().ToArray();
        var variantArr = rows.Select(r => r.VariantId).ToArray();

        var stocks = await admin.GetSellableStocksAsync(variantArr, ct);

        var taxRates = new Dictionary<Guid, int>();
        await using (var cmd = mainDb.CreateCommand(
            """SELECT "Id", "TaxRate" FROM catalog.products WHERE "Id" = ANY($1)"""))
        {
            cmd.Parameters.AddWithValue(productArr);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) taxRates[r.GetGuid(0)] = r.GetInt32(1);
        }

        // Görseller: ürün düzeyi, kapak önce, en fazla 8 (zoom CDN tabanı)
        var cdnBase = await CdnHelper.BuildZoomUrlAsync(catalogDb, ct);
        var imageUrls = new Dictionary<Guid, List<string>>();
        await using (var cmd = mainDb.CreateCommand(
            """
            SELECT "ProductId", "FileName" FROM (
                SELECT DISTINCT ON ("ProductId", "FileName") "ProductId", "FileName",
                       "IsProductCover", "SortOrder"
                FROM catalog.product_images
                WHERE "ProductId" = ANY($1) AND NOT "IsDeleted"
            ) x ORDER BY "ProductId", "IsProductCover" DESC, "SortOrder"
            """))
        {
            cmd.Parameters.AddWithValue(productArr);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var pid = r.GetGuid(0);
                if (!imageUrls.TryGetValue(pid, out var list)) imageUrls[pid] = list = [];
                if (list.Count < 8) list.Add(cdnBase + r.GetString(1));
            }
        }

        // Referans DB: kategori özellikleri (aktif — required olsun olmasın; eşlenen opsiyoneller de gönderilir)
        var refAttrs = new Dictionary<string, List<RefAttr>>();
        var refDs = await refDb.GetAsync(ct);
        if (refDs is not null)
        {
            await using var cmd = refDs.CreateCommand(
                """
                SELECT category_external_id, attribute_external_id, is_variant_axis
                FROM mp_category_attributes
                WHERE marketplace=$1 AND category_external_id = ANY($2) AND removed_at IS NULL
                """);
            cmd.Parameters.AddWithValue(marketplace);
            cmd.Parameters.AddWithValue(categories);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var cat = r.GetString(0);
                if (!refAttrs.TryGetValue(cat, out var list)) refAttrs[cat] = list = [];
                list.Add(new RefAttr(r.GetString(1), r.GetBoolean(2)));
            }
        }

        var attrMappings = await db.MarketplaceAttributeMappings.AsNoTracking()
            .Where(m => m.Marketplace == marketplace && m.FirmPlatformId == null
                        && categories.Contains(m.MpCategoryExternalId) && m.Status != "broken")
            .ToDictionaryAsync(
                m => (m.MpCategoryExternalId, m.MpAttributeExternalId),
                m => (m.Strategy, m.AttributeTypeId, m.FixedValue), ct);

        var valueMappings = new Dictionary<(string, string, Guid), string>();
        foreach (var v in await db.MarketplaceValueMappings.AsNoTracking()
            .Where(v => v.Marketplace == marketplace && v.FirmPlatformId == null
                        && categories.Contains(v.MpCategoryExternalId) && v.Status != "broken"
                        && v.TargetExternalId != null)
            .ToListAsync(ct))
            valueMappings[(v.MpCategoryExternalId, v.MpAttributeExternalId, v.AttributeValueId)] = v.TargetExternalId!;

        var productMpValues = new Dictionary<(Guid, string, string), (string?, string?)>();
        foreach (var v in await db.MarketplaceProductAttributeValues.AsNoTracking()
            .Where(v => v.Marketplace == marketplace && v.FirmPlatformId == null
                        && products.Contains(v.ProductId))
            .ToListAsync(ct))
            productMpValues[(v.ProductId, v.MpCategoryExternalId, v.MpAttributeExternalId)] =
                (v.ValueExternalId, v.ValueText);

        var ownValues = new Dictionary<Guid, Dictionary<Guid, List<Guid>>>();
        var ownLiterals = new Dictionary<Guid, Dictionary<Guid, string>>();
        var usedValueIds = new HashSet<Guid>();
        await using (var cmd = mainDb.CreateCommand(
            """
            SELECT "ProductId", "AttributeTypeId", "AttributeValueId", "CustomValue"->>'tr'
            FROM catalog.product_attributes
            WHERE "ProductId" = ANY($1) AND NOT "IsDeleted"
            """))
        {
            cmd.Parameters.AddWithValue(productArr);
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
                    usedValueIds.Add(r.GetGuid(2));
                }
                else if (!r.IsDBNull(3))
                {
                    if (!ownLiterals.TryGetValue(pid, out var byType)) ownLiterals[pid] = byType = [];
                    byType[tid] = r.GetString(3);
                }
            }
        }

        var variantAttrs = new Dictionary<Guid, Dictionary<Guid, Guid>>();
        await using (var cmd = mainDb.CreateCommand(
            """
            SELECT "VariantId", "AttributeTypeId", "AttributeValueId"
            FROM catalog.product_variant_attributes
            WHERE "VariantId" = ANY($1) AND NOT "IsDeleted" AND "AttributeValueId" IS NOT NULL
            """))
        {
            cmd.Parameters.AddWithValue(variantArr);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var vid = r.GetGuid(0);
                if (!variantAttrs.TryGetValue(vid, out var byType)) variantAttrs[vid] = byType = [];
                byType[r.GetGuid(1)] = r.GetGuid(2);
            }
        }

        var valueLabels = new Dictionary<Guid, string>();
        if (usedValueIds.Count > 0)
        {
            await using var cmd = mainDb.CreateCommand(
                """
                SELECT "Id", COALESCE("NameI18n"->>'tr', '') FROM definition.attribute_values
                WHERE "Id" = ANY($1)
                """);
            cmd.Parameters.AddWithValue(usedValueIds.ToArray());
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) valueLabels[r.GetGuid(0)] = r.GetString(1);
        }

        return new BuildContext(stocks, taxRates, imageUrls, refAttrs, attrMappings, valueMappings,
            productMpValues, ownValues, ownLiterals, variantAttrs, valueLabels);
    }

    private static string Sha256(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
}
