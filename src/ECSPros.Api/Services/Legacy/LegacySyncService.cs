using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using Npgsql;

namespace ECSPros.Api.Services.Legacy;

/// <summary>
/// Part B / B1-B3 (2026-08-01): eski sistem (juludedb) → yeni katalog periyodik senkronu.
/// MigrationTool Faz 26 (fiyat/görsel/stok, ID-koruyan UPDATE), Faz 27 (kanal fiyatı) ve
/// Faz 5/6/7'nin YENİ-ÜRÜN alt kümesinin (B1) çalışan API'ye portudur; SQL'ler batch
/// araçtakiyle birebir aynı kuralları uygular. LegacySyncWorker zamanlar.
///
/// GÜVENLİK KADEMELERİ (B4 ile aynı felsefe): (1) Legacy:MySqlConnection dolu,
/// (2) Legacy:Sync:Enabled=true, (3) Legacy:Sync:DryRun=false → ancak o zaman yazar.
/// DryRun'da tüm okuma/karşılaştırma yapılır, plan sayıları raporlanır, hiçbir şey yazılmaz.
///
/// Bilinen sınırlar: erp_variant_data yeni ürünler için yazılmaz (nebim ERP eşlemesi ayrı iş);
/// Redis'te desen-bazlı silme olmadığından fiyat/stok değişimi vitrine cache TTL'i (≤10 dk)
/// içinde yansır; materyalize (statik) kanal kategorilerine yeni ürün otomatik girmez.
/// </summary>
public sealed class LegacySyncService(
    NpgsqlDataSource dataSource,
    IConfiguration config,
    ILogger<LegacySyncService> logger)
{
    private const string DEF = "definition";
    private const string CAT = "catalog";
    private (DateTime CreatedAt, string Code)? _missingImageCursor;

    private string MySqlConn => config["Legacy:MySqlConnection"] ?? "";
    private int LegacyPlatformId => config.GetValue("Legacy:MisharLegacyPlatformId", 41);
    private bool DryRun => config.GetValue("Legacy:Sync:DryRun", true);
    private bool PricesEnabled => config.GetValue("Legacy:Sync:Prices", true);
    private bool StockEnabled => config.GetValue("Legacy:Sync:Stock", true);
    private DateTime ProductCreatedAfter =>
        config.GetValue("Legacy:Sync:ProductCreatedAfter", new DateTime(2026, 7, 9));

    public bool IsConfigured => !string.IsNullOrWhiteSpace(MySqlConn);

    public sealed record Report(bool Success, bool DryRun, string Slice, int Changed, string Detail, string? Error, int DurationMs);

    // ─── DİLİM 1: FİYAT + KANAL FİYATI + STOK (sık, ~10 dk) ─────────────────
    public async Task<Report> SyncPriceAndStockAsync(CancellationToken ct)
    {
        var t0 = DateTime.UtcNow;
        var log = new StringBuilder();
        bool dry = DryRun;
        bool pricesEnabled = PricesEnabled;
        bool stockEnabled = StockEnabled;
        string slice = pricesEnabled
            ? stockEnabled ? "pricestock" : "price"
            : stockEnabled ? "stock" : "pricestock";

        if (!pricesEnabled && !stockEnabled)
        {
            log.AppendLine("[FİYAT/STOK] Prices=false ve Stock=false; dilim kapalı.");
            return new(true, dry, slice, 0, log.ToString(), null, DurMs(t0));
        }

        try
        {
            await using var pg = await dataSource.OpenConnectionAsync(ct);
            await using var my = new MySqlConnection(MySqlConn);
            await my.OpenAsync(ct);

            int changed = 0;
            var barcodeToVariant = await PgMapAsync(pg, $"SELECT \"Barcode\", \"Id\" FROM {CAT}.product_variants WHERE \"IsDeleted\"=false AND \"Barcode\" IS NOT NULL AND \"Barcode\"<>''", ct);
            var legacyVariantBarcode = await MyIntStringMapAsync(my, "SELECT Id, barkod FROM apurunvaryantlari WHERE barkod IS NOT NULL AND barkod<>''", ct);

            if (pricesEnabled)
            {
                changed += await SyncBasePriceAsync(pg, my, log, dry, ct);
                changed += await SyncChannelPriceAsync(pg, my, log, dry, barcodeToVariant, legacyVariantBarcode, ct);
            }
            else
            {
                log.AppendLine("[FİYAT] kapalı; [KANAL FİYAT] kapalı.");
            }

            if (stockEnabled)
                changed += await SyncStockAsync(pg, my, log, dry, barcodeToVariant, legacyVariantBarcode, ct);
            else
                log.AppendLine("[STOK] kapalı.");

            if (!dry && changed > 0)
            {
                if (pricesEnabled)
                {
                    await PgExecAsync(pg, $"ANALYZE {CAT}.products", ct);
                    await PgExecAsync(pg, "ANALYZE storefront.channel_variants", ct);
                }
                if (stockEnabled)
                    await PgExecAsync(pg, "ANALYZE inventory.inv_stocks", ct);
            }
            return new(true, dry, slice, changed, log.ToString(), null, DurMs(t0));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Legacy senkron (fiyat/stok) hatası");
            return new(false, dry, slice, 0, log.ToString(), ex.Message, DurMs(t0));
        }
    }

    // Faz 26a portu: apurunler.satisFiyati/alisFiyati/kdvOrani → products (Code ile).
    private async Task<int> SyncBasePriceAsync(NpgsqlConnection pg, MySqlConnection my, StringBuilder log, bool dry, CancellationToken ct)
    {
        await PgExecAsync(pg, "DROP TABLE IF EXISTS _ls_price", ct);
        await PgExecAsync(pg, "CREATE TEMP TABLE _ls_price(code text PRIMARY KEY, price numeric, cost numeric, tax int)", ct);

        var batch = new List<object?[]>();
        int okunan = 0;
        // Keep-listesi + kesim sonrası açılan yeni ürünler (JOIN products zaten korur; bu filtre okuma hacmi için)
        await using (var r = await MyQueryAsync(my, @"SELECT urunKodu, satisFiyati, alisFiyati, kdvOrani FROM apurunler
            WHERE urunKodu IS NOT NULL AND urunKodu<>''
            AND (urunKodu IN (SELECT urunkodu FROM yeniurunkodlari) OR olusturmaTarihi >= @cutoff)",
            ct, ("@cutoff", ProductCreatedAfter)))
            while (await r.ReadAsync(ct))
            {
                string kod = r.GetString(0);
                decimal price = r.IsDBNull(1) ? 0 : (decimal)r.GetDouble(1);
                decimal? cost = r.IsDBNull(2) ? null : (decimal)r.GetDouble(2);
                int tax = r.IsDBNull(3) ? 20 : r.GetInt32(3);
                batch.Add(new object?[] { kod, price, cost == 0m ? null : cost, tax });
                okunan++;
                if (batch.Count >= 1000) { await PgBatchInsertAsync(pg, "_ls_price", new[] { "code", "price", "cost", "tax" }, new string?[4], batch, ct); batch.Clear(); }
            }
        await PgBatchInsertAsync(pg, "_ls_price", new[] { "code", "price", "cost", "tax" }, new string?[4], batch, ct);

        long degisecek = await PgScalarAsync<long>(pg, $@"SELECT COUNT(*) FROM {CAT}.products p JOIN _ls_price t ON p.""Code""=t.code
            WHERE p.""IsDeleted""=false AND (p.""BasePrice"" IS DISTINCT FROM t.price
                OR p.""BaseCost"" IS DISTINCT FROM t.cost OR p.""TaxRate"" IS DISTINCT FROM t.tax)", ct);
        log.AppendLine($"[FİYAT] eski listede {okunan} ürün; değişecek: {degisecek}.");
        if (!dry && degisecek > 0)
            await PgExecAsync(pg, $@"UPDATE {CAT}.products p SET ""BasePrice""=t.price, ""BaseCost""=t.cost, ""TaxRate""=t.tax,
                ""UpdatedAt""=now() FROM _ls_price t WHERE p.""Code""=t.code AND p.""IsDeleted""=false
                AND (p.""BasePrice"" IS DISTINCT FROM t.price OR p.""BaseCost"" IS DISTINCT FROM t.cost OR p.""TaxRate"" IS DISTINCT FROM t.tax)", ct);
        return (int)degisecek;
    }

    // Faz 27 portu: plurunler (platform 41) → channel_variants Price/CompareAt/IsActive (mishar).
    private async Task<int> SyncChannelPriceAsync(NpgsqlConnection pg, MySqlConnection my, StringBuilder log, bool dry,
        Dictionary<string, Guid> barcodeToVariant, Dictionary<int, string> legacyVariantBarcode, CancellationToken ct)
    {
        var fp = await PgScalarAsync<Guid>(pg, "SELECT \"Id\" FROM core.core_firm_platforms WHERE \"Code\"='mishar'", ct);

        await PgExecAsync(pg, "DROP TABLE IF EXISTS _ls_cv", ct);
        await PgExecAsync(pg, "CREATE TEMP TABLE _ls_cv(variant_id uuid PRIMARY KEY, price numeric, compare_at numeric, is_active boolean)", ct);

        var batch = new List<object?[]>();
        var eklendi = new HashSet<Guid>();
        int okunan = 0, atlanan = 0;
        // Kural MigrateChannelDataForPlatform/Faz 27 ile birebir: satis>0 ? satis : null;
        // compareAt = liste>0 && liste!=satis ? liste : null; is_active = satista.
        await using (var r = await MyQueryAsync(my, $"SELECT urunAnaVaryantId, satisFiyati, listeFiyati, satista FROM plurunler WHERE platformId={LegacyPlatformId}", ct))
            while (await r.ReadAsync(ct))
            {
                int lvid = r.GetInt32(0);
                if (!legacyVariantBarcode.TryGetValue(lvid, out var bc) || !barcodeToVariant.TryGetValue(bc, out var vg)) { atlanan++; continue; }
                if (!eklendi.Add(vg)) continue;
                decimal satis = r.IsDBNull(1) ? 0 : (decimal)r.GetDouble(1);
                decimal liste = r.IsDBNull(2) ? 0 : (decimal)r.GetDouble(2);
                bool satista = !r.IsDBNull(3) && Convert.ToBoolean(r.GetValue(3));
                batch.Add(new object?[] { vg, satis > 0 ? satis : null, liste > 0 && liste != satis ? liste : null, satista });
                okunan++;
                if (batch.Count >= 1000) { await PgBatchInsertAsync(pg, "_ls_cv", new[] { "variant_id", "price", "compare_at", "is_active" }, new string?[4], batch, ct); batch.Clear(); }
            }
        await PgBatchInsertAsync(pg, "_ls_cv", new[] { "variant_id", "price", "compare_at", "is_active" }, new string?[4], batch, ct);

        long degisecek = await PgScalarAsync<long>(pg, $@"SELECT COUNT(*) FROM storefront.channel_variants cv JOIN _ls_cv t ON cv.""VariantId""=t.variant_id
            WHERE cv.""FirmPlatformId""='{fp}' AND cv.""IsDeleted""=false
            AND (cv.""Price"" IS DISTINCT FROM t.price OR cv.""CompareAtPrice"" IS DISTINCT FROM t.compare_at OR cv.""IsActive"" IS DISTINCT FROM t.is_active)", ct);
        long yeni = await PgScalarAsync<long>(pg, $@"SELECT COUNT(*) FROM _ls_cv t WHERE NOT EXISTS (
            SELECT 1 FROM storefront.channel_variants cv WHERE cv.""FirmPlatformId""='{fp}' AND cv.""VariantId""=t.variant_id AND cv.""IsDeleted""=false)", ct);
        log.AppendLine($"[KANAL FİYAT] eşleşen {okunan} varyant (atlanan {atlanan}); değişecek: {degisecek}, yeni: {yeni}.");
        if (!dry && (degisecek > 0 || yeni > 0))
        {
            await PgExecAsync(pg, $@"UPDATE storefront.channel_variants cv SET ""Price""=t.price, ""CompareAtPrice""=t.compare_at,
                ""IsActive""=t.is_active, ""PriceType""=CASE WHEN t.price IS NOT NULL THEN 'manual' ELSE NULL END, ""UpdatedAt""=now()
                FROM _ls_cv t WHERE cv.""VariantId""=t.variant_id AND cv.""FirmPlatformId""='{fp}' AND cv.""IsDeleted""=false
                AND (cv.""Price"" IS DISTINCT FROM t.price OR cv.""CompareAtPrice"" IS DISTINCT FROM t.compare_at OR cv.""IsActive"" IS DISTINCT FROM t.is_active)", ct);
            await PgExecAsync(pg, $@"INSERT INTO storefront.channel_variants (""Id"",""FirmPlatformId"",""VariantId"",""PriceType"",""Price"",""CompareAtPrice"",""IsActive"",""CreatedAt"",""IsDeleted"")
                SELECT gen_random_uuid(), '{fp}', t.variant_id, CASE WHEN t.price IS NOT NULL THEN 'manual' ELSE NULL END, t.price, t.compare_at, t.is_active, now(), false
                FROM _ls_cv t WHERE NOT EXISTS (SELECT 1 FROM storefront.channel_variants cv WHERE cv.""FirmPlatformId""='{fp}' AND cv.""VariantId""=t.variant_id AND cv.""IsDeleted""=false)", ct);
        }
        return (int)(degisecek + yeni);
    }

    // Faz 26c portu: opproductlocations (yalnız REZERVSİZ satırlar) → inv_stocks.Quantity.
    // Rezervasyonlara DOKUNULMAZ; Quantity asla rezervin altına düşürülmez.
    private async Task<int> SyncStockAsync(NpgsqlConnection pg, MySqlConnection my, StringBuilder log, bool dry,
        Dictionary<string, Guid> barcodeToVariant, Dictionary<int, string> legacyVariantBarcode, CancellationToken ct)
    {
        var binByBarcode = await PgMapAsync(pg, "SELECT \"Barcode\", \"Id\" FROM inventory.inv_warehouse_bins WHERE \"Barcode\" IS NOT NULL AND \"Barcode\"<>''", ct);
        var unitBarcode = await MyIntStringMapAsync(my, "SELECT Id, barcode FROM dfstorageunits WHERE barcode IS NOT NULL AND barcode<>''", ct);

        await PgExecAsync(pg, "DROP TABLE IF EXISTS _ls_stock", ct);
        await PgExecAsync(pg, "CREATE TEMP TABLE _ls_stock(variant_id uuid, bin_id uuid, qty int, PRIMARY KEY(variant_id, bin_id))", ct);

        var batch = new List<object?[]>();
        var eklendi = new HashSet<(Guid, Guid)>();
        int okunan = 0, atlananAdet = 0;
        // 2026-08-07 (kullanıcı kararı): stok kaynağı YALNIZ dfstoragetypes Id=1 "İnternete Açık"
        // depolar. Filtresiz sayım Mağaza Reyonu/İade/Defo/Bağış (tip=2) adetlerini de Merkez
        // Depo'nun online stoğuna karıştırıyordu (~26 bin adet).
        await using (var r = await MyQueryAsync(my, @"SELECT pl.productVariantId, pl.storageUnitId,
                SUM(CASE WHEN pl.transactionDetailId IS NULL THEN 1 ELSE 0 END) AS adet
            FROM opproductlocations pl
            JOIN dfstorageunits su ON su.Id=pl.storageUnitId
            JOIN dfstorages st ON st.Id=su.storageId
            WHERE st.type=@stype AND st.status=1
            GROUP BY pl.productVariantId, pl.storageUnitId", ct,
            ("@stype", config.GetValue("Legacy:Sync:StockStorageType", 1))))
            while (await r.ReadAsync(ct))
            {
                int lvid = r.GetInt32(0), luid = r.GetInt32(1);
                int adet = Convert.ToInt32(r.GetValue(2));
                if (!legacyVariantBarcode.TryGetValue(lvid, out var vbc) || !barcodeToVariant.TryGetValue(vbc, out var vg)
                    || !unitBarcode.TryGetValue(luid, out var ubc) || !binByBarcode.TryGetValue(ubc, out var bg)) { atlananAdet += adet; continue; }
                if (!eklendi.Add((vg, bg))) continue;
                batch.Add(new object?[] { vg, bg, adet });
                okunan++;
                if (batch.Count >= 1000) { await PgBatchInsertAsync(pg, "_ls_stock", new[] { "variant_id", "bin_id", "qty" }, new string?[3], batch, ct); batch.Clear(); }
            }
        await PgBatchInsertAsync(pg, "_ls_stock", new[] { "variant_id", "bin_id", "qty" }, new string?[3], batch, ct);

        long guncellenecek = await PgScalarAsync<long>(pg, @"SELECT COUNT(*) FROM inventory.inv_stocks s JOIN _ls_stock t ON s.""VariantId""=t.variant_id AND s.""BinId""=t.bin_id
            WHERE s.""IsDeleted""=false AND s.""Quantity"" IS DISTINCT FROM GREATEST(t.qty, s.""ReservedQuantity"")", ct);
        long yeniKombin = await PgScalarAsync<long>(pg, @"SELECT COUNT(*) FROM _ls_stock t WHERE NOT EXISTS (
            SELECT 1 FROM inventory.inv_stocks s WHERE s.""VariantId""=t.variant_id AND s.""BinId""=t.bin_id AND s.""IsDeleted""=false)", ct);
        long sifirlanacak = await PgScalarAsync<long>(pg, @"SELECT COUNT(*) FROM inventory.inv_stocks s WHERE s.""IsDeleted""=false AND s.""Quantity"">s.""ReservedQuantity""
            AND NOT EXISTS (SELECT 1 FROM _ls_stock t WHERE t.variant_id=s.""VariantId"" AND t.bin_id=s.""BinId"")", ct);
        log.AppendLine($"[STOK] eski kombinasyon {okunan} (atlanan adet {atlananAdet}); güncellenecek: {guncellenecek}, yeni: {yeniKombin}, sıfırlanacak: {sifirlanacak}.");

        if (!dry && (guncellenecek > 0 || yeniKombin > 0 || sifirlanacak > 0))
        {
            await PgExecAsync(pg, @"UPDATE inventory.inv_stocks s SET ""Quantity""=GREATEST(t.qty, s.""ReservedQuantity""), ""UpdatedAt""=now()
                FROM _ls_stock t WHERE s.""VariantId""=t.variant_id AND s.""BinId""=t.bin_id AND s.""IsDeleted""=false
                AND s.""Quantity"" IS DISTINCT FROM GREATEST(t.qty, s.""ReservedQuantity"")", ct);
            await PgExecAsync(pg, @"INSERT INTO inventory.inv_stocks (""Id"",""VariantId"",""WarehouseId"",""LocationId"",""SectionId"",""BinId"",""StockType"",""Quantity"",""ReservedQuantity"",""CreatedAt"",""IsDeleted"")
                SELECT gen_random_uuid(), t.variant_id, sec.""WarehouseId"", NULL, b.""SectionId"", t.bin_id, 'physical', t.qty, 0, now(), false
                FROM _ls_stock t JOIN inventory.inv_warehouse_bins b ON b.""Id""=t.bin_id JOIN inventory.inv_warehouse_sections sec ON sec.""Id""=b.""SectionId""
                WHERE NOT EXISTS (SELECT 1 FROM inventory.inv_stocks s WHERE s.""VariantId""=t.variant_id AND s.""BinId""=t.bin_id AND s.""IsDeleted""=false)", ct);
            await PgExecAsync(pg, @"UPDATE inventory.inv_stocks s SET ""Quantity""=s.""ReservedQuantity"", ""UpdatedAt""=now()
                WHERE s.""IsDeleted""=false AND s.""Quantity"">s.""ReservedQuantity""
                AND NOT EXISTS (SELECT 1 FROM _ls_stock t WHERE t.variant_id=s.""VariantId"" AND t.bin_id=s.""BinId"")", ct);
        }
        return (int)(guncellenecek + yeniKombin + sifirlanacak);
    }

    // ─── DİLİM 2: GÖRSELLER (seyrek) — Faz 26b portu ─────────────────────────
    // product_images'a gelen FK yok → drift varsa tek transaction'da sil + yeniden kur.
    public Task<Report> SyncImagesAsync(CancellationToken ct) =>
        SyncImagesCoreAsync(MySqlConn, LegacyPlatformId, DryRun, ct);

    /// <summary>
    /// İzole LegacyImport worker'ının SELECT-only MySQL bağlantısıyla görsel metadata uzlaştırması.
    /// Fiziksel görsel dosyalarına dokunmaz; yalnız catalog.product_images kayıtlarını yeniler.
    /// </summary>
    public Task<Report> SyncImagesFromReadOnlySourceAsync(
        string sourceConnectionString, int legacyPlatformId, bool dryRun, CancellationToken ct) =>
        SyncImagesCoreAsync(sourceConnectionString, legacyPlatformId, dryRun, ct);

    private async Task<Report> SyncImagesCoreAsync(
        string sourceConnectionString, int legacyPlatformId, bool dryRun, CancellationToken ct)
    {
        var t0 = DateTime.UtcNow;
        var log = new StringBuilder();
        bool dry = dryRun;
        try
        {
            if (string.IsNullOrWhiteSpace(sourceConnectionString))
                return new(false, dry, "images", 0, log.ToString(),
                    "Legacy görsel kaynağı ConnectionString boş.", DurMs(t0));

            await using var pg = await dataSource.OpenConnectionAsync(ct);
            await using var my = new MySqlConnection(sourceConnectionString);
            await my.OpenAsync(ct);

            var codeToProduct = await PgMapAsync(pg, $"SELECT \"Code\", \"Id\" FROM {CAT}.products WHERE \"IsDeleted\"=false", ct);
            var barcodeToVariant = await PgMapAsync(pg, $"SELECT \"Barcode\", \"Id\" FROM {CAT}.product_variants WHERE \"IsDeleted\"=false AND \"Barcode\" IS NOT NULL AND \"Barcode\"<>''", ct);
            var legacyVariantBarcode = await MyIntStringMapAsync(my, "SELECT Id, barkod FROM apurunvaryantlari WHERE barkod IS NOT NULL AND barkod<>''", ct);
            var legacyProductCode = new Dictionary<int, string>();
            await using (var r = await MyQueryAsync(my, @"SELECT Id, urunKodu FROM apurunler WHERE urunKodu IS NOT NULL AND urunKodu<>''
                AND (urunKodu IN (SELECT urunkodu FROM yeniurunkodlari) OR olusturmaTarihi >= @cutoff)", ct, ("@cutoff", ProductCreatedAfter)))
                while (await r.ReadAsync(ct)) legacyProductCode[r.GetInt32(0)] = r.GetString(1);
            var imageSetMap = await LoadImageSetMapAsync(pg, my, ct);
            if (imageSetMap.Count == 0)
                return new(false, dry, "images", 0, log.ToString(), "image_sets eşlemesi boş — senkron atlandı.", DurMs(t0));

            // Varyant başına tek set: platformun resim seti (dfplatforms.resimSetId) öncelikli;
            // yoksa en yeni yüklenen set, eşitlikte çok resimli, sonra küçük setId.
            int preferredSet = await LoadPreferredImageSetAsync(my, legacyPlatformId, ct);
            var chosenSet = new Dictionary<(int, int?), (int setId, int cnt, DateTime maxT)>();
            // maxT string olarak çekilir: legacy'de '0000-00-00' sıfır-tarihler var, GetDateTime patlar.
            await using (var rs = await MyQueryAsync(my, @"SELECT urunId, urunAnaVaryantId, IFNULL(resimSetId,1) AS setId, COUNT(*) AS c,
                    DATE_FORMAT(MAX(IFNULL(olusturmaTarihi,'1900-01-01')),'%Y-%m-%d %H:%i:%s') AS maxT
                FROM apurunresimleri WHERE isSilindi=0 AND resimDosyaAdi IS NOT NULL AND resimDosyaAdi<>''
                GROUP BY urunId, urunAnaVaryantId, IFNULL(resimSetId,1)", ct))
                while (await rs.ReadAsync(ct))
                {
                    var key = (rs.GetInt32(0), rs.IsDBNull(1) ? (int?)null : rs.GetInt32(1));
                    int setId = rs.GetInt32(2), c = Convert.ToInt32(rs.GetValue(3));
                    var maxT = !rs.IsDBNull(4) && DateTime.TryParse(rs.GetString(4), out var d) ? d : DateTime.MinValue;
                    SetSecimBirlestir(chosenSet, key, setId, c, maxT, preferredSet);
                }

            await PgExecAsync(pg, "DROP TABLE IF EXISTS _ls_img", ct);
            await PgExecAsync(pg, "CREATE TEMP TABLE _ls_img(product_id uuid, variant_id uuid, set_id uuid, file_name text, sort_order int, is_variant_cover boolean)", ct);
            Guid defaultSetId = imageSetMap.Values.First();
            var seen = new HashSet<(Guid, Guid?, string)>();
            var variantFirst = new HashSet<int>();
            var batch = new List<object?[]>();
            int yeniSatir = 0, atlanan = 0;
            await using (var r = await MyQueryAsync(my, @"SELECT resimSetId, urunId, urunAnaVaryantId, resimDosyaAdi, siraNo
                FROM apurunresimleri WHERE isSilindi=0 AND resimDosyaAdi IS NOT NULL AND resimDosyaAdi<>''
                ORDER BY urunId, urunAnaVaryantId, siraNo", ct))
                while (await r.ReadAsync(ct))
                {
                    int oldSetId = r.IsDBNull(0) ? 1 : r.GetInt32(0);
                    int urunId = r.GetInt32(1);
                    int? variantOldId = r.IsDBNull(2) ? null : r.GetInt32(2);
                    string fileName = r.GetString(3);
                    int siraNo = r.IsDBNull(4) ? 0 : r.GetInt32(4);

                    if (!legacyProductCode.TryGetValue(urunId, out var kod) || !codeToProduct.TryGetValue(kod, out var productGuid)) { atlanan++; continue; }
                    if (chosenSet.TryGetValue((urunId, variantOldId), out var cs) && cs.setId != oldSetId) continue;

                    Guid? variantGuid = null;
                    if (variantOldId.HasValue && legacyVariantBarcode.TryGetValue(variantOldId.Value, out var vbc)
                        && barcodeToVariant.TryGetValue(vbc, out var vg)) variantGuid = vg;

                    var setId = imageSetMap.TryGetValue(oldSetId, out var sid) ? sid : defaultSetId;
                    if (!seen.Add((productGuid, variantGuid, fileName))) continue;
                    bool isVariantCover = variantOldId.HasValue && variantFirst.Add(variantOldId.Value);

                    batch.Add(new object?[] { productGuid, variantGuid, setId, fileName, siraNo, isVariantCover });
                    yeniSatir++;
                    if (batch.Count >= 1000) { await PgBatchInsertAsync(pg, "_ls_img", new[] { "product_id", "variant_id", "set_id", "file_name", "sort_order", "is_variant_cover" }, new string?[6], batch, ct); batch.Clear(); }
                }
            await PgBatchInsertAsync(pg, "_ls_img", new[] { "product_id", "variant_id", "set_id", "file_name", "sort_order", "is_variant_cover" }, new string?[6], batch, ct);

            // Tekil sayım: eşzamanlı çift kurulum kazasında (2026-08-05) ham COUNT ikiye katlanıp
            // %90 emniyetini kalıcı kilitledi — kıyas her zaman tekil dosya üzerinden.
            long mevcut = await PgScalarAsync<long>(pg, $@"SELECT COUNT(*) FROM (
                SELECT DISTINCT ""ProductId"",""VariantId"",""FileName"" FROM {CAT}.product_images) d", ct);
            long yeniDosya = await PgScalarAsync<long>(pg, $@"SELECT COUNT(*) FROM _ls_img n WHERE NOT EXISTS (
                SELECT 1 FROM {CAT}.product_images o WHERE o.""ProductId""=n.product_id AND o.""FileName""=n.file_name AND o.""IsDeleted""=false)", ct);
            long bayatDosya = await PgScalarAsync<long>(pg, $@"SELECT COUNT(*) FROM {CAT}.product_images o WHERE o.""IsDeleted""=false AND NOT EXISTS (
                SELECT 1 FROM _ls_img n WHERE n.product_id=o.""ProductId"" AND n.file_name=o.""FileName"")", ct);
            log.AppendLine($"[GÖRSEL] mevcut {mevcut}, yeni set {yeniSatir} (atlanan {atlanan}); tazelenecek: {yeniDosya}, bayat: {bayatDosya}.");

            if (yeniDosya == 0 && bayatDosya == 0)
                return new(true, dry, "images", 0, log.ToString(), null, DurMs(t0));

            // Otomasyon emniyeti (batch araçta yoktu): eski DB'den eksik/yarım veri gelirse
            // kataloğun görsellerini silip küçük bir setle değiştirmeyelim.
            if (mevcut > 0 && yeniSatir < mevcut * 0.9)
                return new(false, dry, "images", 0, log.ToString(),
                    $"EMNİYET: yeni set ({yeniSatir}) mevcudun ({mevcut}) %90'ından küçük — yeniden kurma reddedildi.", DurMs(t0));

            if (!dry)
            {
                await using var tx = await pg.BeginTransactionAsync(ct);
                try
                {
                    // Tek kurucu: ikinci bir instance (ör. staging) aynı anda koşarsa tablo çift
                    // kurulur — kilidi alamayan vazgeçer.
                    bool kilit = await PgScalarAsync<bool>(pg,
                        "SELECT pg_try_advisory_xact_lock(hashtext('legacy_sync_images'))", ct, tx);
                    if (!kilit)
                    {
                        await tx.RollbackAsync(ct);
                        return new(false, dry, "images", 0, log.ToString(),
                            "Başka bir instance görsel setini kuruyor — bu koşu atlandı.", DurMs(t0));
                    }
                    await PgExecAsync(pg, $"DELETE FROM {CAT}.product_images", ct, tx, timeoutSec: 300);
                    await PgExecAsync(pg, $@"INSERT INTO {CAT}.product_images
                        (""Id"",""ProductId"",""VariantId"",""ImageSetId"",""FileName"",""SortOrder"",""IsProductCover"",""IsVariantCover"",""Status"",""BatchId"",""CreatedAt"",""IsDeleted"")
                        SELECT gen_random_uuid(), product_id, variant_id, set_id, file_name, sort_order, false, COALESCE(is_variant_cover,false), 'Active', '{Guid.NewGuid()}', now(), false FROM _ls_img", ct, tx, timeoutSec: 600);
                    await tx.CommitAsync(ct);
                }
                catch { await tx.RollbackAsync(ct); throw; }
                await PgExecAsync(pg, $"ANALYZE {CAT}.product_images", ct);
            }
            return new(true, dry, "images", (int)(yeniDosya + bayatDosya), log.ToString(), null, DurMs(t0));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Legacy senkron (görsel) hatası");
            return new(false, dry, "images", 0, log.ToString(), ex.Message, DurMs(t0));
        }
    }

    /// <summary>
    /// Görseli olmayan sınırlı bir ürün grubunu MySQL'den hedefli tamamlar. Tam katalog taraması
    /// yapmaz ve mevcut ürün görsellerini silmez; sık kadansta düşük maliyetli toparlama içindir.
    /// </summary>
    public async Task<Report> SyncMissingImagesFromReadOnlySourceAsync(
        string sourceConnectionString, int legacyPlatformId, bool dryRun, int batchSize, CancellationToken ct)
    {
        var t0 = DateTime.UtcNow;
        var log = new StringBuilder();
        try
        {
            if (string.IsNullOrWhiteSpace(sourceConnectionString))
                return new(false, dryRun, "images-missing", 0, log.ToString(),
                    "Legacy görsel kaynağı ConnectionString boş.", DurMs(t0));

            await using var pg = await dataSource.OpenConnectionAsync(ct);
            var missing = await ReadMissingImageProductsAsync(pg, _missingImageCursor, batchSize, ct);
            if (missing.Count == 0 && _missingImageCursor is not null)
            {
                _missingImageCursor = null;
                missing = await ReadMissingImageProductsAsync(pg, null, batchSize, ct);
            }
            if (missing.Count == 0)
                return new(true, dryRun, "images-missing", 0,
                    "[GÖRSEL-HEDEFLİ] Görselsiz ürün bulunmadı.", null, DurMs(t0));
            _missingImageCursor = (missing[^1].CreatedAt, missing[^1].Code);

            await using var my = new MySqlConnection(sourceConnectionString);
            await my.OpenAsync(ct);
            var productByLegacyId = await LoadLegacyProductsByCodesAsync(my, missing, ct);
            if (productByLegacyId.Count == 0)
                return new(true, dryRun, "images-missing", 0,
                    $"[GÖRSEL-HEDEFLİ] Aday {missing.Count}; MySQL ürün eşleşmesi yok.", null, DurMs(t0));

            var imageSetMap = await LoadImageSetMapAsync(pg, my, ct);
            if (imageSetMap.Count == 0)
                return new(false, dryRun, "images-missing", 0, log.ToString(),
                    "image_sets eşlemesi boş — hedefli senkron atlandı.", DurMs(t0));

            int preferredSet = await LoadPreferredImageSetAsync(my, legacyPlatformId, ct);
            var legacyIds = productByLegacyId.Keys.ToArray();
            var chosenSets = await LoadChosenImageSetsAsync(my, legacyIds, preferredSet, ct);
            var sourceImages = await LoadTargetedSourceImagesAsync(
                my, legacyIds, productByLegacyId, chosenSets, ct);
            var targetVariants = await LoadTargetVariantsAsync(
                pg, missing.Select(x => x.ProductId).ToArray(), ct);

            int validRows = sourceImages.Count(x =>
                x.LegacyVariantId is null ||
                (!string.IsNullOrWhiteSpace(x.Barcode) && targetVariants.ContainsKey(x.Barcode)));
            log.AppendLine(
                $"[GÖRSEL-HEDEFLİ] aday={missing.Count}, MySQL ürün={productByLegacyId.Count}, kaynak görsel={sourceImages.Count}, yazılabilir={validRows}.");
            if (dryRun || validRows == 0)
                return new(true, dryRun, "images-missing", validRows, log.ToString(), null, DurMs(t0));

            await using var tx = await pg.BeginTransactionAsync(ct);
            bool locked = await PgScalarAsync<bool>(pg,
                "SELECT pg_try_advisory_xact_lock(hashtext('legacy_sync_images'))", ct, tx);
            if (!locked)
            {
                await tx.RollbackAsync(ct);
                return new(false, dryRun, "images-missing", 0, log.ToString(),
                    "Başka bir instance görsel metadata'sını güncelliyor — bu tur atlandı.", DurMs(t0));
            }

            int inserted = 0;
            var firstVariants = new HashSet<int>();
            var batchId = Guid.NewGuid();
            foreach (var image in sourceImages)
            {
                Guid? variantId = null;
                if (image.LegacyVariantId is not null)
                {
                    if (string.IsNullOrWhiteSpace(image.Barcode) ||
                        !targetVariants.TryGetValue(image.Barcode, out var resolvedVariant)) continue;
                    variantId = resolvedVariant;
                }
                var setId = imageSetMap.TryGetValue(image.LegacySetId, out var mappedSet)
                    ? mappedSet
                    : imageSetMap.Values.First();
                bool variantCover = image.LegacyVariantId is int legacyVariantId &&
                                    firstVariants.Add(legacyVariantId);
                inserted += await PgExecCountAsync(pg, tx, $"""
                    INSERT INTO {CAT}.product_images
                        ("Id","ProductId","VariantId","ImageSetId","FileName","SortOrder",
                         "IsProductCover","IsVariantCover","Status","BatchId","CreatedAt","IsDeleted")
                    SELECT @id,@productId,@variantId::uuid,@setId,@fileName,@sortOrder,
                           false,@variantCover,'Active',@batchId,now(),false
                     WHERE NOT EXISTS (
                         SELECT 1 FROM {CAT}.product_images existing
                          WHERE existing."ProductId"=@productId
                            AND existing."VariantId" IS NOT DISTINCT FROM @variantId::uuid
                            AND existing."FileName"=@fileName
                            AND existing."IsDeleted"=false)
                    """, ct,
                    ("id", Guid.NewGuid()), ("productId", image.ProductId),
                    ("variantId", (object?)variantId ?? DBNull.Value), ("setId", setId),
                    ("fileName", image.FileName), ("sortOrder", image.SortOrder),
                    ("variantCover", variantCover), ("batchId", batchId));
            }
            await tx.CommitAsync(ct);
            log.AppendLine($"[GÖRSEL-HEDEFLİ] eklenen={inserted}.");
            return new(true, false, "images-missing", inserted, log.ToString(), null, DurMs(t0));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Legacy hedefli eksik görsel senkronu hatası");
            return new(false, dryRun, "images-missing", 0, log.ToString(), ex.Message, DurMs(t0));
        }
    }

    private sealed record MissingImageProduct(Guid ProductId, string Code, DateTime CreatedAt);
    private sealed record TargetedSourceImage(
        Guid ProductId, int? LegacyVariantId, string? Barcode, int LegacySetId,
        string FileName, int SortOrder);

    private static async Task<List<MissingImageProduct>> ReadMissingImageProductsAsync(
        NpgsqlConnection pg, (DateTime CreatedAt, string Code)? cursor, int batchSize, CancellationToken ct)
    {
        var cursorFilter = cursor.HasValue
            ? "AND (p.\"CreatedAt\"<@cursorCreated OR (p.\"CreatedAt\"=@cursorCreated AND p.\"Code\"<@cursorCode))"
            : string.Empty;
        await using var command = new NpgsqlCommand($"""
            SELECT p."Id",p."Code",p."CreatedAt"
              FROM {CAT}.products p
             WHERE p."IsDeleted"=false
               {cursorFilter}
               AND NOT EXISTS (
                   SELECT 1 FROM {CAT}.product_images i
                    WHERE i."ProductId"=p."Id" AND i."IsDeleted"=false AND i."Status"='Active')
             ORDER BY p."CreatedAt" DESC,p."Code" DESC
             LIMIT @limit
            """, pg);
        if (cursor.HasValue)
        {
            command.Parameters.AddWithValue("cursorCreated", cursor.Value.CreatedAt);
            command.Parameters.AddWithValue("cursorCode", cursor.Value.Code);
        }
        command.Parameters.Add("limit", NpgsqlTypes.NpgsqlDbType.Integer).Value = batchSize;
        var result = new List<MissingImageProduct>(batchSize);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetDateTime(2)));
        return result;
    }

    private static async Task<Dictionary<int, MissingImageProduct>> LoadLegacyProductsByCodesAsync(
        MySqlConnection my, IReadOnlyList<MissingImageProduct> products, CancellationToken ct)
    {
        var parameterNames = products.Select((_, i) => $"@code{i}").ToArray();
        await using var command = new MySqlCommand(
            $"SELECT Id,urunKodu FROM apurunler WHERE urunKodu IN ({string.Join(',', parameterNames)})", my)
            { CommandTimeout = 120 };
        for (int i = 0; i < products.Count; i++)
            command.Parameters.AddWithValue(parameterNames[i], products[i].Code);
        var byCode = products.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<int, MissingImageProduct>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var code = reader.GetString(1);
            if (byCode.TryGetValue(code, out var product)) result[reader.GetInt32(0)] = product;
        }
        return result;
    }

    private static async Task<Dictionary<(int ProductId, int? VariantId), int>> LoadChosenImageSetsAsync(
        MySqlConnection my, int[] productIds, int preferredSet, CancellationToken ct)
    {
        var parameters = productIds.Select((_, i) => $"@id{i}").ToArray();
        await using var command = new MySqlCommand($"""
            SELECT urunId,urunAnaVaryantId,IFNULL(resimSetId,1),COUNT(*),
                   DATE_FORMAT(MAX(IFNULL(olusturmaTarihi,'1900-01-01')),'%Y-%m-%d %H:%i:%s')
              FROM apurunresimleri
             WHERE isSilindi=0 AND resimDosyaAdi IS NOT NULL AND resimDosyaAdi<>''
               AND urunId IN ({string.Join(',', parameters)})
             GROUP BY urunId,urunAnaVaryantId,IFNULL(resimSetId,1)
            """, my) { CommandTimeout = 120 };
        for (int i = 0; i < productIds.Length; i++) command.Parameters.AddWithValue(parameters[i], productIds[i]);
        var candidates = new Dictionary<(int, int?), (int setId, int cnt, DateTime maxT)>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var key = (reader.GetInt32(0), reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1));
            var maxT = !reader.IsDBNull(4) && DateTime.TryParse(reader.GetString(4), out var parsed)
                ? parsed : DateTime.MinValue;
            SetSecimBirlestir(candidates, key, reader.GetInt32(2),
                Convert.ToInt32(reader.GetValue(3)), maxT, preferredSet);
        }
        return candidates.ToDictionary(x => (x.Key.Item1, x.Key.Item2), x => x.Value.setId);
    }

    private static async Task<List<TargetedSourceImage>> LoadTargetedSourceImagesAsync(
        MySqlConnection my, int[] productIds,
        IReadOnlyDictionary<int, MissingImageProduct> products,
        IReadOnlyDictionary<(int ProductId, int? VariantId), int> chosenSets,
        CancellationToken ct)
    {
        var parameters = productIds.Select((_, i) => $"@id{i}").ToArray();
        await using var command = new MySqlCommand($"""
            SELECT r.resimSetId,r.urunId,r.urunAnaVaryantId,r.resimDosyaAdi,r.siraNo,v.barkod
              FROM apurunresimleri r
              LEFT JOIN apurunvaryantlari v ON v.Id=r.urunAnaVaryantId
             WHERE r.isSilindi=0 AND r.resimDosyaAdi IS NOT NULL AND r.resimDosyaAdi<>''
               AND r.urunId IN ({string.Join(',', parameters)})
             ORDER BY r.urunId,r.urunAnaVaryantId,r.siraNo
            """, my) { CommandTimeout = 120 };
        for (int i = 0; i < productIds.Length; i++) command.Parameters.AddWithValue(parameters[i], productIds[i]);
        var seen = new HashSet<(Guid ProductId, int? VariantId, string FileName)>();
        var result = new List<TargetedSourceImage>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            int legacySetId = reader.IsDBNull(0) ? 1 : reader.GetInt32(0);
            int productId = reader.GetInt32(1);
            int? variantId = reader.IsDBNull(2) ? null : reader.GetInt32(2);
            if (!products.TryGetValue(productId, out var product) ||
                !chosenSets.TryGetValue((productId, variantId), out var chosenSet) ||
                chosenSet != legacySetId) continue;
            string fileName = reader.GetString(3);
            if (!seen.Add((product.ProductId, variantId, fileName))) continue;
            result.Add(new(product.ProductId, variantId,
                reader.IsDBNull(5) ? null : reader.GetString(5), legacySetId,
                fileName, reader.IsDBNull(4) ? 0 : reader.GetInt32(4)));
        }
        return result;
    }

    private static async Task<Dictionary<string, Guid>> LoadTargetVariantsAsync(
        NpgsqlConnection pg, Guid[] productIds, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT "Barcode","Id" FROM {CAT}.product_variants
             WHERE "IsDeleted"=false AND "ProductId"=ANY(@productIds)
               AND "Barcode" IS NOT NULL AND "Barcode"<>''
            """, pg);
        command.Parameters.AddWithValue("productIds", productIds);
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result[reader.GetString(0)] = reader.GetGuid(1);
        return result;
    }

    // ─── DİLİM 3 (B1): YENİ ÜRÜN KARTLARI ────────────────────────────────────
    // Keep-listesi (yeniurunkodlari, 2026-07-09'da donduruldu) SONRASI eski sistemde açılan
    // ürünler (örn. P-00022775) hiçbir migration fazına girmiyordu. Bu dilim, kesim tarihinden
    // sonra oluşturulmuş ve yeni katalogda Code ile bulunmayan ürünleri Faz 5/6/7 kurallarıyla
    // ekler: ürün + varyantlar (+özellik değerleri) + görseller + mishar kanal satırları.
    // Stok, hemen ardından koşulan fiyat/stok dilimiyle gelir (yeni kombinasyon INSERT'i).
    public async Task<Report> SyncNewProductsAsync(CancellationToken ct)
    {
        var t0 = DateTime.UtcNow;
        var log = new StringBuilder();
        bool dry = DryRun;
        try
        {
            await using var pg = await dataSource.OpenConnectionAsync(ct);
            await using var my = new MySqlConnection(MySqlConn);
            await my.OpenAsync(ct);

            var codeToProduct = await PgMapAsync(pg, $"SELECT \"Code\", \"Id\" FROM {CAT}.products WHERE \"IsDeleted\"=false", ct);

            // Adaylar: kesim sonrası oluşturulmuş + katalogda olmayan
            var adaylar = new List<(int oldId, string kod, string ad, int markaId, int grupId,
                decimal alis, decimal satis, int kdv, string? tedKod, bool netAcik, bool satAcik, DateTime? created)>();
            await using (var r = await MyQueryAsync(my, @"SELECT Id, urunKodu, urunAdi, markaId, urunGrupId,
                alisFiyati, satisFiyati, kdvOrani, tedarikciUrunKodu, interneteAcik, satisaAcik, olusturmaTarihi
                FROM apurunler WHERE urunKodu IS NOT NULL AND urunKodu<>'' AND olusturmaTarihi >= @cutoff ORDER BY Id",
                ct, ("@cutoff", ProductCreatedAfter)))
                while (await r.ReadAsync(ct))
                {
                    string kod = r.GetString(1);
                    if (codeToProduct.ContainsKey(kod)) continue;
                    adaylar.Add((r.GetInt32(0), kod,
                        r.IsDBNull(2) ? kod : r.GetString(2),
                        r.IsDBNull(3) ? 0 : r.GetInt32(3),
                        r.IsDBNull(4) ? 0 : r.GetInt32(4),
                        r.IsDBNull(5) ? 0 : (decimal)r.GetDouble(5),
                        r.IsDBNull(6) ? 0 : (decimal)r.GetDouble(6),
                        r.IsDBNull(7) ? 20 : r.GetInt32(7),
                        r.IsDBNull(8) ? null : r.GetString(8),
                        !r.IsDBNull(9) && r.GetValue(9).ToString() == "1",
                        !r.IsDBNull(10) && r.GetValue(10).ToString() == "1",
                        r.IsDBNull(11) ? null : r.GetDateTime(11)));
                }

            if (adaylar.Count == 0)
                return new(true, dry, "products", 0, "[YENİ ÜRÜN] aday yok.", null, DurMs(t0));

            // Eşlemeler
            var (attrTypeMap, markaTypeId) = await LoadAttrTypeMapAsync(pg, my, ct);
            var attrValueMap = await LoadAttrValueMapAsync(pg, my, attrTypeMap, ct);
            var productGroupMap = await LoadProductGroupMapAsync(pg, ct);
            var brandValueMap = await LoadBrandValueMapAsync(pg, my, markaTypeId, ct);
            var imageSetMap = await LoadImageSetMapAsync(pg, my, ct);
            var barcodeToVariant = await PgMapAsync(pg, $"SELECT \"Barcode\", \"Id\" FROM {CAT}.product_variants WHERE \"IsDeleted\"=false AND \"Barcode\" IS NOT NULL AND \"Barcode\"<>''", ct);
            var fp = await PgScalarAsync<Guid>(pg, "SELECT \"Id\" FROM core.core_firm_platforms WHERE \"Code\"='mishar'", ct);

            // Grubu eşlenemeyenler atlanır (B-09: sessiz yanlış-grup fallback YASAK) — raporlanır
            var uygulanacak = adaylar.Where(a => productGroupMap.ContainsKey(a.grupId)).ToList();
            var grupsuz = adaylar.Where(a => !productGroupMap.ContainsKey(a.grupId)).ToList();
            log.AppendLine($"[YENİ ÜRÜN] aday {adaylar.Count}: eklenecek {uygulanacak.Count}, grubu eşlenemeyen {grupsuz.Count}.");
            foreach (var a in uygulanacak) log.AppendLine($"  + {a.kod} — {a.ad}");
            foreach (var a in grupsuz) log.AppendLine($"  ! ATLANDI (grup {a.grupId} eşlenemedi): {a.kod} — {a.ad} → docs/grup_eslesme.md'ye satır ekleyin");

            if (dry || uygulanacak.Count == 0)
                return new(true, dry, "products", uygulanacak.Count, log.ToString(),
                    grupsuz.Count > 0 ? $"{grupsuz.Count} ürün grup eşlemesi bekliyor." : null, DurMs(t0));

            var urunIdList = string.Join(",", uygulanacak.Select(a => a.oldId));

            await using var tx = await pg.BeginTransactionAsync(ct);
            try
            {
                var productMap = new Dictionary<int, Guid>();   // eski urunId → yeni ProductId
                var variantMap = new Dictionary<int, Guid>();   // eski varyantId → yeni VariantId

                // 1) Ürünler (+marka özelliği) — Faz 5 kolonları
                foreach (var a in uygulanacak)
                {
                    var pid = Guid.NewGuid();
                    productMap[a.oldId] = pid;
                    // Ürün düzeyinde IsActive kolonu YOK (satış görünürlüğü M1'de kaldırıldı;
                    // MigrationTool Faz 5 listesi bayat) — ürünün açıklığı IsSaleOpen +
                    // kanal satırı IsActive'iyle yönetilir.
                    bool isActive = a.netAcik && a.satAcik;
                    await PgExecAsync(pg, $@"INSERT INTO {CAT}.products
                        (""Id"",""ProductGroupId"",""Code"",""NameI18n"",""BasePrice"",""BaseCost"",""TaxRate"",""IsSaleOpen"",
                         ""SupplierProductCode"",""Slug"",""Tags"",""CreatedAt"",""IsDeleted"")
                        VALUES (@id,@gid,@code,@name::jsonb,@price,@cost,@tax,@act,@ted,NULL,'[]'::jsonb,@created,false)", ct, tx,
                        ("id", pid), ("gid", productGroupMap[a.grupId]), ("code", a.kod), ("name", I18n(a.ad)),
                        ("price", a.satis), ("cost", a.alis == 0m ? DBNull.Value : a.alis), ("tax", a.kdv), ("act", isActive),
                        ("ted", (object?)a.tedKod ?? DBNull.Value),
                        ("created", a.created.HasValue ? DateTime.SpecifyKind(a.created.Value, DateTimeKind.Utc) : DateTime.UtcNow));
                    if (a.markaId > 0 && brandValueMap.TryGetValue(a.markaId, out var brandVal))
                        await PgExecAsync(pg, $@"INSERT INTO {CAT}.product_attributes (""Id"",""ProductId"",""AttributeTypeId"",""AttributeValueId"",""CreatedAt"",""IsDeleted"")
                            VALUES (@id,@pid,@tid,@vid,now(),false)", ct, tx, ("id", Guid.NewGuid()), ("pid", pid), ("tid", markaTypeId), ("vid", brandVal));
                }

                // 2) Varyantlar + özellik değerleri (eksik attribute_value oluşturulur) — Faz 6 kuralları
                int varyantSayisi = 0;
                var satirlar = new List<(int oldId, int urunId, string? barkod, (int tip, string val)[] attrs, DateTime? created)>();
                await using (var r = await MyQueryAsync(my, $@"SELECT Id, urunId, barkod, varyant1TipId, varyant1Degeri,
                    varyant2TipId, varyant2Degeri, varyant3TipId, varyant3Degeri, olusturmaTarihi
                    FROM apurunvaryantlari WHERE urunId IN ({urunIdList}) ORDER BY urunId, Id", ct))
                    while (await r.ReadAsync(ct))
                    {
                        var attrs = new List<(int, string)>();
                        for (int ax = 0; ax < 3; ax++)
                        {
                            int tipId = r.IsDBNull(3 + ax * 2) ? 0 : r.GetInt32(3 + ax * 2);
                            string val = r.IsDBNull(4 + ax * 2) ? "" : r.GetString(4 + ax * 2);
                            if (tipId != 0 && !string.IsNullOrWhiteSpace(val)) attrs.Add((tipId, val));
                        }
                        satirlar.Add((r.GetInt32(0), r.GetInt32(1), r.IsDBNull(2) ? null : r.GetString(2),
                            attrs.ToArray(), r.IsDBNull(9) ? null : r.GetDateTime(9)));
                    }

                foreach (var v in satirlar)
                {
                    if (!productMap.TryGetValue(v.urunId, out var pid)) continue;
                    // Barkod başka üründe zaten varsa (çakışma) varyantı atla — rapora yaz
                    if (!string.IsNullOrWhiteSpace(v.barkod) && barcodeToVariant.ContainsKey(v.barkod))
                    { log.AppendLine($"  ! varyant atlandı (barkod zaten katalogda): {v.barkod}"); continue; }

                    var vid = Guid.NewGuid();
                    variantMap[v.oldId] = vid;
                    string sku = !string.IsNullOrWhiteSpace(v.barkod) ? v.barkod : $"{v.urunId}-{v.oldId}";
                    await PgExecAsync(pg, $@"INSERT INTO {CAT}.product_variants
                        (""Id"",""ProductId"",""Sku"",""Barcode"",""BasePrice"",""IsActive"",""CreatedAt"",""IsDeleted"")
                        VALUES (@id,@pid,@sku,@bc,0,true,@created,false)", ct, tx,
                        ("id", vid), ("pid", pid), ("sku", sku), ("bc", (object?)v.barkod ?? DBNull.Value),
                        ("created", v.created.HasValue ? DateTime.SpecifyKind(v.created.Value, DateTimeKind.Utc) : DateTime.UtcNow));
                    if (!string.IsNullOrWhiteSpace(v.barkod)) barcodeToVariant[v.barkod] = vid;
                    varyantSayisi++;

                    foreach (var (tipId, val) in v.attrs)
                    {
                        if (!attrTypeMap.TryGetValue(tipId, out var typeGuid)) continue;
                        if (!attrValueMap.TryGetValue((tipId, val), out var valGuid))
                        {
                            valGuid = Guid.NewGuid();
                            await PgExecAsync(pg, $@"INSERT INTO {DEF}.attribute_values
                                (""Id"",""AttributeTypeId"",""NameI18n"",""IsActive"",""SortOrder"",""CreatedAt"",""IsDeleted"")
                                VALUES (@id,@tid,@name::jsonb,true,0,now(),false)", ct, tx,
                                ("id", valGuid), ("tid", typeGuid), ("name", I18n(val)));
                            attrValueMap[(tipId, val)] = valGuid;
                        }
                        await PgExecAsync(pg, $@"INSERT INTO {CAT}.product_variant_attributes
                            (""Id"",""VariantId"",""AttributeTypeId"",""AttributeValueId"",""CreatedAt"",""IsDeleted"")
                            VALUES (@id,@vid,@tid,@avid,now(),false)", ct, tx,
                            ("id", Guid.NewGuid()), ("vid", vid), ("tid", typeGuid), ("avid", valGuid));
                    }
                }

                // 3) Görseller (yalnız bu ürünler; set seçimi SyncImagesAsync ile aynı kural:
                // platform seti öncelikli, yoksa en yeni set)
                int gorselSayisi = 0;
                if (imageSetMap.Count > 0)
                {
                    Guid defaultSetId = imageSetMap.Values.First();
                    int preferredSet = await LoadPreferredImageSetAsync(my, ct);
                    var chosenSet = new Dictionary<(int, int?), (int setId, int cnt, DateTime maxT)>();
                    await using (var rs = await MyQueryAsync(my, $@"SELECT urunId, urunAnaVaryantId, IFNULL(resimSetId,1), COUNT(*),
                            DATE_FORMAT(MAX(IFNULL(olusturmaTarihi,'1900-01-01')),'%Y-%m-%d %H:%i:%s')
                        FROM apurunresimleri WHERE isSilindi=0 AND resimDosyaAdi IS NOT NULL AND resimDosyaAdi<>'' AND urunId IN ({urunIdList})
                        GROUP BY urunId, urunAnaVaryantId, IFNULL(resimSetId,1)", ct))
                        while (await rs.ReadAsync(ct))
                        {
                            var key = (rs.GetInt32(0), rs.IsDBNull(1) ? (int?)null : rs.GetInt32(1));
                            int setId = rs.GetInt32(2), c = Convert.ToInt32(rs.GetValue(3));
                            var maxT = !rs.IsDBNull(4) && DateTime.TryParse(rs.GetString(4), out var d) ? d : DateTime.MinValue;
                            SetSecimBirlestir(chosenSet, key, setId, c, maxT, preferredSet);
                        }

                    var seen = new HashSet<(Guid, Guid?, string)>();
                    var variantFirst = new HashSet<int>();
                    var batchId = Guid.NewGuid();
                    var imgRows = new List<object?[]>();
                    await using (var ri = await MyQueryAsync(my, $@"SELECT resimSetId, urunId, urunAnaVaryantId, resimDosyaAdi, siraNo
                        FROM apurunresimleri WHERE isSilindi=0 AND resimDosyaAdi IS NOT NULL AND resimDosyaAdi<>'' AND urunId IN ({urunIdList})
                        ORDER BY urunId, urunAnaVaryantId, siraNo", ct))
                        while (await ri.ReadAsync(ct))
                        {
                            int oldSetId = ri.IsDBNull(0) ? 1 : ri.GetInt32(0);
                            int urunId = ri.GetInt32(1);
                            int? variantOldId = ri.IsDBNull(2) ? null : ri.GetInt32(2);
                            string fileName = ri.GetString(3);
                            int siraNo = ri.IsDBNull(4) ? 0 : ri.GetInt32(4);

                            if (!productMap.TryGetValue(urunId, out var pid)) continue;
                            if (chosenSet.TryGetValue((urunId, variantOldId), out var cs) && cs.setId != oldSetId) continue;
                            Guid? vgd = variantOldId.HasValue && variantMap.TryGetValue(variantOldId.Value, out var vg) ? vg : null;
                            var setId = imageSetMap.TryGetValue(oldSetId, out var sid) ? sid : defaultSetId;
                            if (!seen.Add((pid, vgd, fileName))) continue;
                            bool isVariantCover = variantOldId.HasValue && variantFirst.Add(variantOldId.Value);
                            imgRows.Add(new object?[] { Guid.NewGuid(), pid, vgd, setId, fileName, siraNo, false, isVariantCover, "Active", batchId, DateTime.UtcNow, false });
                            gorselSayisi++;
                        }
                    if (imgRows.Count > 0)
                        await PgBatchInsertAsync(pg, $"{CAT}.product_images",
                            new[] { "Id", "ProductId", "VariantId", "ImageSetId", "FileName", "SortOrder", "IsProductCover", "IsVariantCover", "Status", "BatchId", "CreatedAt", "IsDeleted" },
                            new string?[12], imgRows, ct, tx);
                }

                // 4) Mishar kanal satırları (plurunler platform 41 → upsert; Faz 14 kuralları)
                int kanalVaryant = 0;
                var seenProducts = new HashSet<Guid>();
                var kanalSatirlar = new List<(Guid pid, Guid vid, decimal? price, decimal? cmp, bool satista)>();
                await using (var rc = await MyQueryAsync(my, $@"SELECT urunId, urunAnaVaryantId, satisFiyati, listeFiyati, satista
                    FROM plurunler WHERE platformId={LegacyPlatformId} AND urunId IN ({urunIdList})", ct))
                    while (await rc.ReadAsync(ct))
                    {
                        int urunId = rc.GetInt32(0), lvid = rc.GetInt32(1);
                        if (!productMap.TryGetValue(urunId, out var pid) || !variantMap.TryGetValue(lvid, out var vid)) continue;
                        decimal satis = rc.IsDBNull(2) ? 0 : (decimal)rc.GetDouble(2);
                        decimal liste = rc.IsDBNull(3) ? 0 : (decimal)rc.GetDouble(3);
                        bool satista = !rc.IsDBNull(4) && Convert.ToBoolean(rc.GetValue(4));
                        kanalSatirlar.Add((pid, vid, satis > 0 ? satis : null, liste > 0 && liste != satis ? liste : null, satista));
                    }
                foreach (var k in kanalSatirlar)
                {
                    seenProducts.Add(k.pid);
                    await PgExecAsync(pg, @"INSERT INTO storefront.channel_variants
                        (""Id"",""FirmPlatformId"",""VariantId"",""PriceType"",""Price"",""CompareAtPrice"",""IsActive"",""CreatedAt"",""IsDeleted"")
                        VALUES (@id,@fp,@vid,@pt,@price,@cmp,@act,now(),false)
                        ON CONFLICT (""FirmPlatformId"",""VariantId"") DO UPDATE SET
                        ""PriceType""=EXCLUDED.""PriceType"", ""Price""=EXCLUDED.""Price"",
                        ""CompareAtPrice""=EXCLUDED.""CompareAtPrice"", ""IsActive""=EXCLUDED.""IsActive"", ""UpdatedAt""=now()", ct, tx,
                        ("id", Guid.NewGuid()), ("fp", fp), ("vid", k.vid),
                        ("pt", k.price.HasValue ? "manual" : DBNull.Value), ("price", (object?)k.price ?? DBNull.Value),
                        ("cmp", (object?)k.cmp ?? DBNull.Value), ("act", k.satista));
                    kanalVaryant++;
                }
                foreach (var pid in seenProducts)
                    await PgExecAsync(pg, @"INSERT INTO storefront.channel_products
                        (""Id"",""FirmPlatformId"",""ProductId"",""IsActive"",""SortOrder"",""CreatedAt"",""IsDeleted"")
                        VALUES (@id,@fp,@pid,true,0,now(),false)
                        ON CONFLICT (""FirmPlatformId"",""ProductId"") DO UPDATE SET ""IsActive""=true, ""UpdatedAt""=now()", ct, tx,
                        ("id", Guid.NewGuid()), ("fp", fp), ("pid", pid));

                await tx.CommitAsync(ct);
                log.AppendLine($"  ✓ {productMap.Count} ürün + {varyantSayisi} varyant + {gorselSayisi} görsel + {kanalVaryant} kanal varyantı ({seenProducts.Count} kanal ürünü) eklendi.");
            }
            catch { await tx.RollbackAsync(ct); throw; }

            await PgExecAsync(pg, $"ANALYZE {CAT}.products", ct);
            await PgExecAsync(pg, $"ANALYZE {CAT}.product_variants", ct);
            return new(true, false, "products", uygulanacak.Count, log.ToString(),
                grupsuz.Count > 0 ? $"{grupsuz.Count} ürün grup eşlemesi bekliyor." : null, DurMs(t0));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Legacy senkron (yeni ürün) hatası");
            return new(false, dry, "products", 0, log.ToString(), ex.Message, DurMs(t0));
        }
    }

    // ─── EŞLEME YÜKLEYİCİLERİ (MigrationTool Ensure* portları) ───────────────
    private static async Task<(Dictionary<int, Guid> map, Guid markaTypeId)> LoadAttrTypeMapAsync(
        NpgsqlConnection pg, MySqlConnection my, CancellationToken ct)
    {
        var mysqlNames = await MyIntStringMapAsync(my, "SELECT Id, aciklama FROM dfvaryanttipleri", ct);
        var pgCodes = new Dictionary<string, Guid>();
        await using (var cmd = new NpgsqlCommand($"SELECT \"Id\", \"Code\" FROM {DEF}.attribute_types", pg))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) pgCodes[r.GetString(1)] = r.GetGuid(0);
        Guid marka = pgCodes.GetValueOrDefault("marka");
        var map = new Dictionary<int, Guid>();
        foreach (var (id, name) in mysqlNames)
            if (pgCodes.TryGetValue(Slugify(name), out var g)) map[id] = g;
        return (map, marka);
    }

    private static async Task<Dictionary<(int, string), Guid>> LoadAttrValueMapAsync(
        NpgsqlConnection pg, MySqlConnection my, Dictionary<int, Guid> attrTypeMap, CancellationToken ct)
    {
        var mysqlVals = new List<(int, string)>();
        await using (var r = await MyQueryAsync(my, "SELECT varyantTipId, varyantDegeri FROM dfvaryanttipdegerleri", ct))
            while (await r.ReadAsync(ct)) { if (!r.IsDBNull(1)) mysqlVals.Add((r.GetInt32(0), r.GetString(1))); }

        var pgVals = new Dictionary<(Guid, string), Guid>();
        await using (var cmd = new NpgsqlCommand($"SELECT \"Id\", \"AttributeTypeId\", \"NameI18n\"->>'tr' FROM {DEF}.attribute_values", pg))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) { if (!r.IsDBNull(2)) pgVals[(r.GetGuid(1), r.GetString(2))] = r.GetGuid(0); }

        var map = new Dictionary<(int, string), Guid>();
        foreach (var (tipId, val) in mysqlVals)
            if (attrTypeMap.TryGetValue(tipId, out var tg) && pgVals.TryGetValue((tg, val), out var vid))
                map[(tipId, val)] = vid;
        return map;
    }

    private async Task<Dictionary<int, Guid>> LoadProductGroupMapAsync(NpgsqlConnection pg, CancellationToken ct)
    {
        var map = new Dictionary<int, Guid>();
        var codeToId = new Dictionary<string, Guid>();
        await using (var cmd = new NpgsqlCommand($"SELECT \"Id\", \"Code\" FROM {DEF}.product_groups", pg))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
            {
                string code = r.GetString(1);
                codeToId[code] = r.GetGuid(0);
                if (code.StartsWith("grp_") && int.TryParse(code[4..], out int mid)) map[mid] = r.GetGuid(0);
            }

        // B-09: birleştirilen/silinen eski grupların hedefi docs/grup_eslesme.md'den (MigrationTool ile aynı format)
        string[] adaylar = { "/opt/ECSProsAI/docs/grup_eslesme.md", "docs/grup_eslesme.md" };
        var path = adaylar.FirstOrDefault(File.Exists);
        if (path is null) { logger.LogWarning("grup_eslesme.md bulunamadı — yalnız grp_ kodlu gruplar eşlenir."); return map; }
        var satirRx = new Regex(@"^\|\s*(\d+)\s*\|[^|]*\|[^|]*\|([^|]*)\|");
        var kodRx = new Regex(@"\(([a-z0-9_]+)\)");
        foreach (var line in File.ReadLines(path))
        {
            var m = satirRx.Match(line);
            if (!m.Success) continue;
            int lid = int.Parse(m.Groups[1].Value);
            string hedef = m.Groups[2].Value.Trim();
            var km = kodRx.Match(hedef);
            string? targetCode = km.Success ? km.Groups[1].Value : hedef.StartsWith("—") ? null : $"grp_{lid}";
            if (!map.ContainsKey(lid) && targetCode is not null && codeToId.TryGetValue(targetCode, out var gid))
                map[lid] = gid;
        }
        return map;
    }

    private static async Task<Dictionary<int, Guid>> LoadBrandValueMapAsync(
        NpgsqlConnection pg, MySqlConnection my, Guid markaTypeId, CancellationToken ct)
    {
        var map = new Dictionary<int, Guid>();
        if (markaTypeId == Guid.Empty) return map;
        var brands = await MyIntStringMapAsync(my, "SELECT Id, marka FROM dfmarkalar", ct);
        var pgBrands = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = new NpgsqlCommand($"SELECT \"Id\", \"NameI18n\"->>'tr' FROM {DEF}.attribute_values WHERE \"AttributeTypeId\"=@tid", pg))
        {
            cmd.Parameters.AddWithValue("tid", markaTypeId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) { if (!r.IsDBNull(1)) pgBrands[r.GetString(1)] = r.GetGuid(0); }
        }
        foreach (var (id, name) in brands)
            if (pgBrands.TryGetValue(name, out var g)) map[id] = g;
        return map;
    }

    private static async Task<Dictionary<int, Guid>> LoadImageSetMapAsync(NpgsqlConnection pg, MySqlConnection my, CancellationToken ct)
    {
        var names = await MyIntStringMapAsync(my, "SELECT Id, setAdi FROM dfresimsetleri", ct);
        var pgSets = new Dictionary<string, Guid>();
        await using (var cmd = new NpgsqlCommand($"SELECT \"Id\", \"Name\" FROM {DEF}.image_sets", pg))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) pgSets[r.GetString(1)] = r.GetGuid(0);
        var map = new Dictionary<int, Guid>();
        foreach (var (id, name) in names)
            if (pgSets.TryGetValue(name, out var g)) map[id] = g;
        return map;
    }

    // ─── DB YARDIMCILARI ─────────────────────────────────────────────────────
    // Set seçimi (2026-08-07): eski "en çok resimli set" kuralı, dosyaları fiziken silinmiş
    // 2023 Julude setini (daha kalabalık) seçip 404 üretiyordu. Platformun kendi seti
    // (dfplatforms.resimSetId — eski sitenin gösterdiği set) her zaman kazanır; o set
    // varyantta hiç yoksa en yeni yüklenen set, eşitlikte çok resimli, sonra küçük setId.
    private async Task<int> LoadPreferredImageSetAsync(MySqlConnection my, CancellationToken ct) =>
        await LoadPreferredImageSetAsync(my, LegacyPlatformId, ct);

    private static async Task<int> LoadPreferredImageSetAsync(
        MySqlConnection my, int legacyPlatformId, CancellationToken ct)
    {
        try
        {
            await using var cmd = new MySqlCommand(
                "SELECT resimSetId FROM dfplatforms WHERE Id=@platformId", my);
            cmd.Parameters.AddWithValue("@platformId", legacyPlatformId);
            var v = await cmd.ExecuteScalarAsync(ct);
            return v is null or DBNull ? 1 : Convert.ToInt32(v);
        }
        catch { return 1; }
    }

    private static void SetSecimBirlestir(Dictionary<(int, int?), (int setId, int cnt, DateTime maxT)> map,
        (int, int?) key, int setId, int cnt, DateTime maxT, int preferredSetId)
    {
        if (!map.TryGetValue(key, out var cur)) { map[key] = (setId, cnt, maxT); return; }
        bool curPref = cur.setId == preferredSetId, yeniPref = setId == preferredSetId;
        bool better = yeniPref != curPref ? yeniPref
            : maxT != cur.maxT ? maxT > cur.maxT
            : cnt != cur.cnt ? cnt > cur.cnt
            : setId < cur.setId;
        if (better) map[key] = (setId, cnt, maxT);
    }

    private static async Task<Dictionary<string, Guid>> PgMapAsync(NpgsqlConnection pg, string sql, CancellationToken ct)
    {
        var map = new Dictionary<string, Guid>(StringComparer.Ordinal);
        await using var cmd = new NpgsqlCommand(sql, pg) { CommandTimeout = 300 };
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) map[r.GetString(0)] = r.GetGuid(1);
        return map;
    }

    private static async Task<Dictionary<int, string>> MyIntStringMapAsync(MySqlConnection my, string sql, CancellationToken ct)
    {
        var map = new Dictionary<int, string>();
        await using var r = await MyQueryAsync(my, sql, ct);
        while (await r.ReadAsync(ct)) map[r.GetInt32(0)] = r.GetString(1);
        return map;
    }

    private static async Task<MySqlDataReader> MyQueryAsync(MySqlConnection my, string sql, CancellationToken ct,
        params (string name, object value)[] ps)
    {
        var cmd = new MySqlCommand(sql, my) { CommandTimeout = 300 };
        foreach (var (name, value) in ps) cmd.Parameters.AddWithValue(name, value);
        return (MySqlDataReader)await cmd.ExecuteReaderAsync(ct);
    }

    private static async Task PgExecAsync(NpgsqlConnection pg, string sql, CancellationToken ct)
        => await PgExecAsync(pg, sql, ct, null, 120);

    private static async Task PgExecAsync(NpgsqlConnection pg, string sql, CancellationToken ct,
        NpgsqlTransaction? tx, int timeoutSec = 120)
    {
        await using var cmd = new NpgsqlCommand(sql, pg, tx) { CommandTimeout = timeoutSec };
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task PgExecAsync(NpgsqlConnection pg, string sql, CancellationToken ct,
        NpgsqlTransaction? tx, params (string name, object? value)[] ps)
    {
        await using var cmd = new NpgsqlCommand(sql, pg, tx) { CommandTimeout = 120 };
        foreach (var (name, value) in ps) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<int> PgExecCountAsync(NpgsqlConnection pg, NpgsqlTransaction tx,
        string sql, CancellationToken ct, params (string name, object? value)[] ps)
    {
        await using var cmd = new NpgsqlCommand(sql, pg, tx) { CommandTimeout = 120 };
        foreach (var (name, value) in ps) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<T> PgScalarAsync<T>(NpgsqlConnection pg, string sql, CancellationToken ct,
        NpgsqlTransaction? tx = null)
    {
        await using var cmd = new NpgsqlCommand(sql, pg, tx) { CommandTimeout = 300 };
        return (T)(await cmd.ExecuteScalarAsync(ct))!;
    }

    private static async Task PgBatchInsertAsync(NpgsqlConnection pg, string tableFull, string[] columns,
        string?[] casts, List<object?[]> rows, CancellationToken ct, NpgsqlTransaction? tx = null)
    {
        if (rows.Count == 0) return;
        var sb = new StringBuilder();
        sb.Append("INSERT INTO ").Append(tableFull).Append(" (\"").Append(string.Join("\",\"", columns)).Append("\") VALUES ");
        await using var cmd = new NpgsqlCommand { Connection = pg, Transaction = tx, CommandTimeout = 120 };
        int p = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('(');
            for (int c = 0; c < columns.Length; c++)
            {
                if (c > 0) sb.Append(',');
                string pname = "p" + p++;
                sb.Append('@').Append(pname);
                if (casts[c] != null) sb.Append("::").Append(casts[c]);
                cmd.Parameters.AddWithValue(pname, rows[i][c] ?? DBNull.Value);
            }
            sb.Append(')');
        }
        cmd.CommandText = sb.ToString();
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };
    private static string I18n(string? tr) => JsonSerializer.Serialize(new Dictionary<string, string> { ["tr"] = tr ?? "" }, JsonOpts);
    private static int DurMs(DateTime t0) => (int)(DateTime.UtcNow - t0).TotalMilliseconds;

    private static string Slugify(string s) => s.ToLowerInvariant()
        .Replace(" ", "_").Replace("ı", "i").Replace("ş", "s").Replace("ğ", "g")
        .Replace("ü", "u").Replace("ö", "o").Replace("ç", "c").Replace("â", "a")
        .Replace("î", "i").Replace("û", "u").Replace("/", "_").Replace("-", "_")
        .Replace("(", "").Replace(")", "").Replace(".", "");
}
