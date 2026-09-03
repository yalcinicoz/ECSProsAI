using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace ECSPros.Api.Services.ErpSource;

/// <summary>
/// V3 ERP'yi kalıcı veri otoritesi kabul eder. Tüm yazılar Code/Barcode doğal anahtarlarıyla
/// idempotenttir; tanım eşleşmesi olmayan veri yanlış gruba/değere düşürülmez.
/// </summary>
public sealed class ErpSourceSyncService(
    NpgsqlDataSource dataSource,
    IErpSourceReader source,
    ErpSourceOptions options,
    ILogger<ErpSourceSyncService> logger)
{
    private (DateTime CreatedAt, string Code)? _productAttributeCursor;

    public bool IsConfigured => source.IsConfigured;

    public async Task<ErpSourceSyncReport> RefreshProductAsync(string productCode, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var detail = new StringBuilder();
        var code = productCode.Trim();
        if (!options.TargetedRefreshEnabled)
            return new(false, options.DryRun, "product-refresh", 0, "", "ERP hedefli ürün yenileme kapalı.", 0);
        if (!source.IsConfigured)
            return new(false, options.DryRun, "product-refresh", 0, "", "ERP kaynak bağlantısı yapılandırılmamış.", 0);
        if (code.Length == 0)
            return new(false, options.DryRun, "product-refresh", 0, "", "Ürün kodu boş olamaz.", 0);

        try
        {
            var snapshot = await source.ReadProductSnapshotAsync(code, ct);
            if (snapshot is null)
                return new(false, options.DryRun, "product-refresh", 0, "", $"V3 ürün bulunamadı: {code}", (int)sw.ElapsedMilliseconds);

            await using var pg = await dataSource.OpenConnectionAsync(ct);
            var groups = await LoadGroupsAsync(pg, ct);
            var attrTypes = await LoadAttributeTypesAsync(pg, ct);
            var attrValues = await LoadAttributeValuesAsync(pg, ct);
            var channelPlatforms = await LoadConfiguredPlatformsAsync(pg, ct);
            var existing = await FindProductAsync(pg, snapshot.Product.Code, ct);
            var groupId = ResolveGroup(snapshot.Product.ProductGroupName, groups);
            if (existing is null && groupId is null)
                return new(false, options.DryRun, "product-refresh", 0, "",
                    $"ERP grubu eşleşmedi: {snapshot.Product.ProductGroupName}", (int)sw.ElapsedMilliseconds);

            var supplier = await ResolveSupplierAsync(pg, snapshot.Supplier, detail, ct);
            if (supplier.BlockingError is not null)
                return new(false, options.DryRun, "product-refresh", 0, detail.ToString(),
                    supplier.BlockingError, (int)sw.ElapsedMilliseconds);

            if (!options.DryRun)
            {
                await EnsureVariantDefinitionValuesAsync(pg, null, snapshot.Variants, attrTypes, detail, ct);
                await EnsureProductDefinitionValuesAsync(pg, null, snapshot.Attributes, attrTypes, detail, ct);
                attrValues = await LoadAttributeValuesAsync(pg, ct);
            }
            var variantsComplete = ValidateVariantAttributes(
                snapshot.Variants.SelectMany(x => x.Attributes).ToArray(), attrTypes, attrValues, detail);
            var productAttrsComplete = ValidateProductAttributes(
                snapshot.Attributes, attrTypes, attrValues, snapshot.Product.Code, detail);
            if (!variantsComplete || !productAttrsComplete)
                return new(false, options.DryRun, "product-refresh", 0, detail.ToString(),
                    "ERP katalog tanım eşleşmeleri eksik.", (int)sw.ElapsedMilliseconds);

            if (options.DryRun)
            {
                detail.AppendLine($"[ERP HEDEFLİ] ürün={code}; varyant={snapshot.Variants.Count}; dry-run, yazılmadı.");
                return new(true, true, "product-refresh", 1, detail.ToString(), null, (int)sw.ElapsedMilliseconds);
            }

            await using var tx = await pg.BeginTransactionAsync(ct);
            try
            {
                await ExecAsync(pg, tx, "SELECT pg_advisory_xact_lock(hashtext(@key))", ct,
                    ("key", $"erp-product:{snapshot.Product.Code.ToLowerInvariant()}"));
                existing = await FindProductAsync(pg, tx, snapshot.Product.Code, ct);
                Guid productId;
                bool changed;
                if (existing is null)
                {
                    productId = Guid.NewGuid();
                    await InsertProductAsync(pg, tx, productId, groupId!.Value, snapshot.Product, ct);
                    changed = true;
                }
                else
                {
                    productId = existing.Value.Id;
                    changed = await UpdateProductAsync(pg, tx, productId, groupId, snapshot.Product, ct) > 0;
                }

                changed |= await ApplySupplierAsync(pg, tx, productId, supplier.AccountId, ct) > 0;
                foreach (var variant in snapshot.Variants)
                {
                    var upsert = await UpsertVariantAsync(pg, tx, productId, variant, detail, ct);
                    if (upsert.Id is null)
                        throw new InvalidOperationException($"ERP barkodu başka ürüne bağlı: {variant.Barcode}.");
                    var attrs = await ReplaceVariantAttributesAsync(
                        pg, tx, upsert.Id.Value, variant.Attributes, attrTypes, attrValues, detail, ct);
                    if (!attrs.Complete) throw new InvalidOperationException($"Varyant eşleşmesi değişti: {variant.Barcode}.");
                    changed |= upsert.Changed || attrs.Changed;
                }
                var productAttrs = await ReplaceProductAttributesAsync(
                    pg, tx, productId, snapshot.Attributes, attrTypes, attrValues, detail, ct);
                if (!productAttrs.Complete) throw new InvalidOperationException($"Ürün eşleşmesi değişti: {code}.");
                changed |= productAttrs.Changed;
                if (existing is null)
                    foreach (var platformId in channelPlatforms.Values)
                        changed |= await EnsureChannelProductAsync(pg, tx, productId, platformId, ct) > 0;

                await tx.CommitAsync(ct);
                detail.AppendLine($"[ERP HEDEFLİ] ürün={code}; varyant={snapshot.Variants.Count}; değişti={changed}.");
                return new(true, false, "product-refresh", changed ? 1 : 0, detail.ToString(), null, (int)sw.ElapsedMilliseconds);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ERP hedefli ürün yenileme başarısız: {ProductCode}", code);
            return Fail("product-refresh", detail, ex, sw);
        }
    }

    public async Task<ErpSourceSyncReport> RefreshProductByBarcodeAsync(string barcode, CancellationToken ct)
    {
        var value = barcode.Trim();
        if (!options.TargetedRefreshEnabled)
            return new(false, options.DryRun, "product-refresh", 0, "", "ERP hedefli ürün yenileme kapalı.", 0);
        if (!source.IsConfigured)
            return new(false, options.DryRun, "product-refresh", 0, "", "ERP kaynak bağlantısı yapılandırılmamış.", 0);
        if (value.Length == 0)
            return new(false, options.DryRun, "product-refresh", 0, "", "Barkod boş olamaz.", 0);

        try
        {
            var code = await source.ResolveProductCodeByBarcodeAsync(value, ct);
            return string.IsNullOrWhiteSpace(code)
                ? new(false, options.DryRun, "product-refresh", 0, "", $"V3 barkod bulunamadı: {value}", 0)
                : await RefreshProductAsync(code, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ERP barkoduyla hedefli ürün yenileme başarısız: {Barcode}", value);
            return new(false, options.DryRun, "product-refresh", 0, "", ex.Message, 0);
        }
    }

    public async Task<ErpSourceSyncReport> SyncCatalogAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var detail = new StringBuilder();
        try
        {
            var startedAt = DateTime.UtcNow;
            await using var pg = await dataSource.OpenConnectionAsync(ct);
            var since = await GetSinceAsync(pg, "catalog", ct);
            var changedProducts = await source.ReadProductsAsync(since, ct);
            var productCodes = changedProducts.Select(x => x.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var reconciliation = options.ProductAttributeReconciliationEnabled
                ? await ReconcileProductAttributesAsync(pg, detail, ct)
                : (Candidates: 0, SourceMatched: 0, Changed: 0);
            detail.AppendLine($"[ERP KATALOG] başlangıç={since:O}; değişen ürün={changedProducts.Count}; " +
                              $"özellik uzlaştırma aday={reconciliation.Candidates}, kaynak eşleşen={reconciliation.SourceMatched}, " +
                              $"güncellenen={reconciliation.Changed}.");
            if (productCodes.Count == 0)
            {
                if (!options.DryRun)
                    await SaveCheckpointAsync(pg, "catalog", startedAt, null, ct);
                return Ok("catalog", reconciliation.Changed, detail, sw);
            }

            var groups = await LoadGroupsAsync(pg, ct);
            var attrTypes = await LoadAttributeTypesAsync(pg, ct);
            var attrValues = await LoadAttributeValuesAsync(pg, ct);
            var channelPlatforms = await LoadConfiguredPlatformsAsync(pg, ct);
            var missingPlatforms = options.ChannelPrices.Keys
                .Where(x => !channelPlatforms.ContainsKey(x))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (missingPlatforms.Length > 0)
                throw new InvalidOperationException(
                    $"ERP kanal fiyat eşlemesindeki platformlar hedefte yok: {string.Join(", ", missingPlatforms)}.");
            int changed = reconciliation.Changed, newProducts = 0, variants = 0, skipped = 0;
            bool blockingMappingError = false;

            foreach (var productCode in productCodes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();
                var snapshot = await source.ReadProductSnapshotAsync(productCode, ct);
                if (snapshot is null)
                {
                    skipped++;
                    blockingMappingError = true;
                    detail.AppendLine($"! ATLANDI ürün {productCode}: hedefli V3 snapshot bulunamadı.");
                    continue;
                }

                var currentProduct = snapshot.Product;
                var sourceVariants = snapshot.Variants;
                var sourceAttributes = snapshot.Attributes;
                var existing = await FindProductAsync(pg, currentProduct.Code, ct);
                var groupId = ResolveGroup(currentProduct.ProductGroupName, groups);
                if (existing is null && groupId is null)
                {
                    skipped++;
                    blockingMappingError = true;
                    detail.AppendLine($"! ATLANDI yeni ürün {currentProduct.Code}: ERP grubu '{currentProduct.ProductGroupName}' eşleşmedi.");
                    continue;
                }

                var supplier = await ResolveSupplierAsync(pg, snapshot.Supplier, detail, ct);
                if (supplier.BlockingError is not null)
                {
                    skipped++;
                    blockingMappingError = true;
                    detail.AppendLine($"! ATLANDI ürün {currentProduct.Code}: {supplier.BlockingError}");
                    continue;
                }

                if (!options.DryRun)
                {
                    await EnsureVariantDefinitionValuesAsync(pg, null, sourceVariants, attrTypes, detail, ct);
                    await EnsureProductDefinitionValuesAsync(pg, null, sourceAttributes, attrTypes, detail, ct);
                    attrValues = await LoadAttributeValuesAsync(pg, ct);
                }

                var variantMappingsComplete = ValidateVariantAttributes(
                    sourceVariants.SelectMany(x => x.Attributes).ToArray(), attrTypes, attrValues, detail);
                var productMappingsComplete = ValidateProductAttributes(
                    sourceAttributes, attrTypes, attrValues, currentProduct.Code, detail);
                if (!variantMappingsComplete || !productMappingsComplete)
                {
                    blockingMappingError = true;
                    skipped++;
                    continue;
                }

                if (options.DryRun)
                {
                    changed++;
                    if (existing is null) newProducts++;
                    variants += sourceVariants.Count;
                    continue;
                }

                await using var tx = await pg.BeginTransactionAsync(ct);
                try
                {
                    Guid productId;
                    bool productChanged;
                    if (existing is null)
                    {
                        productId = Guid.NewGuid();
                        await InsertProductAsync(pg, tx, productId, groupId!.Value, currentProduct, ct);
                        newProducts++;
                        productChanged = true;
                    }
                    else
                    {
                        productId = existing.Value.Id;
                        productChanged = await UpdateProductAsync(pg, tx, productId, groupId, currentProduct, ct) > 0;
                    }

                    productChanged |= await ApplySupplierAsync(
                        pg, tx, productId, supplier.AccountId, ct) > 0;

                    foreach (var variant in sourceVariants)
                    {
                        var variantResult = await UpsertVariantAsync(pg, tx, productId, variant, detail, ct);
                        if (variantResult.Id is null)
                        {
                            blockingMappingError = true;
                            throw new InvalidOperationException(
                                $"ERP barkodu başka ürüne bağlı: {variant.Barcode}.");
                        }
                        variants++;
                        var variantAttributes = await ReplaceVariantAttributesAsync(
                            pg, tx, variantResult.Id.Value, variant.Attributes, attrTypes, attrValues, detail, ct);
                        if (!variantAttributes.Complete)
                            throw new InvalidOperationException(
                                $"ERP varyant attribute eşleşmesi doğrulama sonrasında değişti: {variant.Barcode}.");
                        productChanged |= variantResult.Changed || variantAttributes.Changed;
                    }

                    var productAttributes = await ReplaceProductAttributesAsync(
                        pg, tx, productId, sourceAttributes, attrTypes, attrValues, detail, ct);
                    if (!productAttributes.Complete)
                        throw new InvalidOperationException(
                            $"ERP ürün attribute eşleşmesi doğrulama sonrasında değişti: {currentProduct.Code}.");
                    productChanged |= productAttributes.Changed;

                    // Yeni ERP ürünü kanala da bağlanır. Mevcut ürünlerdeki personel kapsam/
                    // aktiflik kararlarına dokunulmaz; fiyat dilimi varyant satırlarını idempotent
                    // olarak oluşturur veya yalnız fiyat alanlarını günceller.
                    if (existing is null)
                    {
                        foreach (var platformId in channelPlatforms.Values)
                            productChanged |= await EnsureChannelProductAsync(
                                pg, tx, productId, platformId, ct) > 0;
                    }

                    await tx.CommitAsync(ct);
                    if (productChanged) changed++;
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }
            }

            detail.AppendLine($"[ERP KATALOG] işlenen={changed}, yeni={newProducts}, varyant={variants}, atlanan={skipped}.");
            if (!options.DryRun && !blockingMappingError)
                await SaveCheckpointAsync(pg, "catalog", startedAt, null, ct);
            else if (blockingMappingError)
                detail.AppendLine("! Checkpoint ilerletilmedi: eşleşmeyen kayıtlar düzeltildikten sonra yeniden okunacak.");

            return new(true, options.DryRun, "catalog", changed, detail.ToString(),
                blockingMappingError ? "ERP katalog tanım eşleşmeleri eksik." : null, (int)sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ERP kaynak katalog senkronu başarısız");
            return Fail("catalog", detail, ex, sw);
        }
    }

    private async Task<IReadOnlyList<ProductAttributeCandidate>> ReadProductAttributeCandidatesAsync(
        NpgsqlConnection pg, int batchSize, CancellationToken ct)
    {
        var result = await ReadProductAttributeCandidatesAfterAsync(
            pg, _productAttributeCursor, batchSize, ct);
        if (result.Count == 0 && _productAttributeCursor is not null)
        {
            _productAttributeCursor = null;
            result = await ReadProductAttributeCandidatesAfterAsync(pg, null, batchSize, ct);
        }
        if (result.Count > 0)
            _productAttributeCursor = (result[^1].CreatedAt, result[^1].Code);
        return result;
    }

    private async Task<(int Candidates, int SourceMatched, int Changed)> ReconcileProductAttributesAsync(
        NpgsqlConnection pg, StringBuilder detail, CancellationToken ct)
    {
        var candidates = await ReadProductAttributeCandidatesAsync(pg, options.ProductAttributeBatchSize, ct);
        if (candidates.Count == 0) return (0, 0, 0);
        var codes = candidates.Select(x => x.Code).ToArray();

        IReadOnlyDictionary<string, IReadOnlyList<ErpProductAttributeRow>> attributes;
        if (source is IErpProductAttributeBatchReader batchReader)
        {
            attributes = await batchReader.ReadProductAttributesAsync(codes, ct);
        }
        else
        {
            var fallback = new Dictionary<string, IReadOnlyList<ErpProductAttributeRow>>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in codes)
            {
                var rows = await source.ReadProductAttributesAsync(code, ct);
                if (rows.Count > 0) fallback[code] = rows;
            }
            attributes = fallback;
        }

        var sourceMatched = attributes.Count(x => x.Value.Count > 0);
        if (options.DryRun) return (candidates.Count, sourceMatched, sourceMatched);

        var attrTypes = await LoadAttributeTypesAsync(pg, ct);
        await EnsureProductDefinitionValuesAsync(
            pg, null, attributes.Values.SelectMany(x => x).ToArray(), attrTypes, detail, ct);
        var attrValues = await LoadAttributeValuesAsync(pg, ct);
        var changed = 0;
        foreach (var (code, rows) in attributes)
        {
            if (rows.Count == 0) continue;
            var product = await FindProductAsync(pg, code, ct);
            if (product is null) continue;
            if (!ValidateProductAttributes(rows, attrTypes, attrValues, code, detail)) continue;
            await using var tx = await pg.BeginTransactionAsync(ct);
            try
            {
                await ExecAsync(pg, tx, "SELECT pg_advisory_xact_lock(hashtext(@key))", ct,
                    ("key", $"erp-product:{code.ToLowerInvariant()}"));
                var result = await ReplaceProductAttributesAsync(
                    pg, tx, product.Value.Id, rows, attrTypes, attrValues, detail, ct);
                if (result.Complete && result.Changed) changed++;
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
        return (candidates.Count, sourceMatched, changed);
    }

    private sealed record ProductAttributeCandidate(string Code, DateTime CreatedAt);

    private static async Task<IReadOnlyList<ProductAttributeCandidate>> ReadProductAttributeCandidatesAfterAsync(
        NpgsqlConnection pg, (DateTime CreatedAt, string Code)? cursor, int batchSize, CancellationToken ct)
    {
        var cursorFilter = cursor.HasValue
            ? "AND (\"CreatedAt\"<@cursorCreated OR (\"CreatedAt\"=@cursorCreated AND \"Code\"<@cursorCode))"
            : string.Empty;
        await using var command = new NpgsqlCommand($$"""
            SELECT "Code","CreatedAt"
              FROM catalog.products
             WHERE "IsDeleted"=false
               {{cursorFilter}}
             ORDER BY "CreatedAt" DESC,"Code" DESC
             LIMIT @limit
            """, pg);
        if (cursor.HasValue)
        {
            command.Parameters.AddWithValue("cursorCreated", cursor.Value.CreatedAt);
            command.Parameters.AddWithValue("cursorCode", cursor.Value.Code);
        }
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = batchSize;
        var result = new List<ProductAttributeCandidate>(batchSize);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetString(0), reader.GetDateTime(1)));
        return result;
    }

    public async Task<ErpSourceSyncReport> SyncPricesAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var detail = new StringBuilder();
        try
        {
            var startedAt = DateTime.UtcNow;
            await using var pg = await dataSource.OpenConnectionAsync(ct);
            var since = await GetSinceAsync(pg, "price", ct);
            var products = await source.ReadProductsAsync(since, ct);
            int changed = 0, missing = 0, channelRows = 0;
            foreach (var p in products)
            {
                var found = await FindProductAsync(pg, p.Code, ct);
                if (found is null) { missing++; continue; }
                if (options.DryRun) { changed++; continue; }

                await using var tx = await pg.BeginTransactionAsync(ct);
                var productRows = await ExecAsync(pg, tx, """
                    UPDATE catalog.products SET "BasePrice"=@price, "BaseCost"=@cost, "TaxRate"=@tax, "UpdatedAt"=now()
                    WHERE "Id"=@id AND "IsDeleted"=false AND (
                      "BasePrice" IS DISTINCT FROM @price OR "BaseCost" IS DISTINCT FROM @cost OR "TaxRate" IS DISTINCT FROM @tax)
                    """, ct, ("id", found.Value.Id), ("price", p.BasePrice),
                    ("cost", (object?)p.BaseCost ?? DBNull.Value), ("tax", p.TaxRate));
                var productChanged = productRows > 0;

                foreach (var (platformCode, map) in options.ChannelPrices)
                {
                    if (!p.Values.TryGetValue(map.PriceColumn, out var price)) continue;
                    p.Values.TryGetValue(map.CompareAtPriceColumn, out var compareAt);
                    var affected = await ExecAsync(pg, tx, """
                        INSERT INTO storefront.channel_variants AS existing
                          ("Id","FirmPlatformId","VariantId","PriceType","Price","CompareAtPrice","IsActive","CreatedAt","IsDeleted")
                        SELECT gen_random_uuid(),fp."Id",v."Id",'erp',@price,@compare,true,now(),false
                        FROM catalog.product_variants v
                        JOIN core.core_firm_platforms fp ON fp."Code"=@platform AND NOT fp."IsDeleted"
                        WHERE v."ProductId"=@productId AND NOT v."IsDeleted"
                        ON CONFLICT ("FirmPlatformId","VariantId") DO UPDATE SET
                          "Price"=EXCLUDED."Price", "CompareAtPrice"=EXCLUDED."CompareAtPrice",
                          "PriceType"='erp', "IsDeleted"=false, "UpdatedAt"=now()
                        WHERE existing."Price" IS DISTINCT FROM EXCLUDED."Price"
                           OR existing."CompareAtPrice" IS DISTINCT FROM EXCLUDED."CompareAtPrice"
                           OR existing."PriceType" IS DISTINCT FROM 'erp'
                           OR existing."IsDeleted"
                        """, ct, ("price", (object?)price ?? DBNull.Value),
                        ("compare", NormalizeCompareAt(price, compareAt)),
                        ("productId", found.Value.Id), ("platform", platformCode));
                    channelRows += affected;
                    productChanged |= affected > 0;
                }
                await tx.CommitAsync(ct);
                if (productChanged) changed++;
            }
            detail.AppendLine($"[ERP FİYAT] kaynak={products.Count}, ürün={changed}, katalogda-yok={missing}, kanal-varyant={channelRows}.");
            if (!options.DryRun && missing == 0)
                await SaveCheckpointAsync(pg, "price", startedAt, null, ct);
            else if (!options.DryRun)
                detail.AppendLine("! Fiyat checkpoint ilerletilmedi: katalogda bulunmayan ERP ürünleri yeniden denenecek.");
            return new(true, options.DryRun, "price", changed, detail.ToString(),
                !options.DryRun && missing > 0 ? $"ERP fiyat diliminde katalogda bulunmayan {missing} ürün var." : null,
                (int)sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ERP kaynak fiyat senkronu başarısız");
            return Fail("price", detail, ex, sw);
        }
    }

    private async Task<DateTime> GetSinceAsync(NpgsqlConnection pg, string slice, CancellationToken ct)
    {
        var last = await ScalarNullableAsync<DateTime>(pg,
            "SELECT \"WatermarkUtc\" FROM integration.erp_sync_checkpoints WHERE \"Slice\"=@slice AND \"IsDeleted\"=false",
            ct, ("slice", slice));
        var since = last ?? options.InitialSinceUtc;
        return DateTime.SpecifyKind(since, DateTimeKind.Utc).AddMinutes(-Math.Max(0, options.OverlapMinutes));
    }

    private static async Task SaveCheckpointAsync(NpgsqlConnection pg, string slice, DateTime watermark,
        string? error, CancellationToken ct)
    {
        await ExecAsync(pg, null, """
            INSERT INTO integration.erp_sync_checkpoints
              ("Id","Slice","WatermarkUtc","LastError","CreatedAt","IsDeleted")
            VALUES (gen_random_uuid(),@slice,@watermark,@error,now(),false)
            ON CONFLICT ("Slice") WHERE "IsDeleted"=false DO UPDATE SET
              "WatermarkUtc"=EXCLUDED."WatermarkUtc", "LastError"=EXCLUDED."LastError", "UpdatedAt"=now()
            """, ct, ("slice", slice), ("watermark", watermark), ("error", (object?)error ?? DBNull.Value));
    }

    private Guid? ResolveGroup(string? sourceName, GroupMaps groups)
    {
        if (string.IsNullOrWhiteSpace(sourceName)) return null;
        string normalized = Normalize(sourceName);
        var configured = options.ProductGroupCodes.FirstOrDefault(x => Normalize(x.Key) == normalized);
        if (!string.IsNullOrWhiteSpace(configured.Value) && groups.ByCode.TryGetValue(configured.Value, out var mapped))
            return mapped;
        return groups.ByNormalizedName.TryGetValue(normalized, out var exact) ? exact : null;
    }

    private static async Task<GroupMaps> LoadGroupsAsync(NpgsqlConnection pg, CancellationToken ct)
    {
        var byCode = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var candidates = new Dictionary<string, List<Guid>>();
        await using var cmd = new NpgsqlCommand("SELECT \"Id\",\"Code\",COALESCE(\"NameI18n\"->>'tr','') FROM definition.product_groups WHERE \"IsDeleted\"=false", pg);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var id = r.GetGuid(0); byCode[r.GetString(1)] = id;
            var name = Normalize(r.GetString(2));
            if (!candidates.TryGetValue(name, out var list)) candidates[name] = list = [];
            list.Add(id);
        }
        return new(byCode, candidates.Where(x => x.Value.Count == 1).ToDictionary(x => x.Key, x => x.Value[0]));
    }

    private static async Task<Dictionary<string, Guid>> LoadAttributeTypesAsync(NpgsqlConnection pg, CancellationToken ct)
    {
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = new NpgsqlCommand("SELECT \"Code\",\"Id\" FROM definition.attribute_types WHERE \"IsDeleted\"=false", pg);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) map[r.GetString(0)] = r.GetGuid(1);
        return map;
    }

    private static async Task<Dictionary<(Guid TypeId, string Value), Guid>> LoadAttributeValuesAsync(NpgsqlConnection pg, CancellationToken ct)
    {
        var map = new Dictionary<(Guid, string), Guid>();
        await using var cmd = new NpgsqlCommand("SELECT \"AttributeTypeId\",COALESCE(\"NameI18n\"->>'tr',''),\"Id\" FROM definition.attribute_values WHERE \"IsDeleted\"=false", pg);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) map[(r.GetGuid(0), Normalize(r.GetString(1)))] = r.GetGuid(2);
        return map;
    }

    private async Task<Dictionary<string, Guid>> LoadConfiguredPlatformsAsync(
        NpgsqlConnection pg, CancellationToken ct)
    {
        var requested = options.ChannelPrices.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        if (requested.Count == 0) return result;

        await using var command = new NpgsqlCommand(
            "SELECT \"Code\",\"Id\" FROM core.core_firm_platforms WHERE NOT \"IsDeleted\"", pg);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var code = reader.GetString(0);
            if (requested.Contains(code)) result[code] = reader.GetGuid(1);
        }
        return result;
    }

    private static Task<(Guid Id, Guid GroupId)?> FindProductAsync(NpgsqlConnection pg, string code, CancellationToken ct)
        => FindProductAsync(pg, null, code, ct);

    private static async Task<(Guid Id, Guid GroupId)?> FindProductAsync(
        NpgsqlConnection pg, NpgsqlTransaction? tx, string code, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("SELECT \"Id\",\"ProductGroupId\" FROM catalog.products WHERE \"Code\"=@code AND \"IsDeleted\"=false", pg, tx);
        cmd.Parameters.AddWithValue("code", code);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? (r.GetGuid(0), r.GetGuid(1)) : null;
    }

    private static Task<int> InsertProductAsync(NpgsqlConnection pg, NpgsqlTransaction tx, Guid id, Guid groupId,
        ErpProductRow p, CancellationToken ct) => ExecAsync(pg, tx, """
        INSERT INTO catalog.products
          ("Id","ProductGroupId","Code","NameI18n","BasePrice","BaseCost","TaxRate","IsSaleOpen","SourceType","Tags","CreatedAt","UpdatedAt","IsDeleted")
        VALUES (@id,@group,@code,@name::jsonb,@price,@cost,@tax,@sale,'own','[]'::jsonb,@created,@updated,false)
        """, ct, ("id", id), ("group", groupId), ("code", p.Code), ("name", I18n(p.InternetName ?? p.Name)),
        ("price", p.BasePrice), ("cost", (object?)p.BaseCost ?? DBNull.Value), ("tax", p.TaxRate),
        ("sale", p.IsSaleOpen), ("created", p.CreatedAtUtc ?? DateTime.UtcNow),
        ("updated", (object?)p.UpdatedAtUtc ?? DBNull.Value));

    private static Task<int> EnsureChannelProductAsync(
        NpgsqlConnection pg, NpgsqlTransaction tx, Guid productId, Guid platformId, CancellationToken ct)
        => ExecAsync(pg, tx, """
            INSERT INTO storefront.channel_products
              ("Id","FirmPlatformId","ProductId","IsActive","SortOrder","InScope","ScopeSource","IsExcluded","CreatedAt","IsDeleted")
            VALUES (gen_random_uuid(),@platform,@product,true,0,true,'erp',false,now(),false)
            ON CONFLICT ("FirmPlatformId","ProductId") DO NOTHING
            """, ct, ("platform", platformId), ("product", productId));

    private static Task<int> UpdateProductAsync(NpgsqlConnection pg, NpgsqlTransaction tx, Guid id, Guid? groupId,
        ErpProductRow p, CancellationToken ct) => groupId.HasValue
        ? ExecAsync(pg, tx, """
            UPDATE catalog.products SET "ProductGroupId"=@group, "NameI18n"=@name::jsonb,
              "TaxRate"=@tax, "IsSaleOpen"=@sale, "UpdatedAt"=now()
            WHERE "Id"=@id AND ("ProductGroupId" IS DISTINCT FROM @group OR
              "NameI18n" IS DISTINCT FROM @name::jsonb OR "TaxRate" IS DISTINCT FROM @tax OR
              "IsSaleOpen" IS DISTINCT FROM @sale)
            """, ct, ("id", id), ("group", groupId.Value),
            ("name", I18n(p.InternetName ?? p.Name)), ("tax", p.TaxRate), ("sale", p.IsSaleOpen))
        : ExecAsync(pg, tx, """
            UPDATE catalog.products SET "NameI18n"=@name::jsonb,
              "TaxRate"=@tax, "IsSaleOpen"=@sale, "UpdatedAt"=now()
            WHERE "Id"=@id AND ("NameI18n" IS DISTINCT FROM @name::jsonb OR
              "TaxRate" IS DISTINCT FROM @tax OR "IsSaleOpen" IS DISTINCT FROM @sale)
            """, ct, ("id", id), ("name", I18n(p.InternetName ?? p.Name)),
            ("tax", p.TaxRate), ("sale", p.IsSaleOpen));

    private static async Task<(Guid? Id, bool Changed)> UpsertVariantAsync(NpgsqlConnection pg, NpgsqlTransaction tx, Guid productId,
        ErpVariantRow v, StringBuilder detail, CancellationToken ct)
    {
        await using var find = new NpgsqlCommand("SELECT \"Id\",\"ProductId\" FROM catalog.product_variants WHERE \"Barcode\"=@barcode AND \"IsDeleted\"=false", pg, tx);
        find.Parameters.AddWithValue("barcode", v.Barcode);
        Guid? id = null, owner = null;
        await using (var r = await find.ExecuteReaderAsync(ct))
            if (await r.ReadAsync(ct)) { id = r.GetGuid(0); owner = r.GetGuid(1); }
        if (id.HasValue && owner != productId)
        {
            detail.AppendLine($"! Barkod çakışması {v.Barcode}: başka ürüne bağlı; taşınmadı.");
            return (null, false);
        }
        if (id.HasValue)
        {
            var affected = await ExecAsync(pg, tx, "UPDATE catalog.product_variants SET \"IsActive\"=true,\"UpdatedAt\"=now() WHERE \"Id\"=@id AND NOT \"IsActive\"",
                ct, ("id", id.Value));
            return (id, affected > 0);
        }
        id = Guid.NewGuid();
        // Admin AddProductVariants ile aynı sözleşme: yeni varyant ana ürünün fiyat ve
        // maliyetini aynen devralır. Kanal/kardeş varyanttan fiyat tahmin edilmez;
        // ana ürün fiyatı 0 ise 0 kalır ve normal pozitif-fiyat filtreleri devrededir.
        var inserted = await ExecAsync(pg, tx, """
            INSERT INTO catalog.product_variants
              ("Id","ProductId","Sku","Barcode","BasePrice","BaseCost","IsActive","CreatedAt","UpdatedAt","IsDeleted")
            SELECT @id,p."Id",@sku,@barcode,p."BasePrice",p."BaseCost",true,@created,@updated,false
              FROM catalog.products p
             WHERE p."Id"=@product AND NOT p."IsDeleted"
            """, ct, ("id", id.Value), ("product", productId), ("sku", v.Barcode), ("barcode", v.Barcode),
            ("created", v.CreatedAtUtc ?? DateTime.UtcNow), ("updated", (object?)v.UpdatedAtUtc ?? DBNull.Value));
        if (inserted != 1)
            throw new InvalidOperationException($"ERP varyantının hedef ürünü bulunamadı: {v.Barcode}.");
        return (id, true);
    }

    private async Task<AttributeReplaceResult> ReplaceVariantAttributesAsync(NpgsqlConnection pg, NpgsqlTransaction tx, Guid variantId,
        IReadOnlyList<ErpVariantAttributeRow> sourceAttrs, IReadOnlyDictionary<string, Guid> types,
        IReadOnlyDictionary<(Guid TypeId, string Value), Guid> values, StringBuilder detail, CancellationToken ct)
    {
        bool complete = true, changed = false;
        foreach (var group in sourceAttrs.GroupBy(x => x.TypeId))
        {
            if (!options.VariantAttributeTypeCodes.TryGetValue(group.Key, out var code) || !types.TryGetValue(code, out var typeId))
            {
                detail.AppendLine($"! Varyant tip eşleşmesi yok: ERP typeId={group.Key}."); complete = false; continue;
            }
            var resolved = group.Select(x => (Source: x, Id: values.GetValueOrDefault((typeId, Normalize(x.Value)))))
                .Where(x => x.Id != Guid.Empty).ToList();
            if (resolved.Count != group.Count())
            {
                foreach (var x in group.Where(x => !values.ContainsKey((typeId, Normalize(x.Value)))))
                    detail.AppendLine($"! Tanım değeri yok: {code}='{x.Value}'.");
                complete = false; continue;
            }
            var desired = resolved.Select(x => x.Id).ToHashSet();
            var existing = await ReadAttributeValueIdsAsync(pg, tx,
                "SELECT \"AttributeValueId\" FROM catalog.product_variant_attributes WHERE \"VariantId\"=@id AND \"AttributeTypeId\"=@type AND NOT \"IsDeleted\"",
                variantId, typeId, ct);
            if (existing.SetEquals(desired)) continue;
            await ExecAsync(pg, tx, "DELETE FROM catalog.product_variant_attributes WHERE \"VariantId\"=@id AND \"AttributeTypeId\"=@type",
                ct, ("id", variantId), ("type", typeId));
            foreach (var x in resolved)
                await ExecAsync(pg, tx, """
                    INSERT INTO catalog.product_variant_attributes
                      ("Id","VariantId","AttributeTypeId","AttributeValueId","CreatedAt","IsDeleted")
                    VALUES (gen_random_uuid(),@variant,@type,@value,now(),false)
                    ON CONFLICT ("VariantId","AttributeTypeId","AttributeValueId") DO NOTHING
                    """, ct, ("variant", variantId), ("type", typeId), ("value", x.Id));
            changed = true;
        }
        return new(complete, changed);
    }

    private bool ValidateVariantAttributes(IReadOnlyList<ErpVariantAttributeRow> sourceAttrs,
        IReadOnlyDictionary<string, Guid> types,
        IReadOnlyDictionary<(Guid TypeId, string Value), Guid> values,
        StringBuilder detail)
    {
        bool complete = true;
        foreach (var group in sourceAttrs.GroupBy(x => x.TypeId))
        {
            if (!options.VariantAttributeTypeCodes.TryGetValue(group.Key, out var code) ||
                !types.TryGetValue(code, out var typeId))
            {
                detail.AppendLine($"! Varyant tip eşleşmesi yok: ERP typeId={group.Key}.");
                complete = false;
                continue;
            }

            foreach (var source in group)
            {
                if (values.ContainsKey((typeId, Normalize(source.Value)))) continue;
                if (options.AutoCreateColorValues && code.Equals("renk", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(source.SourceCode)) continue;
                detail.AppendLine($"! Tanım değeri yok: {code}='{source.Value}'.");
                complete = false;
            }
        }
        return complete;
    }

    private async Task<AttributeReplaceResult> ReplaceProductAttributesAsync(NpgsqlConnection pg, NpgsqlTransaction tx, Guid productId,
        IReadOnlyList<ErpProductAttributeRow> sourceAttrs, IReadOnlyDictionary<string, Guid> types,
        IReadOnlyDictionary<(Guid TypeId, string Value), Guid> values, StringBuilder detail, CancellationToken ct)
    {
        bool complete = true, changed = false;
        foreach (var source in sourceAttrs)
        {
            if (!options.ProductAttributeTypeCodes.ContainsKey(source.KeywordId) &&
                !IsIgnoredProductAttributeType(source.KeywordId))
            {
                detail.AppendLine($"! Ürün attribute tip eşleşmesi yok: ERP keywordId={source.KeywordId}.");
                complete = false;
            }
        }
        foreach (var group in sourceAttrs
                     .Where(x => options.ProductAttributeTypeCodes.ContainsKey(x.KeywordId))
                     .GroupBy(x => options.ProductAttributeTypeCodes[x.KeywordId], StringComparer.OrdinalIgnoreCase))
        {
            var code = group.Key;
            if (!types.TryGetValue(code, out var typeId)) { detail.AppendLine($"! Attribute type yok: {code}."); complete = false; continue; }
            var ids = group.Select(x => values.GetValueOrDefault((typeId, Normalize(ResolveProductAttributeValue(code, x.Value)))))
                .Where(x => x != Guid.Empty).Distinct().ToList();
            if (ids.Count != group.Select(x => Normalize(ResolveProductAttributeValue(code, x.Value))).Distinct().Count())
            {
                foreach (var source in group.Where(x => !values.ContainsKey(
                             (typeId, Normalize(ResolveProductAttributeValue(code, x.Value))))))
                    detail.AppendLine($"! Tanım değeri yok: {code}='{source.Value}' (ERP keywordId={source.KeywordId}, ürün={productId}).");
                complete = false;
                continue;
            }
            var desired = ids.ToHashSet();
            var existing = await ReadAttributeValueIdsAsync(pg, tx,
                "SELECT \"AttributeValueId\" FROM catalog.product_attributes WHERE \"ProductId\"=@id AND \"AttributeTypeId\"=@type AND \"AttributeValueId\" IS NOT NULL AND NOT \"IsDeleted\"",
                productId, typeId, ct);
            if (existing.SetEquals(desired)) continue;
            await ExecAsync(pg, tx, "DELETE FROM catalog.product_attributes WHERE \"ProductId\"=@id AND \"AttributeTypeId\"=@type",
                ct, ("id", productId), ("type", typeId));
            foreach (var valueId in ids)
                await ExecAsync(pg, tx, """
                    INSERT INTO catalog.product_attributes
                      ("Id","ProductId","AttributeTypeId","AttributeValueId","CreatedAt","IsDeleted")
                    VALUES (gen_random_uuid(),@product,@type,@value,now(),false)
                    ON CONFLICT ("ProductId","AttributeTypeId","AttributeValueId") DO NOTHING
                    """, ct, ("product", productId), ("type", typeId), ("value", valueId));
            changed = true;
        }
        return new(complete, changed);
    }

    private bool ValidateProductAttributes(IReadOnlyList<ErpProductAttributeRow> sourceAttrs,
        IReadOnlyDictionary<string, Guid> types,
        IReadOnlyDictionary<(Guid TypeId, string Value), Guid> values,
        string productCode,
        StringBuilder detail)
    {
        bool complete = true;
        foreach (var source in sourceAttrs)
        {
            if (!options.ProductAttributeTypeCodes.ContainsKey(source.KeywordId) &&
                !IsIgnoredProductAttributeType(source.KeywordId))
            {
                detail.AppendLine($"! Ürün attribute tip eşleşmesi yok: ERP keywordId={source.KeywordId}.");
                complete = false;
            }
        }
        foreach (var group in sourceAttrs
                     .Where(x => options.ProductAttributeTypeCodes.ContainsKey(x.KeywordId))
                     .GroupBy(x => options.ProductAttributeTypeCodes[x.KeywordId], StringComparer.OrdinalIgnoreCase))
        {
            var code = group.Key;
            if (!types.TryGetValue(code, out var typeId))
            {
                detail.AppendLine($"! Attribute type yok: {code}.");
                complete = false;
                continue;
            }

            var missing = group.Where(x => !values.ContainsKey(
                    (typeId, Normalize(ResolveProductAttributeValue(code, x.Value)))))
                .ToArray();
            if (missing.Length == 0) continue;
            foreach (var source in missing)
                detail.AppendLine($"! Tanım değeri yok: {code}='{source.Value}' (ERP keywordId={source.KeywordId}, ürün={productCode}).");
            complete = false;
        }
        return complete;
    }

    private bool IsIgnoredProductAttributeType(string sourceCode)
        => options.IgnoredProductAttributeTypeCodes.Contains(sourceCode, StringComparer.OrdinalIgnoreCase);

    private string ResolveProductAttributeValue(string targetTypeCode, string sourceValue)
    {
        if (!options.ProductAttributeValueAliases.TryGetValue(targetTypeCode, out var aliases))
            return sourceValue;
        var normalized = Normalize(sourceValue);
        var alias = aliases.FirstOrDefault(x => Normalize(x.Key) == normalized).Value;
        return string.IsNullOrWhiteSpace(alias) ? sourceValue : alias;
    }

    private static async Task<HashSet<Guid>> ReadAttributeValueIdsAsync(
        NpgsqlConnection pg, NpgsqlTransaction tx, string sql, Guid ownerId, Guid typeId, CancellationToken ct)
    {
        var result = new HashSet<Guid>();
        await using var command = new NpgsqlCommand(sql, pg, tx);
        command.Parameters.AddWithValue("id", ownerId);
        command.Parameters.AddWithValue("type", typeId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetGuid(0));
        return result;
    }

    private static object NormalizeCompareAt(decimal? price, decimal? compareAt)
        => compareAt.HasValue && compareAt > 0 && compareAt != price ? compareAt.Value : DBNull.Value;

    private ErpSourceSyncReport Ok(string slice, int changed, StringBuilder detail, Stopwatch sw)
        => new(true, options.DryRun, slice, changed, detail.ToString(), null, (int)sw.ElapsedMilliseconds);
    private ErpSourceSyncReport Fail(string slice, StringBuilder detail, Exception ex, Stopwatch sw)
        => new(false, options.DryRun, slice, 0, detail.ToString(), ex.Message, (int)sw.ElapsedMilliseconds);

    private static string I18n(string value) => JsonSerializer.Serialize(new Dictionary<string, string> { ["tr"] = value });
    internal static string Normalize(string value) => string.Join(' ', value.Trim().ToLower(new CultureInfo("tr-TR")).Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private async Task EnsureVariantDefinitionValuesAsync(
        NpgsqlConnection pg, NpgsqlTransaction? tx, IReadOnlyList<ErpVariantRow> variants,
        IReadOnlyDictionary<string, Guid> types, StringBuilder detail, CancellationToken ct)
    {
        if (!options.AutoCreateColorValues || !types.TryGetValue("renk", out var colorTypeId)) return;
        var colors = variants.SelectMany(x => x.Attributes)
            .Where(x => x.TypeId == 1 && !string.IsNullOrWhiteSpace(x.SourceCode))
            .GroupBy(x => x.SourceCode!, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First()).ToArray();
        foreach (var color in colors)
        {
            var metadata = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["erpSource"] = "v3", ["erpSourceCode"] = color.SourceCode!
            });
            var affected = await ExecAsync(pg, tx, """
                WITH existing AS (
                  SELECT "Id" FROM definition.attribute_values
                   WHERE "AttributeTypeId"=@type AND NOT "IsDeleted"
                     AND ("ExtraData"->>'erpSourceCode'=@sourceCode
                          OR lower(COALESCE("NameI18n"->>'tr',''))=lower(@name))
                   ORDER BY CASE WHEN "ExtraData"->>'erpSourceCode'=@sourceCode THEN 0 ELSE 1 END
                   LIMIT 1
                ), updated AS (
                  UPDATE definition.attribute_values v SET
                    "NameI18n"=@nameJson::jsonb,
                    "ExtraData"=COALESCE(v."ExtraData",'{}'::jsonb)||@metadata::jsonb,
                    "IsActive"=true,"UpdatedAt"=now()
                   FROM existing e WHERE v."Id"=e."Id"
                     AND (v."NameI18n" IS DISTINCT FROM @nameJson::jsonb
                          OR v."ExtraData"->>'erpSourceCode' IS DISTINCT FROM @sourceCode OR NOT v."IsActive")
                  RETURNING v."Id"
                )
                INSERT INTO definition.attribute_values
                  ("Id","AttributeTypeId","NameI18n","ExtraData","IsActive","SortOrder","CreatedAt","IsDeleted")
                SELECT gen_random_uuid(),@type,@nameJson::jsonb,@metadata::jsonb,true,0,now(),false
                 WHERE NOT EXISTS (SELECT 1 FROM existing)
                """, ct, ("type", colorTypeId), ("sourceCode", color.SourceCode!),
                ("name", color.Value), ("nameJson", I18n(color.Value)), ("metadata", metadata));
            if (affected > 0) detail.AppendLine($"+ V3 renk tanımı eşlendi: {color.SourceCode}={color.Value}.");
        }
    }

    private async Task EnsureProductDefinitionValuesAsync(
        NpgsqlConnection pg, NpgsqlTransaction? tx, IReadOnlyList<ErpProductAttributeRow> attributes,
        IReadOnlyDictionary<string, Guid> types, StringBuilder detail, CancellationToken ct)
    {
        if (!options.AutoCreateProductAttributeValues) return;
        var mapped = attributes
            .Where(x => options.ProductAttributeTypeCodes.ContainsKey(x.KeywordId))
            .Select(x => new
            {
                Source = x,
                TargetCode = options.ProductAttributeTypeCodes[x.KeywordId],
                Name = ResolveProductAttributeValue(options.ProductAttributeTypeCodes[x.KeywordId], x.Value)
            })
            .Where(x => types.ContainsKey(x.TargetCode) && !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => (x.TargetCode, x.Source.KeywordId,
                SourceCode: x.Source.SourceCode ?? Normalize(x.Source.Value)))
            .Select(x => x.First())
            .ToArray();

        if (mapped.Length == 0) return;
        const string lockKey = "erp-product-attribute-values";
        await ExecAsync(pg, tx, tx is null
            ? "SELECT pg_advisory_lock(hashtext(@key))"
            : "SELECT pg_advisory_xact_lock(hashtext(@key))", ct, ("key", lockKey));
        try
        {
            foreach (var item in mapped)
            {
                var typeId = types[item.TargetCode];
                var sourceCode = item.Source.SourceCode ?? Normalize(item.Source.Value);
                var metadata = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["erpSource"] = "v3",
                    ["erpSourceAttributeTypeCode"] = item.Source.KeywordId,
                    ["erpSourceCode"] = sourceCode
                });
                var affected = await ExecAsync(pg, tx, """
                WITH existing AS (
                  SELECT "Id" FROM definition.attribute_values
                   WHERE "AttributeTypeId"=@type AND NOT "IsDeleted"
                     AND (("ExtraData"->>'erpSourceAttributeTypeCode'=@sourceType
                           AND "ExtraData"->>'erpSourceCode'=@sourceCode)
                          OR lower(COALESCE("NameI18n"->>'tr',''))=lower(@name))
                   ORDER BY CASE WHEN "ExtraData"->>'erpSourceAttributeTypeCode'=@sourceType
                                      AND "ExtraData"->>'erpSourceCode'=@sourceCode THEN 0 ELSE 1 END
                   LIMIT 1
                ), updated AS (
                  UPDATE definition.attribute_values v SET
                    "NameI18n"=@nameJson::jsonb,
                    "ExtraData"=COALESCE(v."ExtraData",'{}'::jsonb)||@metadata::jsonb,
                    "IsActive"=true,"UpdatedAt"=now()
                   FROM existing e WHERE v."Id"=e."Id"
                     AND (v."NameI18n" IS DISTINCT FROM @nameJson::jsonb
                          OR v."ExtraData"->>'erpSourceAttributeTypeCode' IS DISTINCT FROM @sourceType
                          OR v."ExtraData"->>'erpSourceCode' IS DISTINCT FROM @sourceCode OR NOT v."IsActive")
                  RETURNING v."Id"
                )
                INSERT INTO definition.attribute_values
                  ("Id","AttributeTypeId","NameI18n","ExtraData","IsActive","SortOrder","CreatedAt","IsDeleted")
                SELECT gen_random_uuid(),@type,@nameJson::jsonb,@metadata::jsonb,true,0,now(),false
                 WHERE NOT EXISTS (SELECT 1 FROM existing)
                """, ct, ("type", typeId), ("sourceType", item.Source.KeywordId),
                    ("sourceCode", sourceCode), ("name", item.Name), ("nameJson", I18n(item.Name)), ("metadata", metadata));
                if (affected > 0)
                    detail.AppendLine($"+ V3 ürün özelliği tanımı eşlendi: {item.TargetCode}={item.Name}.");
            }
        }
        finally
        {
            if (tx is null)
                await ExecAsync(pg, null, "SELECT pg_advisory_unlock(hashtext(@key))", CancellationToken.None, ("key", lockKey));
        }
    }

    private async Task<(Guid? AccountId, string? BlockingError)> ResolveSupplierAsync(
        NpgsqlConnection pg, ErpSupplierRow? supplier, StringBuilder detail, CancellationToken ct)
    {
        if (supplier is null) return (null, null);
        if (!options.SupplierAccountCodes.TryGetValue(supplier.Code, out var accountCode))
        {
            detail.AppendLine($"! V3 tedarikçi eşlemesi yok: code={supplier.Code}, name={supplier.Name}; mevcut SupplierId korunur.");
            return (null, null);
        }
        await using var command = new NpgsqlCommand("""
            SELECT "Id" FROM accounts.current_accounts
             WHERE "Code"=@code AND "AccountType" IN ('supplier','both')
               AND "IsActive" AND NOT "IsDeleted"
            """, pg);
        command.Parameters.AddWithValue("code", accountCode);
        var value = await command.ExecuteScalarAsync(ct);
        return value is Guid id ? (id, null) : (null,
            $"V3 tedarikçi mapping hedef carisi bulunamadı: {supplier.Code}->{accountCode}.");
    }

    private static Task<int> ApplySupplierAsync(
        NpgsqlConnection pg, NpgsqlTransaction tx, Guid productId, Guid? supplierId, CancellationToken ct)
        => supplierId.HasValue
            ? ExecAsync(pg, tx, """
                UPDATE catalog.products SET "SupplierId"=@supplier,"UpdatedAt"=now()
                 WHERE "Id"=@id AND "SupplierId" IS DISTINCT FROM @supplier
                """, ct, ("id", productId), ("supplier", supplierId.Value))
            : Task.FromResult(0);

    private static async Task<int> ExecAsync(NpgsqlConnection pg, NpgsqlTransaction? tx, string sql,
        CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using var cmd = new NpgsqlCommand(sql, pg, tx);
        foreach (var p in parameters)
        {
            if (p.Value is DBNull)
                cmd.Parameters.Add(new NpgsqlParameter(p.Name, NullType(p.Name)) { Value = DBNull.Value });
            else
                cmd.Parameters.AddWithValue(p.Name, p.Value);
        }
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<T?> ScalarNullableAsync<T>(NpgsqlConnection pg, string sql, CancellationToken ct,
        params (string Name, object Value)[] parameters) where T : struct
    {
        await using var cmd = new NpgsqlCommand(sql, pg);
        foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Name, p.Value);
        var value = await cmd.ExecuteScalarAsync(ct);
        if (value is null or DBNull) return null;
        if (value is T typed) return typed;
        return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    private static NpgsqlDbType NullType(string parameterName) => parameterName.ToLowerInvariant() switch
    {
        "cost" or "price" or "compare" => NpgsqlDbType.Numeric,
        "created" or "updated" or "watermark" => NpgsqlDbType.TimestampTz,
        "group" or "id" or "product" or "platform" or "variant" or "warehouse" or "section" or "bin" or "type" or "value" or "supplier" => NpgsqlDbType.Uuid,
        _ => NpgsqlDbType.Text
    };

    private sealed record GroupMaps(Dictionary<string, Guid> ByCode, Dictionary<string, Guid> ByNormalizedName);
    private sealed record AttributeReplaceResult(bool Complete, bool Changed);
}
