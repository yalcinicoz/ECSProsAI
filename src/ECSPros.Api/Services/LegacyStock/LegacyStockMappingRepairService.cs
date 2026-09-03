using System.Data;
using System.Diagnostics;
using ECSPros.Api.Services.LegacyImport;
using MySql.Data.MySqlClient;
using Npgsql;

namespace ECSPros.Api.Services.LegacyStock;

/// <summary>
/// İlk PostgreSQL dump'ından sonra production MySQL'de oluşan stoklu varyant ve rafları,
/// mevcut migration iş anahtarlarını koruyarak hedefe ekler. MySQL yalnız READ ONLY okunur;
/// hedef yazıları tek PostgreSQL transaction'ında ve idempotent olarak yapılır.
/// </summary>
public sealed class LegacyStockMappingRepairService(
    NpgsqlDataSource dataSource,
    ILegacyReadSource source,
    LegacyStockSyncOptions options,
    ILogger<LegacyStockMappingRepairService> logger)
{
    public async Task<LegacyStockSyncReport> RepairAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var snapshot = await ReadSourceMappingsAsync(ct);
            if (snapshot.Variants.Count == 0 || snapshot.Bins.Count == 0)
                throw new InvalidOperationException("Legacy stok eşleme kaynağı boş döndü.");

            await using var pg = await dataSource.OpenConnectionAsync(ct);
            await using var tx = await pg.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var products = await LoadActiveProductsAsync(pg, tx, ct);
                var variants = await LoadVariantsAsync(pg, tx, ct);
                var usedSkus = await LoadUsedSkusAsync(pg, tx, ct);
                var sections = await LoadActiveSectionsAsync(pg, tx, ct);
                var bins = await LoadBinsAsync(pg, tx, ct);
                var usedBinCodes = await LoadUsedBinCodesAsync(pg, tx, ct);
                var attributeTypes = await LoadAttributeTypesAsync(pg, tx, ct);
                var attributeValues = await LoadAttributeValuesAsync(pg, tx, ct);

                var invalidVariant = snapshot.Variants.Values.FirstOrDefault(x =>
                    variants.TryGetValue(x.Barcode, out var target) && (target.IsDeleted || !target.IsActive));
                if (invalidVariant is not null)
                    throw new InvalidOperationException(
                        $"Stoklu varyant hedefte silinmiş veya pasif: {invalidVariant.Barcode}.");
                var invalidBin = snapshot.Bins.Values.FirstOrDefault(x =>
                    bins.TryGetValue(x.Barcode, out var target) && (target.IsDeleted || !target.IsActive));
                if (invalidBin is not null)
                    throw new InvalidOperationException(
                        $"Stoklu raf hedefte silinmiş veya pasif: {invalidBin.Barcode}.");

                var missingVariants = snapshot.Variants.Values
                    .Where(x => !variants.ContainsKey(x.Barcode))
                    .OrderBy(x => x.SourceVariantId)
                    .ToArray();
                var missingBins = snapshot.Bins.Values
                    .Where(x => !bins.ContainsKey(x.Barcode))
                    .OrderBy(x => x.SourceBinId)
                    .ToArray();

                ValidateVariantCandidates(missingVariants, products, variants, usedSkus);
                ValidateBinCandidates(missingBins, sections, bins, usedBinCodes);

                var variantCount = 0;
                var binCount = 0;
                var attributeValueCount = 0;
                var variantAttributeCount = 0;
                var unmappedAttributeCount = 0;

                foreach (var sourceVariant in missingVariants)
                {
                    var variantId = Guid.NewGuid();
                    var createdAt = sourceVariant.CreatedAt.HasValue
                        ? DateTime.SpecifyKind(sourceVariant.CreatedAt.Value, DateTimeKind.Utc)
                        : DateTime.UtcNow;

                    if (!options.MappingRepairDryRun)
                    {
                        await ExecAsync(pg, tx, """
                            INSERT INTO catalog.product_variants
                              ("Id","ProductId","Sku","Barcode","BasePrice","IsActive","CreatedAt","IsDeleted")
                            VALUES (@id,@productId,@sku,@barcode,0,true,@createdAt,false)
                            """, ct,
                            ("id", variantId),
                            ("productId", products[sourceVariant.ProductCode]),
                            ("sku", sourceVariant.Barcode),
                            ("barcode", sourceVariant.Barcode),
                            ("createdAt", createdAt));
                    }
                    variantCount++;

                    foreach (var attribute in sourceVariant.Attributes)
                    {
                        if (!snapshot.AttributeTypeCodes.TryGetValue(attribute.TypeId, out var typeCode)
                            || !attributeTypes.TryGetValue(typeCode, out var typeId))
                        {
                            unmappedAttributeCount++;
                            continue;
                        }

                        if (!attributeValues.TryGetValue((typeId, attribute.Value), out var valueId))
                        {
                            valueId = Guid.NewGuid();
                            if (!options.MappingRepairDryRun)
                            {
                                await ExecAsync(pg, tx, """
                                    INSERT INTO definition.attribute_values
                                      ("Id","AttributeTypeId","NameI18n","IsActive","SortOrder","CreatedAt","IsDeleted")
                                    VALUES (@id,@typeId,jsonb_build_object('tr',@name),true,0,now(),false)
                                    """, ct,
                                    ("id", valueId), ("typeId", typeId), ("name", attribute.Value));
                            }
                            attributeValues[(typeId, attribute.Value)] = valueId;
                            attributeValueCount++;
                        }

                        if (!options.MappingRepairDryRun)
                        {
                            await ExecAsync(pg, tx, """
                                INSERT INTO catalog.product_variant_attributes
                                  ("Id","VariantId","AttributeTypeId","AttributeValueId","CreatedAt","IsDeleted")
                                VALUES (@id,@variantId,@typeId,@valueId,now(),false)
                                """, ct,
                                ("id", Guid.NewGuid()), ("variantId", variantId),
                                ("typeId", typeId), ("valueId", valueId));
                        }
                        variantAttributeCount++;
                    }
                }

                foreach (var sourceBin in missingBins)
                {
                    var sectionId = sections[sourceBin.StorageCode];
                    var code = BuildBinCode(sourceBin, sectionId, usedBinCodes);
                    if (!options.MappingRepairDryRun)
                    {
                        await ExecAsync(pg, tx, """
                            INSERT INTO inventory.inv_warehouse_bins
                              ("Id","SectionId","Code","Barcode","Name","PickingOrder","IsActive",
                               "SortOrder","CreatedAt","IsDeleted")
                            VALUES (@id,@sectionId,@code,@barcode,NULL,0,true,0,now(),false)
                            """, ct,
                            ("id", Guid.NewGuid()), ("sectionId", sectionId),
                            ("code", code), ("barcode", sourceBin.Barcode));
                    }
                    binCount++;
                }

                if (options.MappingRepairDryRun)
                    await tx.RollbackAsync(ct);
                else
                    await tx.CommitAsync(ct);

                var changed = variantCount + binCount + attributeValueCount + variantAttributeCount;
                var detail =
                    $"kaynakStokluVaryant={snapshot.Variants.Count}, kaynakStokluRaf={snapshot.Bins.Count}, " +
                    $"eklenecekVaryant={variantCount}, eklenecekRaf={binCount}, " +
                    $"eklenecekÖzellikDeğeri={attributeValueCount}, eklenecekVaryantÖzelliği={variantAttributeCount}, " +
                    $"eşlenemeyenVaryantÖzelliği={unmappedAttributeCount}";
                return new(true, options.MappingRepairDryRun, changed, detail, null, (int)sw.ElapsedMilliseconds);
            }
            catch
            {
                if (tx.Connection is not null)
                    await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Legacy stok eşleme onarımı başarısız");
            return new(false, options.MappingRepairDryRun, 0, string.Empty, ex.Message, (int)sw.ElapsedMilliseconds);
        }
    }

    private Task<SourceMappingSnapshot> ReadSourceMappingsAsync(CancellationToken ct) =>
        source.ExecuteReadAsync<SourceMappingSnapshot>(async (connection, transaction, token) =>
        {
            var attributeTypeCodes = new Dictionary<int, string>();
            await using (var command = CreateMySqlCommand(connection, transaction,
                "SELECT Id,aciklama FROM dfvaryanttipleri", options.CommandTimeoutSeconds))
            await using (var reader = await command.ExecuteReaderAsync(token))
                while (await reader.ReadAsync(token))
                    attributeTypeCodes[reader.GetInt32(0)] = Slugify(reader.GetString(1));

            var variants = new Dictionary<string, SourceVariant>(StringComparer.OrdinalIgnoreCase);
            await using (var command = CreateMySqlCommand(connection, transaction, """
                SELECT pv.Id,pv.urunId,pv.barkod,p.urunKodu,
                       pv.varyant1TipId,pv.varyant1Degeri,
                       pv.varyant2TipId,pv.varyant2Degeri,
                       pv.varyant3TipId,pv.varyant3Degeri,pv.olusturmaTarihi
                FROM apurunvaryantlari pv
                JOIN apurunler p ON p.Id=pv.urunId
                WHERE pv.barkod IS NOT NULL AND pv.barkod<>''
                  AND EXISTS (
                    SELECT 1
                    FROM opproductlocations pl
                    JOIN dfstorageunits su ON su.Id=pl.storageUnitId
                    JOIN dfstorages st ON st.Id=su.storageId
                    WHERE pl.productVariantId=pv.Id AND st.type=@storageType AND st.status=1
                      AND su.barcode IS NOT NULL AND su.barcode<>'')
                ORDER BY pv.Id
                """, options.CommandTimeoutSeconds))
            {
                command.Parameters.AddWithValue("@storageType", options.StockStorageType);
                await using var reader = await command.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token))
                {
                    var attributes = new List<SourceAttribute>(3);
                    for (var axis = 0; axis < 3; axis++)
                    {
                        var typeIndex = 4 + axis * 2;
                        var valueIndex = typeIndex + 1;
                        var typeId = reader.IsDBNull(typeIndex) ? 0 : reader.GetInt32(typeIndex);
                        var value = reader.IsDBNull(valueIndex) ? string.Empty : reader.GetString(valueIndex).Trim();
                        if (typeId != 0 && value.Length > 0)
                            attributes.Add(new(typeId, value));
                    }

                    var variant = new SourceVariant(
                        reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2).Trim(),
                        reader.IsDBNull(3) ? string.Empty : reader.GetString(3).Trim(),
                        attributes,
                        reader.IsDBNull(10) ? null : reader.GetDateTime(10));
                    if (variants.TryGetValue(variant.Barcode, out var duplicate)
                        && duplicate.SourceVariantId != variant.SourceVariantId)
                        throw new InvalidOperationException(
                            $"Legacy kaynakta aynı barkodu kullanan birden fazla varyant var: {variant.Barcode}.");
                    variants[variant.Barcode] = variant;
                }
            }

            var bins = new Dictionary<string, SourceBin>(StringComparer.OrdinalIgnoreCase);
            await using (var command = CreateMySqlCommand(connection, transaction, """
                SELECT su.Id,su.barcode,su.shelfUnitNumber,st.code
                FROM dfstorageunits su
                JOIN dfstorages st ON st.Id=su.storageId
                WHERE st.type=@storageType AND st.status=1
                  AND su.barcode IS NOT NULL AND su.barcode<>''
                  AND EXISTS (SELECT 1 FROM opproductlocations pl WHERE pl.storageUnitId=su.Id)
                ORDER BY su.Id
                """, options.CommandTimeoutSeconds))
            {
                command.Parameters.AddWithValue("@storageType", options.StockStorageType);
                await using var reader = await command.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token))
                {
                    var bin = new SourceBin(
                        reader.GetInt32(0), reader.GetString(1).Trim(),
                        reader.IsDBNull(2) ? string.Empty : reader.GetString(2).Trim(),
                        reader.IsDBNull(3) ? string.Empty : reader.GetString(3).Trim());
                    if (bins.TryGetValue(bin.Barcode, out var duplicate)
                        && duplicate.SourceBinId != bin.SourceBinId)
                        throw new InvalidOperationException(
                            $"Legacy kaynakta aynı barkodu kullanan birden fazla raf var: {bin.Barcode}.");
                    bins[bin.Barcode] = bin;
                }
            }

            return new(variants, bins, attributeTypeCodes);
        }, ct);

    private static MySqlCommand CreateMySqlCommand(
        MySqlConnection connection, MySqlTransaction transaction, string sql, int timeout)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = timeout;
        command.CommandText = sql;
        return command;
    }

    private static void ValidateVariantCandidates(
        IReadOnlyList<SourceVariant> candidates,
        IReadOnlyDictionary<string, Guid> products,
        IReadOnlyDictionary<string, TargetVariant> variants,
        IReadOnlySet<string> usedSkus)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.Barcode.Length > 50)
                throw new InvalidOperationException($"Varyant barkodu 50 karakter sınırını aşıyor: {candidate.Barcode}.");
            if (candidate.Barcode.Length > 200)
                throw new InvalidOperationException($"Varyant SKU değeri 200 karakter sınırını aşıyor: {candidate.Barcode}.");
            if (candidate.ProductCode.Length == 0 || !products.ContainsKey(candidate.ProductCode))
                throw new InvalidOperationException(
                    $"Stoklu varyantın aktif hedef ürünü bulunamadı: barkod={candidate.Barcode}, ürün={candidate.ProductCode}.");
            if (usedSkus.Contains(candidate.Barcode))
                throw new InvalidOperationException(
                    $"Stoklu varyant barkodu hedefte başka bir SKU tarafından kullanılıyor: {candidate.Barcode}.");
        }
    }

    private static void ValidateBinCandidates(
        IReadOnlyList<SourceBin> candidates,
        IReadOnlyDictionary<string, Guid> sections,
        IReadOnlyDictionary<string, TargetBinStatus> bins,
        IReadOnlySet<BinCodeKey> usedBinCodes)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.Barcode.Length > 100)
                throw new InvalidOperationException($"Raf barkodu 100 karakter sınırını aşıyor: {candidate.Barcode}.");
            if (candidate.StorageCode.Length == 0 || !sections.TryGetValue(candidate.StorageCode, out var sectionId))
                throw new InvalidOperationException(
                    $"Stoklu rafın aktif hedef kısmı bulunamadı: barkod={candidate.Barcode}, kısım={candidate.StorageCode}.");
            var baseCode = candidate.ShelfUnitNumber.Length > 0 ? candidate.ShelfUnitNumber : candidate.Barcode;
            var code = usedBinCodes.Contains(new(sectionId, baseCode))
                ? $"{baseCode}-{candidate.SourceBinId}"
                : baseCode;
            if (code.Length > 50)
                throw new InvalidOperationException($"Hedef raf kodu 50 karakter sınırını aşıyor: {code}.");
            if (usedBinCodes.Contains(new(sectionId, code)) && !code.Equals(baseCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Hedef raf kodu çakışıyor: {candidate.StorageCode}/{code}.");
        }
    }

    private static string BuildBinCode(SourceBin source, Guid sectionId, HashSet<BinCodeKey> usedCodes)
    {
        var baseCode = source.ShelfUnitNumber.Length > 0 ? source.ShelfUnitNumber : source.Barcode;
        var code = usedCodes.Add(new(sectionId, baseCode)) ? baseCode : $"{baseCode}-{source.SourceBinId}";
        if (!code.Equals(baseCode, StringComparison.OrdinalIgnoreCase) && !usedCodes.Add(new(sectionId, code)))
            throw new InvalidOperationException($"Hedef raf kodu çakışıyor: {source.StorageCode}/{code}.");
        return code;
    }

    private static async Task<Dictionary<string, Guid>> LoadActiveProductsAsync(
        NpgsqlConnection pg, NpgsqlTransaction tx, CancellationToken ct)
    {
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        await using var command = new NpgsqlCommand(
            "SELECT \"Code\",\"Id\" FROM catalog.products WHERE NOT \"IsDeleted\"", pg, tx);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetString(0), reader.GetGuid(1));
        return result;
    }

    private static async Task<Dictionary<string, TargetVariant>> LoadVariantsAsync(
        NpgsqlConnection pg, NpgsqlTransaction tx, CancellationToken ct)
    {
        var result = new Dictionary<string, TargetVariant>(StringComparer.OrdinalIgnoreCase);
        await using var command = new NpgsqlCommand(
            "SELECT \"Barcode\",\"Id\",\"IsDeleted\",\"IsActive\" FROM catalog.product_variants " +
            "WHERE \"Barcode\" IS NOT NULL AND \"Barcode\"<>''", pg, tx);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(reader.GetString(0), new(reader.GetGuid(1), reader.GetBoolean(2), reader.GetBoolean(3)));
        return result;
    }

    private static async Task<HashSet<string>> LoadUsedSkusAsync(
        NpgsqlConnection pg, NpgsqlTransaction tx, CancellationToken ct)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = new NpgsqlCommand(
            "SELECT \"Sku\" FROM catalog.product_variants WHERE \"Sku\" IS NOT NULL AND \"Sku\"<>''", pg, tx);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetString(0));
        return result;
    }

    private static async Task<Dictionary<string, Guid>> LoadActiveSectionsAsync(
        NpgsqlConnection pg, NpgsqlTransaction tx, CancellationToken ct)
    {
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        await using var command = new NpgsqlCommand(
            "SELECT \"Code\",\"Id\" FROM inventory.inv_warehouse_sections WHERE NOT \"IsDeleted\" AND \"IsActive\"", pg, tx);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var code = reader.GetString(0);
            if (!result.TryAdd(code, reader.GetGuid(1)))
                throw new InvalidOperationException($"Hedefte aynı koda sahip birden fazla aktif depo kısmı var: {code}.");
        }
        return result;
    }

    private static async Task<Dictionary<string, TargetBinStatus>> LoadBinsAsync(
        NpgsqlConnection pg, NpgsqlTransaction tx, CancellationToken ct)
    {
        var result = new Dictionary<string, TargetBinStatus>(StringComparer.OrdinalIgnoreCase);
        await using var command = new NpgsqlCommand(
            "SELECT \"Barcode\",\"Id\",\"IsDeleted\",\"IsActive\" FROM inventory.inv_warehouse_bins", pg, tx);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(reader.GetString(0), new(reader.GetGuid(1), reader.GetBoolean(2), reader.GetBoolean(3)));
        return result;
    }

    private static async Task<HashSet<BinCodeKey>> LoadUsedBinCodesAsync(
        NpgsqlConnection pg, NpgsqlTransaction tx, CancellationToken ct)
    {
        var result = new HashSet<BinCodeKey>();
        await using var command = new NpgsqlCommand(
            "SELECT \"SectionId\",\"Code\" FROM inventory.inv_warehouse_bins", pg, tx);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetString(1)));
        return result;
    }

    private static async Task<Dictionary<string, Guid>> LoadAttributeTypesAsync(
        NpgsqlConnection pg, NpgsqlTransaction tx, CancellationToken ct)
    {
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        await using var command = new NpgsqlCommand(
            "SELECT \"Code\",\"Id\" FROM definition.attribute_types WHERE NOT \"IsDeleted\" AND \"IsActive\"", pg, tx);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetString(0), reader.GetGuid(1));
        return result;
    }

    private static async Task<Dictionary<(Guid TypeId, string Name), Guid>> LoadAttributeValuesAsync(
        NpgsqlConnection pg, NpgsqlTransaction tx, CancellationToken ct)
    {
        var result = new Dictionary<(Guid TypeId, string Name), Guid>();
        await using var command = new NpgsqlCommand(
            "SELECT \"AttributeTypeId\",\"NameI18n\"->>'tr',\"Id\" FROM definition.attribute_values " +
            "WHERE NOT \"IsDeleted\" AND \"NameI18n\"->>'tr' IS NOT NULL", pg, tx);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.TryAdd((reader.GetGuid(0), reader.GetString(1)), reader.GetGuid(2));
        return result;
    }

    private static async Task<int> ExecAsync(
        NpgsqlConnection pg, NpgsqlTransaction tx, string sql, CancellationToken ct,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, pg, tx) { CommandTimeout = 300 };
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return await command.ExecuteNonQueryAsync(ct);
    }

    private static string Slugify(string value) => value.ToLowerInvariant()
        .Replace(" ", "_").Replace("ı", "i").Replace("ş", "s").Replace("ğ", "g")
        .Replace("ü", "u").Replace("ö", "o").Replace("ç", "c").Replace("â", "a")
        .Replace("î", "i").Replace("û", "u").Replace("/", "_").Replace("-", "_")
        .Replace("(", "").Replace(")", "").Replace(".", "");

    private sealed record SourceAttribute(int TypeId, string Value);
    private sealed record SourceVariant(
        int SourceVariantId,
        int SourceProductId,
        string Barcode,
        string ProductCode,
        IReadOnlyList<SourceAttribute> Attributes,
        DateTime? CreatedAt);
    private sealed record SourceBin(int SourceBinId, string Barcode, string ShelfUnitNumber, string StorageCode);
    private sealed record SourceMappingSnapshot(
        IReadOnlyDictionary<string, SourceVariant> Variants,
        IReadOnlyDictionary<string, SourceBin> Bins,
        IReadOnlyDictionary<int, string> AttributeTypeCodes);
    private sealed record TargetVariant(Guid Id, bool IsDeleted, bool IsActive);
    private sealed record TargetBinStatus(Guid Id, bool IsDeleted, bool IsActive);

    private readonly record struct BinCodeKey(Guid SectionId, string Code)
    {
        public bool Equals(BinCodeKey other) =>
            SectionId == other.SectionId && Code.Equals(other.Code, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() =>
            HashCode.Combine(SectionId, StringComparer.OrdinalIgnoreCase.GetHashCode(Code));
    }
}
