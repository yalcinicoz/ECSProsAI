using System.Data;
using System.Diagnostics;
using ECSPros.Api.Services.LegacyImport;
using ECSPros.Shared.Contracts;
using MySql.Data.MySqlClient;
using Npgsql;
using NpgsqlTypes;

namespace ECSPros.Api.Services.LegacyStock;

public sealed record LegacyStockSyncReport(
    bool Success,
    bool DryRun,
    int Changed,
    string Detail,
    string? Error,
    int DurationMs);

/// <summary>
/// Geçiş süresince production MySQL'deki internete açık, rezervsiz fiziksel adetleri yeni
/// PostgreSQL'e tam snapshot olarak taşır. MySQL daima server-side READ ONLY transaction'dır.
/// PostgreSQL yazıları tek transaction'dır; yalnız kaynakta eşlenen legacy rafları yönetilir.
/// </summary>
public sealed class LegacyStockSyncService(
    NpgsqlDataSource dataSource,
    ILegacyReadSource source,
    LegacyStockSyncOptions options,
    ICacheBustPublisher cacheBust,
    ILogger<LegacyStockSyncService> logger)
{
    public bool IsConfigured => source.IsConfigured;

    public async Task<LegacyStockSyncReport> SyncAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var sourceSnapshot = await ReadSourceSnapshotAsync(ct);
            if (sourceSnapshot.Rows.Count < options.MinimumSourceRows)
                throw new InvalidOperationException(
                    $"Legacy stok kaynak satırı güvenlik eşiğinin altında: {sourceSnapshot.Rows.Count} < {options.MinimumSourceRows}.");
            if (sourceSnapshot.ManagedBinBarcodes.Count == 0)
                throw new InvalidOperationException("Legacy stok kaynağında yönetilecek raf bulunamadı.");

            await using var pg = await dataSource.OpenConnectionAsync(ct);
            await using var tx = await pg.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            try
            {
                var variantByBarcode = await LoadVariantMapAsync(pg, tx, ct);
                var binByBarcode = await LoadBinMapAsync(pg, tx, ct);
                var allVariantStatus = await LoadAllVariantStatusAsync(pg, tx, ct);
                var productStatus = await LoadProductStatusAsync(pg, tx, ct);
                var allBinStatus = await LoadAllBinStatusAsync(pg, tx, ct);
                var activeSectionCodes = await LoadActiveSectionCodesAsync(pg, tx, ct);
                var mapped = new Dictionary<(Guid VariantId, Guid BinId), int>();
                long unmappedQuantity = 0;
                int unmappedRows = 0;
                long unmappedVariantQuantity = 0;
                int unmappedVariantRows = 0;
                long unmappedBinQuantity = 0;
                int unmappedBinRows = 0;
                var unmappedVariantBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var unmappedBinBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var deletedVariantBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var missingVariantWithActiveProduct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var missingVariantWithDeletedProduct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var missingVariantWithoutProduct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var inactiveBinBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var deletedBinBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var missingBinWithSection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var missingBinWithoutSection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                long sourceQuantity = 0;

                foreach (var row in sourceSnapshot.Rows)
                {
                    sourceQuantity += row.AvailableQuantity;
                    var variantMapped = variantByBarcode.TryGetValue(row.VariantBarcode, out var variantId);
                    var binMapped = binByBarcode.TryGetValue(row.BinBarcode, out var bin);
                    if (!variantMapped || !binMapped)
                    {
                        unmappedRows++;
                        unmappedQuantity += row.AvailableQuantity;
                        if (!variantMapped)
                        {
                            unmappedVariantRows++;
                            unmappedVariantQuantity += row.AvailableQuantity;
                            unmappedVariantBarcodes.Add(row.VariantBarcode);
                            if (allVariantStatus.TryGetValue(row.VariantBarcode, out var variantDeleted) && variantDeleted)
                                deletedVariantBarcodes.Add(row.VariantBarcode);
                            else if (productStatus.TryGetValue(row.ProductCode, out var productDeleted))
                            {
                                if (productDeleted) missingVariantWithDeletedProduct.Add(row.VariantBarcode);
                                else missingVariantWithActiveProduct.Add(row.VariantBarcode);
                            }
                            else
                                missingVariantWithoutProduct.Add(row.VariantBarcode);
                        }
                        if (!binMapped)
                        {
                            unmappedBinRows++;
                            unmappedBinQuantity += row.AvailableQuantity;
                            unmappedBinBarcodes.Add(row.BinBarcode);
                            if (allBinStatus.TryGetValue(row.BinBarcode, out var binStatus))
                            {
                                if (binStatus.IsDeleted) deletedBinBarcodes.Add(row.BinBarcode);
                                else if (!binStatus.IsActive) inactiveBinBarcodes.Add(row.BinBarcode);
                            }
                            else if (activeSectionCodes.Contains(row.StorageCode))
                                missingBinWithSection.Add(row.BinBarcode);
                            else
                                missingBinWithoutSection.Add(row.BinBarcode);
                        }
                        continue;
                    }

                    var key = (variantId, bin!.Id);
                    mapped[key] = mapped.GetValueOrDefault(key) + row.AvailableQuantity;
                }

                var managedBinIds = sourceSnapshot.ManagedBinBarcodes
                    .Select(x => binByBarcode.TryGetValue(x, out var bin) ? bin.Id : (Guid?)null)
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToArray();
                if (managedBinIds.Length == 0)
                    throw new InvalidOperationException("Legacy stok raflarının hiçbiri hedef PostgreSQL ile eşleşmedi.");
                if (!options.DryRun && options.BlockOnUnmappedQuantity
                    && (unmappedRows > options.MaximumUnmappedRows
                        || unmappedQuantity > options.MaximumUnmappedQuantity))
                    throw new InvalidOperationException(
                        $"Legacy stokta hedefle eşleşmeyen {unmappedRows} satır/{unmappedQuantity} adet var; " +
                        $"izinli üst sınır {options.MaximumUnmappedRows} satır/{options.MaximumUnmappedQuantity} adet, " +
                        "gerçek yazım engellendi.");

                await CreateTempTablesAsync(pg, tx, ct);
                await CopySnapshotAsync(pg, mapped, ct);
                await CopyManagedBinsAsync(pg, managedBinIds, ct);

                var updateCount = await ScalarAsync<long>(pg, tx, """
                    SELECT count(*)
                    FROM inventory.inv_stocks s
                    JOIN _legacy_stock_snapshot t ON t.variant_id=s."VariantId" AND t.bin_id=s."BinId"
                    WHERE NOT s."IsDeleted"
                      AND s."Quantity" IS DISTINCT FROM (t.available_qty + s."ReservedQuantity")
                    """, ct);
                var insertCount = await ScalarAsync<long>(pg, tx, """
                    SELECT count(*)
                    FROM _legacy_stock_snapshot t
                    WHERE NOT EXISTS (
                      SELECT 1 FROM inventory.inv_stocks s
                      WHERE s."VariantId"=t.variant_id AND s."BinId"=t.bin_id AND NOT s."IsDeleted")
                    """, ct);
                var zeroCount = await ScalarAsync<long>(pg, tx, """
                    SELECT count(*)
                    FROM inventory.inv_stocks s
                    JOIN _legacy_managed_bins b ON b.bin_id=s."BinId"
                    WHERE NOT s."IsDeleted" AND s."Quantity">s."ReservedQuantity"
                      AND NOT EXISTS (
                        SELECT 1 FROM _legacy_stock_snapshot t
                        WHERE t.variant_id=s."VariantId" AND t.bin_id=s."BinId")
                    """, ct);
                var changed = checked((int)(updateCount + insertCount + zeroCount));

                if (!options.DryRun && changed > 0)
                {
                    await ExecAsync(pg, tx, """
                        UPDATE inventory.inv_stocks s
                        SET "Quantity"=t.available_qty + s."ReservedQuantity", "UpdatedAt"=now()
                        FROM _legacy_stock_snapshot t
                        WHERE s."VariantId"=t.variant_id AND s."BinId"=t.bin_id AND NOT s."IsDeleted"
                          AND s."Quantity" IS DISTINCT FROM (t.available_qty + s."ReservedQuantity")
                        """, ct);
                    await ExecAsync(pg, tx, """
                        INSERT INTO inventory.inv_stocks
                          ("Id","VariantId","WarehouseId","LocationId","SectionId","BinId","StockType",
                           "Quantity","ReservedQuantity","CreatedAt","IsDeleted")
                        SELECT gen_random_uuid(),t.variant_id,sec."WarehouseId",NULL,b."SectionId",t.bin_id,
                               'physical',t.available_qty,0,now(),false
                        FROM _legacy_stock_snapshot t
                        JOIN inventory.inv_warehouse_bins b ON b."Id"=t.bin_id
                        JOIN inventory.inv_warehouse_sections sec ON sec."Id"=b."SectionId"
                        WHERE NOT EXISTS (
                          SELECT 1 FROM inventory.inv_stocks s
                          WHERE s."VariantId"=t.variant_id AND s."BinId"=t.bin_id AND NOT s."IsDeleted")
                        """, ct);
                    await ExecAsync(pg, tx, """
                        UPDATE inventory.inv_stocks s
                        SET "Quantity"=s."ReservedQuantity", "UpdatedAt"=now()
                        FROM _legacy_managed_bins b
                        WHERE b.bin_id=s."BinId" AND NOT s."IsDeleted" AND s."Quantity">s."ReservedQuantity"
                          AND NOT EXISTS (
                            SELECT 1 FROM _legacy_stock_snapshot t
                            WHERE t.variant_id=s."VariantId" AND t.bin_id=s."BinId")
                        """, ct);
                }

                if (options.DryRun)
                    await tx.RollbackAsync(ct);
                else
                    await tx.CommitAsync(ct);

                if (!options.DryRun && changed > 0)
                    StockCacheInvalidation.Bust(cacheBust);

                var detail =
                    $"kaynakSatır={sourceSnapshot.Rows.Count}, kaynakAdet={sourceQuantity}, " +
                    $"kaynakRaf={sourceSnapshot.ManagedBinBarcodes.Count}, eşleşenAdet={mapped.Values.Sum(x => (long)x)}, " +
                    $"eşleşenKombin={mapped.Count}, eşleşmeyenSatır={unmappedRows}, eşleşmeyenAdet={unmappedQuantity}, " +
                    $"eşleşmeyenVaryantSatır={unmappedVariantRows}, eşleşmeyenVaryant={unmappedVariantBarcodes.Count}, " +
                    $"eşleşmeyenVaryantAdet={unmappedVariantQuantity}, eşleşmeyenRafSatır={unmappedBinRows}, " +
                    $"eşleşmeyenRaf={unmappedBinBarcodes.Count}, eşleşmeyenRafAdet={unmappedBinQuantity}, " +
                    $"silinmişVaryant={deletedVariantBarcodes.Count}, aktifÜründeEksikVaryant={missingVariantWithActiveProduct.Count}, " +
                    $"silinmişÜründeEksikVaryant={missingVariantWithDeletedProduct.Count}, ürünüOlmayanVaryant={missingVariantWithoutProduct.Count}, " +
                    $"pasifRaf={inactiveBinBarcodes.Count}, silinmişRaf={deletedBinBarcodes.Count}, " +
                    $"mevcutKısımdaEksikRaf={missingBinWithSection.Count}, kısmıOlmayanRaf={missingBinWithoutSection.Count}, " +
                    $"eşleşmemeÜstSınırı={options.MaximumUnmappedRows}/{options.MaximumUnmappedQuantity}, " +
                    $"güncellenecek={updateCount}, yeni={insertCount}, sıfırlanacak={zeroCount}";
                return new(true, options.DryRun, changed, detail, null, (int)sw.ElapsedMilliseconds);
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
            logger.LogError(ex, "Geçici MySQL stok senkronu başarısız");
            return new(false, options.DryRun, 0, string.Empty, ex.Message, (int)sw.ElapsedMilliseconds);
        }
    }

    private Task<SourceSnapshot> ReadSourceSnapshotAsync(CancellationToken ct) =>
        source.ExecuteReadAsync<SourceSnapshot>(async (connection, transaction, token) =>
        {
            var bins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var binCommand = connection.CreateCommand())
            {
                binCommand.Transaction = transaction;
                binCommand.CommandTimeout = options.CommandTimeoutSeconds;
                binCommand.CommandText = """
                    SELECT DISTINCT su.barcode
                    FROM dfstorageunits su
                    JOIN dfstorages st ON st.Id=su.storageId
                    WHERE st.type=@storageType AND st.status=1
                      AND su.barcode IS NOT NULL AND su.barcode<>''
                    """;
                binCommand.Parameters.AddWithValue("@storageType", options.StockStorageType);
                await using var reader = await binCommand.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token)) bins.Add(reader.GetString(0).Trim());
            }

            var rows = new List<SourceRow>();
            await using (var stockCommand = connection.CreateCommand())
            {
                stockCommand.Transaction = transaction;
                stockCommand.CommandTimeout = options.CommandTimeoutSeconds;
                stockCommand.CommandText = """
                    SELECT pv.barkod, su.barcode, p.urunKodu, st.code, su.shelfUnitNumber,
                           SUM(CASE WHEN pl.transactionDetailId IS NULL THEN 1 ELSE 0 END) AS availableQty
                    FROM opproductlocations pl
                    JOIN apurunvaryantlari pv ON pv.Id=pl.productVariantId
                    JOIN apurunler p ON p.Id=pv.urunId
                    JOIN dfstorageunits su ON su.Id=pl.storageUnitId
                    JOIN dfstorages st ON st.Id=su.storageId
                    WHERE st.type=@storageType AND st.status=1
                      AND pv.barkod IS NOT NULL AND pv.barkod<>''
                      AND su.barcode IS NOT NULL AND su.barcode<>''
                    GROUP BY pv.barkod, su.barcode, p.urunKodu, st.code, su.shelfUnitNumber
                    """;
                stockCommand.Parameters.AddWithValue("@storageType", options.StockStorageType);
                await using var reader = await stockCommand.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token))
                {
                    var quantity = reader.GetInt64(5);
                    if (quantity > int.MaxValue)
                        throw new InvalidOperationException("Legacy stok adedi Int32 sınırını aştı.");
                    rows.Add(new(
                        reader.GetString(0).Trim(),
                        reader.GetString(1).Trim(),
                        reader.IsDBNull(2) ? string.Empty : reader.GetString(2).Trim(),
                        reader.IsDBNull(3) ? string.Empty : reader.GetString(3).Trim(),
                        reader.IsDBNull(4) ? string.Empty : reader.GetString(4).Trim(),
                        (int)quantity));
                }
            }
            return new(rows, bins);
        }, ct);

    private static async Task<Dictionary<string, Guid>> LoadVariantMapAsync(
        NpgsqlConnection pg, NpgsqlTransaction tx, CancellationToken ct)
    {
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        await using var command = new NpgsqlCommand(
            "SELECT \"Barcode\",\"Id\" FROM catalog.product_variants WHERE NOT \"IsDeleted\" AND \"Barcode\" IS NOT NULL AND \"Barcode\"<>''",
            pg, tx);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result[reader.GetString(0)] = reader.GetGuid(1);
        return result;
    }

    private static async Task<Dictionary<string, TargetBin>> LoadBinMapAsync(
        NpgsqlConnection pg, NpgsqlTransaction tx, CancellationToken ct)
    {
        var result = new Dictionary<string, TargetBin>(StringComparer.OrdinalIgnoreCase);
        await using var command = new NpgsqlCommand("""
            SELECT b."Barcode",b."Id",b."SectionId",s."WarehouseId"
            FROM inventory.inv_warehouse_bins b
            JOIN inventory.inv_warehouse_sections s ON s."Id"=b."SectionId"
            WHERE NOT b."IsDeleted" AND b."IsActive" AND b."Barcode" IS NOT NULL AND b."Barcode"<>''
            """, pg, tx);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetString(0)] = new(reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3));
        return result;
    }

    private static async Task<Dictionary<string, bool>> LoadAllVariantStatusAsync(
        NpgsqlConnection pg, NpgsqlTransaction tx, CancellationToken ct)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        await using var command = new NpgsqlCommand(
            "SELECT \"Barcode\",\"IsDeleted\" FROM catalog.product_variants WHERE \"Barcode\" IS NOT NULL AND \"Barcode\"<>''",
            pg, tx);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result[reader.GetString(0)] = reader.GetBoolean(1);
        return result;
    }

    private static async Task<Dictionary<string, bool>> LoadProductStatusAsync(
        NpgsqlConnection pg, NpgsqlTransaction tx, CancellationToken ct)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        await using var command = new NpgsqlCommand(
            "SELECT \"Code\",\"IsDeleted\" FROM catalog.products WHERE \"Code\" IS NOT NULL AND \"Code\"<>''",
            pg, tx);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result[reader.GetString(0)] = reader.GetBoolean(1);
        return result;
    }

    private static async Task<Dictionary<string, (bool IsDeleted, bool IsActive)>> LoadAllBinStatusAsync(
        NpgsqlConnection pg, NpgsqlTransaction tx, CancellationToken ct)
    {
        var result = new Dictionary<string, (bool IsDeleted, bool IsActive)>(StringComparer.OrdinalIgnoreCase);
        await using var command = new NpgsqlCommand(
            "SELECT \"Barcode\",\"IsDeleted\",\"IsActive\" FROM inventory.inv_warehouse_bins WHERE \"Barcode\" IS NOT NULL AND \"Barcode\"<>''",
            pg, tx);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetString(0)] = (reader.GetBoolean(1), reader.GetBoolean(2));
        return result;
    }

    private static async Task<HashSet<string>> LoadActiveSectionCodesAsync(
        NpgsqlConnection pg, NpgsqlTransaction tx, CancellationToken ct)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = new NpgsqlCommand(
            "SELECT \"Code\" FROM inventory.inv_warehouse_sections WHERE NOT \"IsDeleted\" AND \"IsActive\" AND \"Code\" IS NOT NULL AND \"Code\"<>''",
            pg, tx);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetString(0));
        return result;
    }

    private static async Task CreateTempTablesAsync(NpgsqlConnection pg, NpgsqlTransaction tx, CancellationToken ct)
    {
        await ExecAsync(pg, tx, """
            CREATE TEMP TABLE _legacy_stock_snapshot(
              variant_id uuid NOT NULL,
              bin_id uuid NOT NULL,
              available_qty integer NOT NULL CHECK (available_qty >= 0),
              PRIMARY KEY(variant_id,bin_id)) ON COMMIT DROP;
            CREATE TEMP TABLE _legacy_managed_bins(
              bin_id uuid PRIMARY KEY) ON COMMIT DROP;
            """, ct);
    }

    private static async Task CopySnapshotAsync(
        NpgsqlConnection pg, IReadOnlyDictionary<(Guid VariantId, Guid BinId), int> rows, CancellationToken ct)
    {
        await using var writer = await pg.BeginBinaryImportAsync(
            "COPY _legacy_stock_snapshot (variant_id,bin_id,available_qty) FROM STDIN (FORMAT BINARY)", ct);
        foreach (var row in rows)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(row.Key.VariantId, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(row.Key.BinId, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(row.Value, NpgsqlDbType.Integer, ct);
        }
        await writer.CompleteAsync(ct);
    }

    private static async Task CopyManagedBinsAsync(NpgsqlConnection pg, IReadOnlyList<Guid> bins, CancellationToken ct)
    {
        await using var writer = await pg.BeginBinaryImportAsync(
            "COPY _legacy_managed_bins (bin_id) FROM STDIN (FORMAT BINARY)", ct);
        foreach (var bin in bins)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(bin, NpgsqlDbType.Uuid, ct);
        }
        await writer.CompleteAsync(ct);
    }

    private static async Task<int> ExecAsync(
        NpgsqlConnection pg, NpgsqlTransaction tx, string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, pg, tx) { CommandTimeout = 300 };
        return await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<T> ScalarAsync<T>(
        NpgsqlConnection pg, NpgsqlTransaction tx, string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, pg, tx) { CommandTimeout = 300 };
        var value = await command.ExecuteScalarAsync(ct);
        return (T)Convert.ChangeType(value!, typeof(T));
    }

    private sealed record SourceRow(
        string VariantBarcode,
        string BinBarcode,
        string ProductCode,
        string StorageCode,
        string ShelfUnitNumber,
        int AvailableQuantity);
    private sealed record SourceSnapshot(IReadOnlyList<SourceRow> Rows, IReadOnlySet<string> ManagedBinBarcodes);
    private sealed record TargetBin(Guid Id, Guid SectionId, Guid WarehouseId);
}
