using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using Npgsql;

await Migration.RunAsync(args);

static class Migration
{
    const string MYSQL_CONN = "Server=51.178.208.50;Port=3306;Database=juludedb;Uid=web;Pwd={wb9&HqD&_zwg~?;Connection Timeout=30;SslMode=None;CharSet=utf8mb4;";
    const string PG_CONN = "Host=localhost;Port=5432;Database=ecommerce_db;Username=ecommerce;Password=EcsPros2025SecureDb!;";

    // Şemalar: attribute_types/values, image_sets, product_groups → "definition";
    // products, product_variants, product_attributes, product_images → "catalog"
    const string DEF = "definition";
    const string CAT = "catalog";

    static readonly Dictionary<int, Guid> imageSetMap = new();
    static readonly Dictionary<int, Guid> attrTypeMap = new();
    static readonly Dictionary<(int typeId, string value), Guid> attrValueMap = new();
    static readonly Dictionary<int, Guid> productGroupMap = new();
    static readonly Dictionary<int, Guid> productMap = new();
    static readonly Dictionary<int, Guid> variantMap = new();
    static readonly Dictionary<int, Guid> brandValueMap = new();
    static Guid markaTypeId = Guid.Empty;
    static Guid nebimFirmIntegrationId = Guid.Empty;

    static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };
    static string I18n(string? tr) => JsonSerializer.Serialize(new Dictionary<string, string> { ["tr"] = tr ?? "" }, JsonOpts);
    static DateTime Now => DateTime.UtcNow;
    static Guid NewId() => Guid.NewGuid();
    static void Log(string msg) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");

    static MySqlConnection mysql = null!;
    static NpgsqlConnection pg = null!;

    public static async Task RunAsync(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        int phase = args.Length > 0 ? int.Parse(args[0]) : 0;

        mysql = new MySqlConnection(MYSQL_CONN);
        pg = new NpgsqlConnection(PG_CONN);
        mysql.Open();
        pg.Open();

        Log($"Bağlantılar açıldı. Faz: {(phase == 0 ? "Tümü" : phase.ToString())}");

        // Tam çalışmada önce tüm tabloları temizle (FK sırası)
        if (phase == 0) ClearAll();

        if (phase == 0 || phase == 1) await Phase1_ImageSets();
        if (phase == 0 || phase == 2) await Phase2_AttributeTypes();
        if (phase == 0 || phase == 3) await Phase3_AttributeValues();
        if (phase == 0 || phase == 4) await Phase4_ProductGroups();
        if (phase == 0 || phase == 5) await Phase5_Products();
        if (phase == 0 || phase == 6) await Phase6_Variants();
        if (phase == 0 || phase == 7) await Phase7_Images();
        if (phase == 8) await Phase8_FixGroupNames();
        if (phase == 9) await Phase9_MergeGroups();
        if (phase == 10) await Phase10_ProductGender();
        if (phase == 0 || phase == 11) await Phase11_ErpVariantData();
        if (phase == 12) await Phase12_ProductSpecs(args.Length > 1 ? args[1] : null);
        if (phase == 13) await Phase13_ProductAttributeValues(args.Length > 1 ? args[1] : null);
        if (phase == 14) await Phase14_FirmsAndChannelData();
        if (phase == 15) await Phase15_SeedChannelCategories(Guid.Parse(args[1]));
        if (phase == 16) await Phase16_WarehouseStructure();
        if (phase == 17) Phase17_ChannelSaleFlags();
        if (phase == 18) Phase18_ChannelVariantUrls();
        if (phase == 19) Phase19_FiltreRengi();
        if (phase == 20) await RunCatalogReload();
        if (phase == 21) await Phase21_FixProductGroups();
        if (phase == 22) await Phase22_MembersAndAddresses();
        if (phase == 23) await Phase23_Orders();
        if (phase == 24) await Phase24_Favorites();
        if (phase == 25) await Phase25_MisharMenu();
        // Faz 26: eski DB'den fiyat/görsel/stok HEDEFLİ güncelleme (ID koruyan yerinde UPDATE —
        // tam-reload Faz 5/6/7'nin aksine). args[1]=="dry" → yalnız rapor, yazma yok.
        if (phase == 26) await Phase26_TargetedUpdate(args.Length > 1 && args[1] == "dry");
        // Faz 27: KANAL fiyatı (channel_variants: Price/CompareAt/IsActive) plurunler'den
        // tazele — storefront filtresi/gösterilen fiyat bunu kullanır (BasePrice değil).
        if (phase == 27) Phase27_ChannelPriceRefresh(args.Length > 1 && args[1] == "dry");
        // Faz 28: YALNIZ stok tazeleme (Faz 26'nın stok parçası; görsel/fiyat'a dokunmaz).
        if (phase == 28) Phase28_StockOnly(args.Length > 1 && args[1] == "dry");
        // Faz 29: ürün videosu + yorum/puan aktarımı (mishar). args[1]=="dry" → yalnız rapor.
        if (phase == 29) await Phase29_VideosAndReviews(args.Length > 1 && args[1] == "dry");

        if (phase is 26 or 27 or 28 or 29) { Log($"=== Faz {phase} bitti ==="); return; }
        Log("=== Migration tamamlandı! ===");
        Log($"  image_sets                  : {PgCount($"{DEF}.image_sets")}");
        Log($"  attribute_types              : {PgCount($"{DEF}.attribute_types")}");
        Log($"  attribute_values             : {PgCount($"{DEF}.attribute_values")}");
        Log($"  product_groups               : {PgCount($"{DEF}.product_groups")}");
        Log($"  products                     : {PgCount($"{CAT}.products")}");
        Log($"  product_attributes           : {PgCount($"{CAT}.product_attributes")}");
        Log($"  product_variants             : {PgCount($"{CAT}.product_variants")}");
        Log($"  product_variant_attributes   : {PgCount($"{CAT}.product_variant_attributes")}");
        Log($"  product_images               : {PgCount($"{CAT}.product_images")}");
        Log($"  erp_variant_data             : {PgCount("integration.erp_variant_data")}");
        Log($"  core_firms                   : {PgCount("core.core_firms")}");
        Log($"  core_firm_platforms          : {PgCount("core.core_firm_platforms")}");
        Log($"  channel_products             : {PgCount("storefront.channel_products")}");
        Log($"  channel_variants             : {PgCount("storefront.channel_variants")}");

        mysql.Close();
        pg.Close();
    }

    // ─── CLEAR ALL (FK sırası) ───────────────────────────────────────────────
    static void ClearAll()
    {
        Log("Tüm tablolar temizleniyor...");
        PgExec($"DELETE FROM {CAT}.product_images WHERE TRUE");
        ClearAttributeTables();
        PgExec($"DELETE FROM {CAT}.product_variants WHERE TRUE");
        PgExec($"DELETE FROM {CAT}.products WHERE TRUE");
        PgExec($"DELETE FROM {DEF}.product_groups WHERE TRUE");
        PgExec($"DELETE FROM {DEF}.image_sets WHERE TRUE");
        Log("  ✓ Temizlendi.");
    }

    static void ClearAttributeTables()
    {
        // Attribute'a bağlı tüm bağımlı tablolar — doğru FK sırası
        PgExec($"DELETE FROM {CAT}.product_variant_attributes WHERE TRUE");
        PgExec($"DELETE FROM {CAT}.product_attributes WHERE TRUE");
        PgExec($"DELETE FROM {CAT}.product_axis_sub_attribute_values WHERE TRUE");
        PgExec($"DELETE FROM {DEF}.product_group_axis_sub_attributes WHERE TRUE");
        PgExec($"DELETE FROM {DEF}.product_group_attributes WHERE TRUE");
        PgExec($"DELETE FROM {DEF}.attribute_values WHERE TRUE");
        PgExec($"DELETE FROM {DEF}.attribute_types WHERE TRUE");
    }

    // ─── FAZ 1: IMAGE SETS ───────────────────────────────────────────────────
    static Task Phase1_ImageSets()
    {
        Log("FAZ 1: ImageSets...");
        // Tek faz çalışıyorsa önce image_sets'e bağlı product_images'ı temizle
        PgExec($"DELETE FROM {CAT}.product_images WHERE TRUE");
        PgExec($"DELETE FROM {DEF}.image_sets WHERE TRUE");

        using var r = MysqlQuery("SELECT Id, setAdi FROM dfresimsetleri");
        int count = 0;
        while (r.Read())
        {
            int oldId = r.GetInt32(0);
            string name = r.GetString(1);
            string code = Slugify(name);
            var newId = NewId();
            imageSetMap[oldId] = newId;

            PgExec($@"INSERT INTO {DEF}.image_sets
                (""Id"", ""Code"", ""Name"", ""IsDefault"", ""FallbackSetId"", ""SortPriority"", ""IsActive"", ""CreatedAt"", ""IsDeleted"")
                VALUES (@id, @code, @name, @def, NULL, @sort, TRUE, @now, FALSE)",
                ("id", newId), ("code", code), ("name", name),
                ("def", oldId == 1), ("sort", oldId), ("now", Now));
            count++;
        }
        Log($"  ✓ {count} ImageSet");
        return Task.CompletedTask;
    }

    // ─── FAZ 2: ATTRIBUTE TYPES ──────────────────────────────────────────────
    static Task Phase2_AttributeTypes()
    {
        Log("FAZ 2: AttributeTypes...");
        ClearAttributeTables();

        using var r = MysqlQuery("SELECT Id, aciklama FROM dfvaryanttipleri ORDER BY Id");
        int count = 0;
        while (r.Read())
        {
            int oldId = r.GetInt32(0);
            string name = r.GetString(1);
            string code = Slugify(name);
            var newId = NewId();
            attrTypeMap[oldId] = newId;

            PgExec($@"INSERT INTO {DEF}.attribute_types
                (""Id"", ""Code"", ""NameI18n"", ""DataType"", ""IsActive"", ""SortOrder"", ""CreatedAt"", ""IsDeleted"")
                VALUES (@id, @code, @name::jsonb, 'select', TRUE, @sort, @now, FALSE)",
                ("id", newId), ("code", code), ("name", I18n(name)), ("sort", oldId), ("now", Now));
            count++;
        }

        // Marka
        markaTypeId = NewId();
        PgExec($@"INSERT INTO {DEF}.attribute_types
            (""Id"", ""Code"", ""NameI18n"", ""DataType"", ""IsActive"", ""SortOrder"", ""CreatedAt"", ""IsDeleted"")
            VALUES (@id, 'marka', @name::jsonb, 'select', TRUE, 0, @now, FALSE)",
            ("id", markaTypeId), ("name", I18n("Marka")), ("now", Now));
        count++;

        Log($"  ✓ {count} AttributeType");
        return Task.CompletedTask;
    }

    // ─── FAZ 3: ATTRIBUTE VALUES ─────────────────────────────────────────────
    static Task Phase3_AttributeValues()
    {
        Log("FAZ 3: AttributeValues...");
        EnsureAttrTypeMaps();
        PgExec($"DELETE FROM {DEF}.attribute_values WHERE TRUE");

        // MySQL'den önce listeye al (tek bağlantıda iki reader olmaz)
        var globalVals = new List<(int tipId, string val, int sira)>();
        using (var r = MysqlQuery("SELECT varyantTipId, varyantDegeri, siraNo FROM dfvaryanttipdegerleri ORDER BY varyantTipId, siraNo"))
            while (r.Read())
                globalVals.Add((r.GetInt32(0), r.IsDBNull(1) ? "" : r.GetString(1), r.IsDBNull(2) ? 0 : r.GetInt32(2)));

        var brands = new List<(int id, string name)>();
        using (var r2 = MysqlQuery("SELECT Id, marka FROM dfmarkalar ORDER BY Id"))
            while (r2.Read())
                brands.Add((r2.GetInt32(0), r2.GetString(1)));

        int count = 0;
        foreach (var (tipId, valueName, siraNo) in globalVals)
        {
            if (!attrTypeMap.TryGetValue(tipId, out var typeGuid)) continue;
            if (string.IsNullOrWhiteSpace(valueName)) continue;
            if (attrValueMap.ContainsKey((tipId, valueName))) continue;

            var newId = NewId();
            attrValueMap[(tipId, valueName)] = newId;
            PgExec($@"INSERT INTO {DEF}.attribute_values
                (""Id"", ""AttributeTypeId"", ""NameI18n"", ""IsActive"", ""SortOrder"", ""CreatedAt"", ""IsDeleted"")
                VALUES (@id, @tid, @name::jsonb, TRUE, @sort, @now, FALSE)",
                ("id", newId), ("tid", typeGuid), ("name", I18n(valueName)), ("sort", siraNo), ("now", Now));
            count++;
        }

        foreach (var (oldId, name) in brands)
        {
            var newId = NewId();
            brandValueMap[oldId] = newId;
            PgExec($@"INSERT INTO {DEF}.attribute_values
                (""Id"", ""AttributeTypeId"", ""NameI18n"", ""IsActive"", ""SortOrder"", ""CreatedAt"", ""IsDeleted"")
                VALUES (@id, @tid, @name::jsonb, TRUE, @sort, @now, FALSE)",
                ("id", newId), ("tid", markaTypeId), ("name", I18n(name)), ("sort", oldId), ("now", Now));
            count++;
        }

        Log($"  ✓ {count} AttributeValue");
        return Task.CompletedTask;
    }

    // ─── FAZ 4: PRODUCT GROUPS ───────────────────────────────────────────────
    static Task Phase4_ProductGroups()
    {
        Log("FAZ 4: ProductGroups...");
        // products → product_groups FK
        PgExec($"DELETE FROM {CAT}.product_images WHERE TRUE");
        PgExec($"DELETE FROM {CAT}.product_variant_attributes WHERE TRUE");
        PgExec($"DELETE FROM {CAT}.product_variants WHERE TRUE");
        PgExec($"DELETE FROM {CAT}.product_attributes WHERE TRUE");
        PgExec($"DELETE FROM {CAT}.products WHERE TRUE");
        PgExec($"DELETE FROM {DEF}.product_groups WHERE TRUE");

        // sinifId → cinsiyetAdi (dual reader sorununu önlemek için önceden yükle)
        var sinifGender = LoadSinifGenderMap();

        // Tüm grupları listeye al, sonra işle
        var groups = new List<(int id, string rawKod, string aciklama, int sira, int sinifId)>();
        using (var r = MysqlQuery("SELECT g.Id, g.kod, g.aciklama, g.siraNo, g.urunSinifId FROM dfurungruplari g ORDER BY g.Id"))
        {
            while (r.Read())
            {
                groups.Add((
                    r.GetInt32(0),
                    r.IsDBNull(1) ? "" : r.GetString(1),
                    r.IsDBNull(2) ? "" : r.GetString(2),
                    r.IsDBNull(3) ? 0 : r.GetInt32(3),
                    r.IsDBNull(4) ? 0 : r.GetInt32(4)
                ));
            }
        }

        // NormCompare(cleanName) → canonical pg Id (typo-toleranslı deduplication)
        var nameToGroupId = new Dictionary<string, Guid>(StringComparer.Ordinal);
        int count = 0, skipped = 0;

        foreach (var (oldId, rawKod, aciklama, sira, sinifId) in groups)
        {
            string baseName = (aciklama.Length > 0 ? aciklama : rawKod).Trim();
            string genderName = sinifGender.TryGetValue(sinifId, out var g) ? g : "Cinsiyetsiz";
            string cleanName = StripGenderPrefix(baseName, genderName);
            string normKey = NormCompare(cleanName);

            if (nameToGroupId.TryGetValue(normKey, out var existingId))
            {
                // Duplicate isim (veya typo variant): aynı canonical gruba map et
                productGroupMap[oldId] = existingId;
                skipped++;
                continue;
            }

            var newId = NewId();
            productGroupMap[oldId] = newId;
            nameToGroupId[normKey] = newId;

            PgExec($@"INSERT INTO {DEF}.product_groups
                (""Id"", ""Code"", ""NameI18n"", ""IsActive"", ""SortOrder"", ""CreatedAt"", ""IsDeleted"")
                VALUES (@id, @code, @name::jsonb, TRUE, @sort, @now, FALSE)",
                ("id", newId), ("code", $"grp_{oldId}"), ("name", I18n(cleanName)), ("sort", sira), ("now", Now));
            count++;
        }
        Log($"  ✓ {count} ProductGroup eklendi, {skipped} duplicate birleştirildi");
        return Task.CompletedTask;
    }

    // ─── FAZ 8: GRUP ADLARI DÜZELT (mevcut veri — sadece UPDATE) ────────────
    static Task Phase8_FixGroupNames()
    {
        Log("FAZ 8: Mevcut grup adlarından cinsiyet prefixi kaldırılıyor...");

        var sinifGender = LoadSinifGenderMap();

        var groups = new List<(int id, string aciklama, int sinifId)>();
        using (var r = MysqlQuery("SELECT g.Id, g.aciklama, g.urunSinifId FROM dfurungruplari g ORDER BY g.Id"))
        {
            while (r.Read())
                groups.Add((r.GetInt32(0), r.IsDBNull(1) ? "" : r.GetString(1), r.IsDBNull(2) ? 0 : r.GetInt32(2)));
        }

        int updated = 0;
        foreach (var (id, aciklama, sinifId) in groups)
        {
            string genderName = sinifGender.TryGetValue(sinifId, out var g) ? g : "Cinsiyetsiz";
            string cleanName = StripGenderPrefix(aciklama, genderName);

            PgExec($@"UPDATE {DEF}.product_groups SET ""NameI18n"" = @name::jsonb WHERE ""Code"" = @code",
                ("name", I18n(cleanName)), ("code", $"grp_{id}"));
            updated++;
        }

        Log($"  ✓ {updated} grup adı güncellendi");
        return Task.CompletedTask;
    }

    // ─── FAZ 5: PRODUCTS ─────────────────────────────────────────────────────
    static Task Phase5_Products()
    {
        Log("FAZ 5: Products...");
        EnsureAttrTypeMaps();
        EnsureProductGroupMap();
        EnsureBrandValueMap();

        PgExec($"DELETE FROM {CAT}.product_attributes WHERE TRUE");
        PgExec($"DELETE FROM {CAT}.products WHERE TRUE");

        // B-09 guard (2026-07-18): eşlenemeyen eski grup varsa aktarım BAŞLAMADAN durur.
        // Eski davranış (rastgele ilk gruba sessiz fallback) 110 grubun / kataloğun %37'sinin
        // Pantolon'a düşmesine yol açmıştı — bkz. docs/urun-grup-eslesme-analizi-2026-07-18.md
        var eksikGruplar = new List<(int id, string ad, int urun)>();
        using (var rg = MysqlQuery(@"SELECT COALESCE(p.urunGrupId,0), COALESCE(g.aciklama,''), COUNT(*)
                FROM apurunler p LEFT JOIN dfurungruplari g ON p.urunGrupId = g.Id
                WHERE p.urunKodu IS NOT NULL AND p.urunKodu != ''
                AND p.urunKodu IN (SELECT urunkodu FROM yeniurunkodlari)
                GROUP BY 1, 2"))
            while (rg.Read())
            {
                int gid = Convert.ToInt32(rg.GetValue(0));
                if (!productGroupMap.ContainsKey(gid))
                    eksikGruplar.Add((gid, rg.IsDBNull(1) ? "" : rg.GetString(1), Convert.ToInt32(rg.GetValue(2))));
            }
        if (eksikGruplar.Count > 0)
        {
            Log("  ✗ EŞLENEMEYEN ESKİ GRUPLAR — docs/grup_eslesme.md'ye satır ekleyin:");
            foreach (var (gid, ad, urun) in eksikGruplar.OrderByDescending(x => x.urun))
                Log($"    grupId={gid} '{ad}' — {urun} ürün");
            throw new Exception($"FAZ 5 DURDURULDU: {eksikGruplar.Count} eski grup eşlenemedi (sessiz varsayılan kaldırıldı).");
        }

        using var r = MysqlQuery(@"SELECT Id, urunKodu, urunAdi, urunInternetAdi, markaId, urunGrupId,
            alisFiyati, satisFiyati, kdvOrani, tedarikciUrunKodu,
            interneteAcik, satisaAcik, olusturmaTarihi, guncellemeTarihi
            FROM apurunler WHERE urunKodu IS NOT NULL AND urunKodu != ''
            AND urunKodu IN (SELECT urunkodu FROM yeniurunkodlari) -- 2026-07-09: yalnız aktif ürün listesi aktarılır
            ORDER BY Id");

        int count = 0;
        var productBatch = new List<object?[]>();
        var attrBatch = new List<(Guid productId, Guid attrTypeId, Guid attrValueId)>();

        while (r.Read())
        {
            int oldId = r.GetInt32(0);
            string kod = r.GetString(1);
            string ad = r.IsDBNull(2) ? kod : r.GetString(2);
            int markaId = r.IsDBNull(4) ? 0 : r.GetInt32(4);
            int grupId = r.IsDBNull(5) ? 0 : r.GetInt32(5);
            decimal alisFiyati = r.IsDBNull(6) ? 0 : (decimal)r.GetDouble(6);
            decimal satisFiyati = r.IsDBNull(7) ? 0 : (decimal)r.GetDouble(7);
            int kdv = r.IsDBNull(8) ? 20 : r.GetInt32(8);
            string? tedUrunKod = r.IsDBNull(9) ? null : r.GetString(9);
            bool interneteAcik = !r.IsDBNull(10) && r[10].ToString() == "1";
            bool satisaAcik = !r.IsDBNull(11) && r[11].ToString() == "1";
            DateTime? createdAt = r.IsDBNull(12) ? null : r.GetDateTime(12);
            DateTime? updatedAt = r.IsDBNull(13) ? null : r.GetDateTime(13);

            var newId = NewId();
            productMap[oldId] = newId;

            var groupId = productGroupMap[grupId]; // guard: tüm gruplar çözüldü, fallback yok (B-09)
            bool isActive = interneteAcik && satisaAcik;
            DateTime created = createdAt.HasValue ? DateTime.SpecifyKind(createdAt.Value, DateTimeKind.Utc) : Now;
            DateTime? updated = updatedAt.HasValue ? DateTime.SpecifyKind(updatedAt.Value, DateTimeKind.Utc) : null;

            productBatch.Add(new object?[]
            {
                newId, groupId, kod, I18n(ad), satisFiyati,
                alisFiyati == 0m ? null : (object)alisFiyati, kdv, isActive,
                isActive,   // IsSaleOpen = isActive (M1 backfill'le tutarlı; satisaAcik değer-migration'ı ayrı karar)
                string.IsNullOrEmpty(tedUrunKod) ? null : (object)tedUrunKod,
                null, "[]", created, (object?)updated, false
            });

            if (markaId > 0 && brandValueMap.TryGetValue(markaId, out var brandValId))
                attrBatch.Add((newId, markaTypeId, brandValId));

            count++;
            if (productBatch.Count >= 500)
            {
                FlushProducts(productBatch);
                productBatch.Clear();
            }
            if (attrBatch.Count >= 500)
            {
                // attrBatch bu turda henüz flush edilmemiş productBatch satırlarına referans verebilir — önce onları yaz.
                if (productBatch.Count > 0) { FlushProducts(productBatch); productBatch.Clear(); }
                FlushProductAttributes(attrBatch);
                attrBatch.Clear();
            }
            if (count % 10000 == 0) Log($"  {count} ürün...");
        }

        FlushProducts(productBatch);
        FlushProductAttributes(attrBatch);
        Log($"  ✓ {count} Product");
        return Task.CompletedTask;
    }

    static readonly string[] ProductCols =
        { "Id", "ProductGroupId", "Code", "NameI18n", "BasePrice", "BaseCost", "TaxRate",
          "IsActive", "IsSaleOpen", "SupplierProductCode", "Slug", "Tags", "CreatedAt", "UpdatedAt", "IsDeleted" };
    static readonly string?[] ProductCasts =
        { null, null, null, "jsonb", null, null, null, null, null, null, null, "jsonb", null, null, null };

    static void FlushProducts(List<object?[]> batch) => PgBatchInsert($"{CAT}.products", ProductCols, ProductCasts, batch);

    static void FlushProductAttributes(List<(Guid productId, Guid attrTypeId, Guid attrValueId)> batch)
    {
        var rows = batch.Select(x => new object?[] { NewId(), x.productId, x.attrTypeId, x.attrValueId, Now, false }).ToList();
        PgBatchInsert($"{CAT}.product_attributes",
            new[] { "Id", "ProductId", "AttributeTypeId", "AttributeValueId", "CreatedAt", "IsDeleted" },
            new string?[6], rows);
    }

    // ─── FAZ 6: VARIANTS ─────────────────────────────────────────────────────
    static Task Phase6_Variants()
    {
        Log("FAZ 6: ProductVariants...");
        EnsureAttrTypeMaps();
        EnsureAttrValueMap();
        EnsureProductMap();

        PgExec($"DELETE FROM {CAT}.product_variant_attributes WHERE TRUE");
        PgExec($"DELETE FROM {CAT}.product_variants WHERE TRUE");

        using var r = MysqlQuery(@"SELECT Id, urunId, barkod,
            varyant1TipId, varyant1Degeri,
            varyant2TipId, varyant2Degeri,
            varyant3TipId, varyant3Degeri,
            olusturmaTarihi
            FROM apurunvaryantlari ORDER BY urunId, Id");

        int count = 0, skipped = 0;
        var variantBatch = new List<object?[]>();
        var attrQueue = new List<(Guid variantId, int tipId, string val)>();

        while (r.Read())
        {
            int oldId = r.GetInt32(0);
            int urunId = r.GetInt32(1);
            string? barkod = r.IsDBNull(2) ? null : r.GetString(2);
            DateTime? createdAt = r.IsDBNull(9) ? null : r.GetDateTime(9);

            if (!productMap.TryGetValue(urunId, out var productGuid)) { skipped++; continue; }

            string sku = !string.IsNullOrWhiteSpace(barkod) ? barkod : $"{urunId}-{oldId}";
            var newId = NewId();
            variantMap[oldId] = newId;

            DateTime created = createdAt.HasValue ? DateTime.SpecifyKind(createdAt.Value, DateTimeKind.Utc) : Now;
            variantBatch.Add(new object?[]
            {
                newId, productGuid, sku,
                string.IsNullOrWhiteSpace(barkod) ? null : barkod,
                0m, true, created, false
            });

            for (int ax = 0; ax < 3; ax++)
            {
                int tipId = r.GetInt32(3 + ax * 2);
                string val = r.IsDBNull(4 + ax * 2) ? "" : r.GetString(4 + ax * 2);
                if (tipId != 0 && !string.IsNullOrWhiteSpace(val))
                    attrQueue.Add((newId, tipId, val));
            }

            count++;
            if (variantBatch.Count >= 500)
            {
                FlushVariants(variantBatch);
                variantBatch.Clear();
            }
            if (attrQueue.Count >= 500)
            {
                // attrQueue variantBatch'ten daha hızlı dolar (varyant başına 1-3 attr) — henüz flush
                // edilmemiş variantBatch satırlarına referans verebilir, önce onları yaz.
                if (variantBatch.Count > 0) { FlushVariants(variantBatch); variantBatch.Clear(); }
                FlushVariantAttributes(attrQueue);
                attrQueue.Clear();
            }
            if (count % 20000 == 0) Log($"  {count} varyant...");
        }

        FlushVariants(variantBatch);
        FlushVariantAttributes(attrQueue);
        Log($"  ✓ {count} ProductVariant ({skipped} atlandı)");
        return Task.CompletedTask;
    }

    static void FlushVariants(List<object?[]> batch) => PgBatchInsert($"{CAT}.product_variants",
        new[] { "Id", "ProductId", "Sku", "Barcode", "BasePrice", "IsActive", "CreatedAt", "IsDeleted" },
        new string?[8], batch);

    static void FlushVariantAttributes(List<(Guid variantId, int tipId, string val)> queue)
    {
        var rows = new List<object?[]>();
        foreach (var (variantId, tipId, val) in queue)
        {
            if (!attrTypeMap.TryGetValue(tipId, out var typeGuid)) continue;

            if (!attrValueMap.TryGetValue((tipId, val), out var valGuid))
            {
                valGuid = NewId();
                PgExec($@"INSERT INTO {DEF}.attribute_values
                    (""Id"", ""AttributeTypeId"", ""NameI18n"", ""IsActive"", ""SortOrder"", ""CreatedAt"", ""IsDeleted"")
                    VALUES (@id, @tid, @name::jsonb, TRUE, 0, @now, FALSE)",
                    ("id", valGuid), ("tid", typeGuid), ("name", I18n(val)), ("now", Now));
                attrValueMap[(tipId, val)] = valGuid;
            }

            rows.Add(new object?[] { NewId(), variantId, typeGuid, valGuid, Now, false });
        }

        PgBatchInsert($"{CAT}.product_variant_attributes",
            new[] { "Id", "VariantId", "AttributeTypeId", "AttributeValueId", "CreatedAt", "IsDeleted" },
            new string?[6], rows);
    }

    // ─── FAZ 7: IMAGES ───────────────────────────────────────────────────────
    static Task Phase7_Images()
    {
        Log("FAZ 7: ProductImages...");
        EnsureImageSetMap();
        EnsureProductMap();
        EnsureVariantMap();

        PgExec($"DELETE FROM {CAT}.product_images WHERE TRUE");
        Guid defaultSetId = imageSetMap.Values.First();
        Guid batchId = NewId();
        var variantFirstImage = new HashSet<int>();
        // apurunresimleri'nde aynı (ürün, varyant, dosya) birden çok satırla kayıtlı —
        // hedefe yalnızca ilk satır yazılır (2026-07-06: 651K çift satır bu yüzden oluşmuştu)
        var seenTargetKeys = new HashSet<(Guid, Guid?, string)>();

        // Aynı (ürün, varyant) iki resim setinde birden kayıtlı olabiliyor (Varsayılan + Julude);
        // aynı fotoğraf set başına AYRI dosya adıyla duruyor (…_5639 / …_5650 gibi) — ikisi de
        // alınırsa galeri her pozu iki kez gösteriyor (2026-07-06, P-00022181). Varyant başına
        // tek set seçilir: en çok resmi olan, eşitlikte küçük set id.
        var chosenSet = new Dictionary<(int, int?), (int setId, int cnt)>();
        using (var rs = MysqlQuery(@"SELECT urunId, urunAnaVaryantId, IFNULL(resimSetId,1) AS setId, COUNT(*) AS c
            FROM apurunresimleri
            WHERE isSilindi = 0 AND resimDosyaAdi IS NOT NULL AND resimDosyaAdi != ''
            GROUP BY urunId, urunAnaVaryantId, IFNULL(resimSetId,1)"))
        {
            while (rs.Read())
            {
                var key = (rs.GetInt32(0), rs.IsDBNull(1) ? (int?)null : rs.GetInt32(1));
                int setId = rs.GetInt32(2), c = Convert.ToInt32(rs.GetValue(3));
                if (!chosenSet.TryGetValue(key, out var cur) || c > cur.cnt || (c == cur.cnt && setId < cur.setId))
                    chosenSet[key] = (setId, c);
            }
        }

        using var r = MysqlQuery(@"SELECT resimSetId, urunId, urunAnaVaryantId, resimDosyaAdi, siraNo
            FROM apurunresimleri
            WHERE isSilindi = 0 AND resimDosyaAdi IS NOT NULL AND resimDosyaAdi != ''
            ORDER BY urunId, urunAnaVaryantId, siraNo");

        int count = 0;
        var imgBatch = new List<object?[]>();

        while (r.Read())
        {
            int oldSetId = r.IsDBNull(0) ? 1 : r.GetInt32(0);
            int urunId = r.GetInt32(1);
            int? variantOldId = r.IsDBNull(2) ? null : r.GetInt32(2);
            string fileName = r.GetString(3);
            int siraNo = r.IsDBNull(4) ? 0 : r.GetInt32(4);

            if (!productMap.TryGetValue(urunId, out var productGuid)) continue;
            if (chosenSet.TryGetValue((urunId, variantOldId), out var cs) && cs.setId != oldSetId) continue;

            Guid? variantGuid = null;
            if (variantOldId.HasValue && variantMap.TryGetValue(variantOldId.Value, out var vg))
                variantGuid = vg;

            var setId = imageSetMap.TryGetValue(oldSetId, out var sid) ? sid : defaultSetId;
            if (!seenTargetKeys.Add((productGuid, variantGuid, fileName))) continue;
            bool isVariantCover = variantOldId.HasValue && variantFirstImage.Add(variantOldId.Value);

            imgBatch.Add(new object?[]
            {
                NewId(), productGuid, variantGuid, setId, fileName, siraNo,
                false, isVariantCover, "Active", batchId, Now, false
            });

            count++;
            if (imgBatch.Count >= 500)
            {
                FlushImages(imgBatch);
                imgBatch.Clear();
            }
            if (count % 20000 == 0) Log($"  {count} resim...");
        }

        FlushImages(imgBatch);
        Log($"  ✓ {count} ProductImage");
        return Task.CompletedTask;
    }

    static void FlushImages(List<object?[]> batch) => PgBatchInsert($"{CAT}.product_images",
        new[] { "Id", "ProductId", "VariantId", "ImageSetId", "FileName", "SortOrder",
                "IsProductCover", "IsVariantCover", "Status", "BatchId", "CreatedAt", "IsDeleted" },
        new string?[12], batch);

    // ─── FAZ 9: DUPLICATE GRUP BİRLEŞTİRME (mevcut veri — PostgreSQL only) ──
    // Hem tam eşleşen hem de normalize edilmiş aynı olan isimleri birleştirir.
    // Canonical seçimi: en çok Türkçe karakter içeren isim (Eşofman > Esofman);
    // eşit ise en küçük grp_ numaralı.
    static Task Phase9_MergeGroups()
    {
        Log("FAZ 9: Aynı isimli ürün grupları birleştiriliyor...");

        // Tüm grupları yükle
        var allGroups = new List<(Guid id, string code, string nameTr)>();
        using (var pgr = new NpgsqlCommand(
            $"SELECT \"Id\", \"Code\", \"NameI18n\"->>'tr' FROM {DEF}.product_groups", pg).ExecuteReader())
        {
            while (pgr.Read())
                allGroups.Add((pgr.GetGuid(0), pgr.GetString(1), pgr.GetString(2).Trim()));
        }

        // Normalize edilmiş isme göre grupla (typo + whitespace toleranslı)
        var byNormName = allGroups
            .GroupBy(g => NormCompare(g.nameTr), StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .ToList();

        int mergedCount = 0;
        int updatedProducts = 0;

        foreach (var grp in byNormName)
        {
            // Canonical = en kaliteli isim (en çok Türkçe harf), sonra en küçük grp_ no
            var sorted = grp
                .OrderByDescending(g => TurkishCharScore(g.nameTr))
                .ThenBy(g => CodeNum(g.code))
                .ToList();
            var canonical = sorted[0];
            var duplicateIds = sorted.Skip(1).Select(g => g.id).ToArray();

            // Canonical'ın adı da güncellenebilir (örn. trim düzeltmesi)
            using var nameCmd = new NpgsqlCommand(
                $"UPDATE {DEF}.product_groups SET \"NameI18n\" = @name::jsonb WHERE \"Id\" = @id", pg);
            nameCmd.Parameters.AddWithValue("name", I18n(canonical.nameTr));
            nameCmd.Parameters.AddWithValue("id", canonical.id);
            nameCmd.ExecuteNonQuery();

            // Ürünleri canonical gruba yönlendir
            using var updCmd = new NpgsqlCommand(
                $"UPDATE {CAT}.products SET \"ProductGroupId\" = @can WHERE \"ProductGroupId\" = ANY(@dupes)", pg);
            updCmd.Parameters.AddWithValue("can", canonical.id);
            updCmd.Parameters.AddWithValue("dupes", duplicateIds);
            int affected = updCmd.ExecuteNonQuery();
            updatedProducts += affected;

            // Duplicate grupları sil
            using var delCmd = new NpgsqlCommand(
                $"DELETE FROM {DEF}.product_groups WHERE \"Id\" = ANY(@dupes)", pg);
            delCmd.Parameters.AddWithValue("dupes", duplicateIds);
            delCmd.ExecuteNonQuery();

            Log($"  Birleştirildi: [{string.Join(", ", sorted.Select(g => g.nameTr))}] → \"{canonical.nameTr}\" ({affected} ürün)");
            mergedCount += duplicateIds.Length;
        }

        var remaining = PgCount($"{DEF}.product_groups");
        Log($"  ✓ {mergedCount} duplicate grup silindi, {updatedProducts} ürün yönlendirildi");
        Log($"  Kalan ürün grubu sayısı: {remaining}");
        return Task.CompletedTask;
    }

    // ─── FAZ 10: PRODUCT CİNSİYET ATTRIBUTE AKTARIMI ─────────────────────────
    // MySQL: apurunler.urunGrupId → dfurungruplari.urunSinifId → dfurunsiniflari.cinsiyetId → dfcinsiyetler.cinsiyet
    // PG: product_attributes (AttributeTypeId = cinsiyet type, AttributeValueId = eşleşen değer)
    static async Task Phase10_ProductGender()
    {
        Log("FAZ 10: Ürün cinsiyet attribute'ları aktarılıyor...");

        // 1. PG'den cinsiyet attribute type ve değerlerini yükle
        Guid cinsiyetTypeId;
        using (var cmd = new NpgsqlCommand(
            $"SELECT \"Id\" FROM {DEF}.attribute_types WHERE \"Code\" = 'cinsiyet'", pg))
        {
            var result = cmd.ExecuteScalar();
            if (result is null) { Log("  [HATA] 'cinsiyet' attribute type bulunamadı!"); return; }
            cinsiyetTypeId = (Guid)result;
        }

        var pgValueMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = new NpgsqlCommand(
            $"SELECT \"Id\", \"NameI18n\"->>'tr' FROM {DEF}.attribute_values WHERE \"AttributeTypeId\" = @tid", pg))
        {
            cmd.Parameters.AddWithValue("tid", cinsiyetTypeId);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
                pgValueMap[rdr.GetString(1)] = rdr.GetGuid(0);
        }
        Log($"  PG'de {pgValueMap.Count} cinsiyet değeri: {string.Join(", ", pgValueMap.Keys)}");

        // 2. PG'den ürün code → id map
        var pgProductMap = new Dictionary<string, Guid>(StringComparer.Ordinal);
        using (var cmd = new NpgsqlCommand($"SELECT \"Code\", \"Id\" FROM {CAT}.products WHERE NOT \"IsDeleted\"", pg))
        {
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read()) pgProductMap[rdr.GetString(0)] = rdr.GetGuid(1);
        }
        Log($"  PG'de {pgProductMap.Count} ürün yüklendi.");

        // 3. MySQL'den ürün → cinsiyet eşlemesini oku
        var mysqlGenderMap = new Dictionary<string, string>(StringComparer.Ordinal); // urunKodu → cinsiyet
        using (var r = MysqlQuery(@"
            SELECT p.urunKodu, COALESCE(c.cinsiyet, 'Cinsiyetsiz') AS cinsiyet
            FROM apurunler p
            JOIN dfurungruplari g ON g.Id = p.urunGrupId
            JOIN dfurunsiniflari s ON s.Id = g.urunSinifId
            LEFT JOIN dfcinsiyetler c ON c.Id = s.cinsiyetId
            WHERE p.urunKodu IS NOT NULL AND p.urunKodu != ''
            AND p.urunKodu IN (SELECT urunkodu FROM yeniurunkodlari)"))
        {
            while (r.Read())
            {
                string kod = r.GetString(0);
                string cinsiyet = r.GetString(1);
                if (!mysqlGenderMap.ContainsKey(kod))
                    mysqlGenderMap[kod] = cinsiyet;
            }
        }
        Log($"  MySQL'den {mysqlGenderMap.Count} ürün cinsiyet kaydı okundu.");

        // 4. Zaten cinsiyet attribute'u olan ürünleri atla
        var alreadyHas = new HashSet<Guid>();
        using (var cmd = new NpgsqlCommand(
            $"SELECT \"ProductId\" FROM {CAT}.product_attributes WHERE \"AttributeTypeId\" = @tid AND NOT \"IsDeleted\"", pg))
        {
            cmd.Parameters.AddWithValue("tid", cinsiyetTypeId);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read()) alreadyHas.Add(rdr.GetGuid(0));
        }
        Log($"  Zaten cinsiyet attribute'u olan {alreadyHas.Count} ürün atlanacak.");

        // 5. Insert
        int inserted = 0, skippedNoValue = 0, skippedNotFound = 0;
        var batch = new List<(Guid productId, Guid valueId)>();

        foreach (var (kod, cinsiyet) in mysqlGenderMap)
        {
            if (!pgProductMap.TryGetValue(kod, out var productId)) { skippedNotFound++; continue; }
            if (alreadyHas.Contains(productId)) continue;
            if (!pgValueMap.TryGetValue(cinsiyet, out var valueId)) { skippedNoValue++; continue; }

            batch.Add((productId, valueId));

            if (batch.Count >= 1000)
            {
                FlushGenderAttributes(batch, cinsiyetTypeId);
                inserted += batch.Count;
                batch.Clear();
                Log($"  {inserted} kayıt eklendi...");
            }
        }

        if (batch.Count > 0)
        {
            FlushGenderAttributes(batch, cinsiyetTypeId);
            inserted += batch.Count;
        }

        Log($"  ✓ {inserted} cinsiyet attribute eklendi");
        Log($"  Atlanan (PG'de ürün yok): {skippedNotFound}");
        Log($"  Atlanan (eşleşen PG değeri yok): {skippedNoValue}");
        await Task.CompletedTask;
    }

    static void FlushGenderAttributes(List<(Guid productId, Guid valueId)> batch, Guid cinsiyetTypeId)
    {
        var rows = batch.Select(x => new object?[] { NewId(), x.productId, cinsiyetTypeId, x.valueId, Now, false }).ToList();
        PgBatchInsert($"{CAT}.product_attributes",
            new[] { "Id", "ProductId", "AttributeTypeId", "AttributeValueId", "CreatedAt", "IsDeleted" },
            new string?[6], rows);
    }

    // ─── FAZ 11: ERP VARIANT DATA (Nebim) ────────────────────────────────────
    // dfcolors.colorName (case-insensitiv) → apurunvaryantlari.varyant1Degeri eşleşiyor (renk tipId=1);
    // dfcolors.colorCode oradan çözülür. varyant2TipId=2 → Beden (sizeValue, kod tablosu yok).
    static Task Phase11_ErpVariantData()
    {
        Log("FAZ 11: ErpVariantData (Nebim)...");
        EnsureVariantMap();
        EnsureNebimFirmIntegration();

        PgExec("DELETE FROM integration.erp_variant_data WHERE \"FirmIntegrationId\" = @fid", ("fid", nebimFirmIntegrationId));

        // urunId → urunKodu (model kodu)
        var modelCodes = new Dictionary<int, string>();
        using (var r = MysqlQuery("SELECT Id, urunKodu FROM apurunler WHERE urunKodu IS NOT NULL AND urunKodu != ''" +
            " AND urunKodu IN (SELECT urunkodu FROM yeniurunkodlari)"))
            while (r.Read()) modelCodes[r.GetInt32(0)] = r.GetString(1);
        Log($"  {modelCodes.Count} model kodu yüklendi.");

        // dfcolors: colorName (case-insensitive) → colorCode. Birkaç isim >1 koda sahip (veri kalitesi); ilk görülen kazanır.
        var colorCodeByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using (var r = MysqlQuery("SELECT colorCode, colorName FROM dfcolors WHERE colorName IS NOT NULL AND colorName != ''"))
            while (r.Read())
            {
                string name = r.GetString(1);
                if (!colorCodeByName.ContainsKey(name))
                    colorCodeByName[name] = r.IsDBNull(0) ? "" : r.GetString(0);
            }
        Log($"  {colorCodeByName.Count} dfcolors adı yüklendi.");

        using var vr = MysqlQuery(@"SELECT Id, urunId, barkod,
            varyant1TipId, varyant1Degeri,
            varyant2TipId, varyant2Degeri,
            varyant3TipId, varyant3Degeri
            FROM apurunvaryantlari ORDER BY Id");

        int count = 0, skipped = 0;
        var batch = new List<(Guid variantId, string payloadJson)>();

        while (vr.Read())
        {
            int oldVariantId = vr.GetInt32(0);
            int urunId = vr.GetInt32(1);
            string? barkod = vr.IsDBNull(2) ? null : vr.GetString(2);

            if (!variantMap.TryGetValue(oldVariantId, out var variantGuid)) { skipped++; continue; }

            string? colorName = null, sizeValue = null;
            for (int ax = 0; ax < 3; ax++)
            {
                int tipId = vr.GetInt32(3 + ax * 2);
                string val = vr.IsDBNull(4 + ax * 2) ? "" : vr.GetString(4 + ax * 2);
                if (string.IsNullOrWhiteSpace(val)) continue;
                if (tipId == 1) colorName = val;       // Renk
                else if (tipId == 2) sizeValue = val;  // Beden
            }

            string? colorCode = colorName != null && colorCodeByName.TryGetValue(colorName, out var cc) ? cc : null;
            string? modelCode = modelCodes.TryGetValue(urunId, out var mc) ? mc : null;

            var payload = new Dictionary<string, object?>
            {
                ["erpProductId"] = urunId,
                ["erpVariantId"] = oldVariantId,
                ["modelCode"] = modelCode,
                ["colorName"] = colorName,
                ["colorCode"] = colorCode,
                ["sizeValue"] = sizeValue,
                ["barcode"] = barkod,
            };

            batch.Add((variantGuid, JsonSerializer.Serialize(payload, JsonOpts)));
            count++;

            if (batch.Count >= 500)
            {
                FlushErpVariantData(batch);
                batch.Clear();
            }
            if (count % 20000 == 0) Log($"  {count} erp_variant_data...");
        }

        FlushErpVariantData(batch);
        Log($"  ✓ {count} ErpVariantData ({skipped} atlandı — eşleşen varyant yok)");
        return Task.CompletedTask;
    }

    static void FlushErpVariantData(List<(Guid variantId, string payloadJson)> batch)
    {
        if (batch.Count == 0) return;
        var sb = new StringBuilder();
        sb.Append(@"INSERT INTO integration.erp_variant_data (""Id"",""FirmIntegrationId"",""VariantId"",""Payload"",""CreatedAt"",""IsDeleted"") VALUES ");
        using var cmd = new NpgsqlCommand { Connection = pg, CommandTimeout = 120 };
        int p = 0;
        for (int i = 0; i < batch.Count; i++)
        {
            if (i > 0) sb.Append(',');
            string pid = "p" + p++, pvid = "p" + p++, pjson = "p" + p++, pnow = "p" + p++;
            sb.Append($"(@{pid},@fid,@{pvid},@{pjson}::jsonb,@{pnow},FALSE)");
            cmd.Parameters.AddWithValue(pid, NewId());
            cmd.Parameters.AddWithValue(pvid, batch[i].variantId);
            cmd.Parameters.AddWithValue(pjson, batch[i].payloadJson);
            cmd.Parameters.AddWithValue(pnow, Now);
        }
        cmd.Parameters.AddWithValue("fid", nebimFirmIntegrationId);
        sb.Append(@" ON CONFLICT (""VariantId"",""FirmIntegrationId"") DO UPDATE SET ""Payload""=EXCLUDED.""Payload"", ""UpdatedAt""=EXCLUDED.""CreatedAt""");
        cmd.CommandText = sb.ToString();
        cmd.ExecuteNonQuery();
    }

    // core.core_integration_services + core.core_firm_integrations içinde Nebim/demo kaydını
    // yoksa oluşturur, varsa mevcut Id'yi kullanır (idempotent).
    static void EnsureNebimFirmIntegration()
    {
        if (nebimFirmIntegrationId != Guid.Empty) return;

        Guid demoFirmId = PgScalar<Guid>("SELECT \"Id\" FROM core.core_firms WHERE \"Code\" = 'demo'");

        Guid serviceId;
        var existingService = PgScalarNullable("SELECT \"Id\" FROM core.core_integration_services WHERE \"Code\" = 'nebim'");
        if (existingService is Guid sid) serviceId = sid;
        else
        {
            serviceId = NewId();
            PgExec(@"INSERT INTO core.core_integration_services
                (""Id"", ""Code"", ""NameI18n"", ""ServiceType"", ""IsAvailable"", ""CreatedAt"", ""IsDeleted"")
                VALUES (@id, 'nebim', @name::jsonb, 'erp', TRUE, @now, FALSE)",
                ("id", serviceId), ("name", I18n("Nebim")), ("now", Now));
            Log("  ✓ core_integration_services: 'nebim' oluşturuldu.");
        }

        var existingFi = PgScalarNullable(
            "SELECT \"Id\" FROM core.core_firm_integrations WHERE \"FirmId\" = @f AND \"IntegrationServiceId\" = @s",
            ("f", demoFirmId), ("s", serviceId));
        if (existingFi is Guid fid) { nebimFirmIntegrationId = fid; return; }

        nebimFirmIntegrationId = NewId();
        PgExec(@"INSERT INTO core.core_firm_integrations
            (""Id"", ""FirmId"", ""IntegrationServiceId"", ""Name"", ""Credentials"", ""Settings"", ""IsActive"", ""CreatedAt"", ""IsDeleted"")
            VALUES (@id, @f, @s, 'Nebim ERP', '{}'::jsonb, '{}'::jsonb, TRUE, @now, FALSE)",
            ("id", nebimFirmIntegrationId), ("f", demoFirmId), ("s", serviceId), ("now", Now));
        Log("  ✓ core_firm_integrations: demo↔nebim oluşturuldu.");
    }

    // Türkçe özgün karakterlerin sayısı — daha yüksek = daha doğru yazım
    static int TurkishCharScore(string s) =>
        s.Count(c => "ışğüöçâîûİŞĞÜÖÇÂÎÛ".Contains(c));

    static int CodeNum(string code) =>
        code.StartsWith("grp_") && int.TryParse(code[4..], out int n) ? n : int.MaxValue;

    // ─── MAP LOADERS ─────────────────────────────────────────────────────────
    static void EnsureAttrTypeMaps()
    {
        if (attrTypeMap.Count > 0) return;
        using var r0 = MysqlQuery("SELECT Id, aciklama FROM dfvaryanttipleri");
        var mysqlNames = new Dictionary<int, string>();
        while (r0.Read()) mysqlNames[r0.GetInt32(0)] = r0.GetString(1);

        using var pgr = new NpgsqlCommand($"SELECT \"Id\", \"Code\" FROM {DEF}.attribute_types", pg).ExecuteReader();
        var pgCodes = new Dictionary<string, Guid>();
        while (pgr.Read()) pgCodes[pgr.GetString(1)] = pgr.GetGuid(0);
        if (pgCodes.TryGetValue("marka", out var mg)) markaTypeId = mg;

        foreach (var (id, name) in mysqlNames)
        {
            string code = Slugify(name);
            if (pgCodes.TryGetValue(code, out var g)) attrTypeMap[id] = g;
        }
    }

    static void EnsureAttrValueMap()
    {
        if (attrValueMap.Count > 0) return;
        EnsureAttrTypeMaps();
        using var r0 = MysqlQuery("SELECT varyantTipId, varyantDegeri FROM dfvaryanttipdegerleri");
        var mysqlVals = new List<(int, string)>();
        while (r0.Read()) { if (!r0.IsDBNull(1)) mysqlVals.Add((r0.GetInt32(0), r0.GetString(1))); }

        using var pgr = new NpgsqlCommand($"SELECT \"Id\", \"AttributeTypeId\", \"NameI18n\"->>'tr' FROM {DEF}.attribute_values", pg).ExecuteReader();
        var pgVals = new Dictionary<(Guid, string), Guid>();
        while (pgr.Read()) pgVals[(pgr.GetGuid(1), pgr.GetString(2))] = pgr.GetGuid(0);

        foreach (var (tipId, val) in mysqlVals)
            if (attrTypeMap.TryGetValue(tipId, out var typeGuid) && pgVals.TryGetValue((typeGuid, val), out var valId))
                attrValueMap[(tipId, val)] = valId;
    }

    static void EnsureProductGroupMap()
    {
        if (productGroupMap.Count > 0) return;
        // Code formatı: "grp_{mysqlId}" — doğrudan parse edebiliriz
        var codeToId = new Dictionary<string, Guid>();
        using (var pgr = new NpgsqlCommand($"SELECT \"Id\", \"Code\" FROM {DEF}.product_groups", pg).ExecuteReader())
            while (pgr.Read())
            {
                string code = pgr.GetString(1); // "grp_123" | "kaban" | "spor_ayakkabi" ...
                codeToId[code] = pgr.GetGuid(0);
                if (code.StartsWith("grp_") && int.TryParse(code[4..], out int mid))
                    productGroupMap[mid] = pgr.GetGuid(0);
            }

        // B-09 (2026-07-18): birleştirilen/silinen eski grupların hedefi docs/grup_eslesme.md'den
        // çözülür — "Bustiyer (grp_9)" / "Kaban (kaban)" parantezli kod, "Elbise" = grp_{MySQLID}.
        foreach (var (legacyId, targetCode) in LoadGroupMergeMapFromDoc())
            if (!productGroupMap.ContainsKey(legacyId) && targetCode is not null
                && codeToId.TryGetValue(targetCode, out var gid))
                productGroupMap[legacyId] = gid;
    }

    /// <summary>docs/grup_eslesme.md tablosunu okur: eski grupId → yeni grup Code (null = grup kaldırıldı).</summary>
    static Dictionary<int, string?> LoadGroupMergeMapFromDoc()
    {
        string[] adaylar = { "../../docs/grup_eslesme.md", "docs/grup_eslesme.md", "/opt/ECSProsAI/docs/grup_eslesme.md" };
        var path = adaylar.FirstOrDefault(File.Exists)
            ?? throw new Exception("docs/grup_eslesme.md bulunamadı — grup eşleme haritası kurulamıyor.");
        var map = new Dictionary<int, string?>();
        var satirRx = new Regex(@"^\|\s*(\d+)\s*\|[^|]*\|[^|]*\|([^|]*)\|");
        var kodRx = new Regex(@"\(([a-z0-9_]+)\)");
        foreach (var line in File.ReadLines(path))
        {
            var m = satirRx.Match(line);
            if (!m.Success) continue;
            int lid = int.Parse(m.Groups[1].Value);
            string hedef = m.Groups[2].Value.Trim();
            var km = kodRx.Match(hedef);
            map[lid] = km.Success ? km.Groups[1].Value
                     : hedef.StartsWith("—") ? null
                     : $"grp_{lid}";
        }
        return map;
    }

    static void EnsureProductMap()
    {
        if (productMap.Count > 0) return;
        Log("  [productMap yükleniyor...]");
        using var r0 = MysqlQuery("SELECT Id, urunKodu FROM apurunler WHERE urunKodu IS NOT NULL AND urunKodu != ''" +
            " AND urunKodu IN (SELECT urunkodu FROM yeniurunkodlari)"); // 2026-07-09: keep listesi dışını hiçbir faz eşlemez
        var codes = new Dictionary<string, int>();
        while (r0.Read()) codes[r0.GetString(1)] = r0.GetInt32(0);

        using var pgr = new NpgsqlCommand($"SELECT \"Id\", \"Code\" FROM {CAT}.products", pg).ExecuteReader();
        while (pgr.Read()) { if (codes.TryGetValue(pgr.GetString(1), out var mid)) productMap[mid] = pgr.GetGuid(0); }
        Log($"  [productMap: {productMap.Count}]");
    }

    static void EnsureVariantMap()
    {
        if (variantMap.Count > 0) return;
        Log("  [variantMap yükleniyor...]");
        using var r0 = MysqlQuery("SELECT Id, barkod FROM apurunvaryantlari WHERE barkod IS NOT NULL AND barkod != ''");
        var barcodes = new Dictionary<string, int>();
        while (r0.Read()) { string bc = r0.GetString(1); if (!barcodes.ContainsKey(bc)) barcodes[bc] = r0.GetInt32(0); }

        using var pgr = new NpgsqlCommand($"SELECT \"Id\", \"Barcode\" FROM {CAT}.product_variants WHERE \"Barcode\" IS NOT NULL", pg).ExecuteReader();
        while (pgr.Read()) { string bc = pgr.GetString(1); if (barcodes.TryGetValue(bc, out var mid)) variantMap[mid] = pgr.GetGuid(0); }
        Log($"  [variantMap: {variantMap.Count}]");
    }

    static void EnsureBrandValueMap()
    {
        if (brandValueMap.Count > 0 || markaTypeId == Guid.Empty) return;
        EnsureAttrTypeMaps();
        using var r0 = MysqlQuery("SELECT Id, marka FROM dfmarkalar");
        var brands = new Dictionary<int, string>();
        while (r0.Read()) brands[r0.GetInt32(0)] = r0.GetString(1);

        using var pgr = new NpgsqlCommand($"SELECT \"Id\", \"NameI18n\"->>'tr' FROM {DEF}.attribute_values WHERE \"AttributeTypeId\" = @tid", pg);
        pgr.Parameters.AddWithValue("tid", markaTypeId);
        using var rdr = pgr.ExecuteReader();
        var pgBrands = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        while (rdr.Read()) pgBrands[rdr.GetString(1)] = rdr.GetGuid(0);

        foreach (var (id, name) in brands)
            if (pgBrands.TryGetValue(name, out var g)) brandValueMap[id] = g;
    }

    static void EnsureImageSetMap()
    {
        if (imageSetMap.Count > 0) return;
        using var r0 = MysqlQuery("SELECT Id, setAdi FROM dfresimsetleri");
        var names = new Dictionary<int, string>();
        while (r0.Read()) names[r0.GetInt32(0)] = r0.GetString(1);

        using var pgr = new NpgsqlCommand($"SELECT \"Id\", \"Name\" FROM {DEF}.image_sets", pg).ExecuteReader();
        var pgSets = new Dictionary<string, Guid>();
        while (pgr.Read()) pgSets[pgr.GetString(1)] = pgr.GetGuid(0);

        foreach (var (id, name) in names)
            if (pgSets.TryGetValue(name, out var g)) imageSetMap[id] = g;
    }

    // ─── DB HELPERS ──────────────────────────────────────────────────────────
    // ── Faz 20: GÜVENLİ KATALOG RELOAD ORKESTRATÖRÜ (tek komut) ───────────────────
    // Kataloğu (ürün/varyant/görsel) yeniden yükler; eksik ürünleri (yeniurunkodlari)
    // katar; tüm katalog GUID'leri yenilenir. ClearAll ÇAĞIRMAZ ve Faz 1–4'ü (image_sets/
    // attribute_types/values/product_groups) çalıştırmaz — böylece grup/özellik GUID'leri
    // ve bunlara bağlı kanal kategori filtre kuralları KORUNUR. Firma/platform da korunur
    // (Phase14 upsert). Fazlar haritalarını mevcut DB'den (Ensure*) kurduğundan tek process'te
    // sıralı koşabilirler. Phase14 artık gerçek platformların eski channel verisini de siler.
    // Stok aktarımı BUNA DAHİL DEĞİL — reload doğrulandıktan sonra ayrı adımda koşulur.
    // ─── FAZ 21: ÜRÜN GRUP DÜZELTME (B-09) — mevcut veri, yalnız UPDATE ────────
    // Eski sistemdeki gruba göre ProductGroupId'yi onarır. Tekrar çalıştırılabilir;
    // eşlenemeyen grup varsa HİÇBİR ürün güncellenmeden raporlayıp durur.
    static Task Phase21_FixProductGroups()
    {
        Log("FAZ 21: Ürün grup düzeltme (B-09)...");
        EnsureProductGroupMap();

        var legacyByCode = new Dictionary<string, int>();
        using (var r = MysqlQuery(@"SELECT urunKodu, COALESCE(urunGrupId, 0) FROM apurunler
                WHERE urunKodu IS NOT NULL AND urunKodu != ''
                AND urunKodu IN (SELECT urunkodu FROM yeniurunkodlari)"))
            while (r.Read()) legacyByCode[r.GetString(0)] = Convert.ToInt32(r.GetValue(1));
        Log($"  eski sistemden {legacyByCode.Count} ürün-grup ataması okundu");

        var eksik = legacyByCode.Values.Distinct().Where(gid => !productGroupMap.ContainsKey(gid)).ToList();
        if (eksik.Count > 0)
        {
            foreach (var gid in eksik.OrderByDescending(g => legacyByCode.Count(kv => kv.Value == g)))
                Log($"  ✗ eşlenemeyen eski grup: {gid} ({legacyByCode.Count(kv => kv.Value == gid)} ürün)");
            throw new Exception($"FAZ 21 DURDURULDU: {eksik.Count} eski grup eşlenemedi — docs/grup_eslesme.md'yi tamamlayın.");
        }

        var grupAdi = new Dictionary<Guid, string>();
        using (var pgr = new NpgsqlCommand($"SELECT \"Id\", \"Code\", \"NameI18n\"->>'tr' FROM {DEF}.product_groups", pg).ExecuteReader())
            while (pgr.Read()) grupAdi[pgr.GetGuid(0)] = pgr.IsDBNull(2) ? pgr.GetString(1) : pgr.GetString(2);

        var codes = new List<string>();
        var hedefler = new List<Guid>();
        int dogru = 0, mysqlYok = 0;
        var dagilim = new Dictionary<string, int>();
        using (var pgr = new NpgsqlCommand($"SELECT \"Code\", \"ProductGroupId\" FROM {CAT}.products", pg).ExecuteReader())
            while (pgr.Read())
            {
                string kod = pgr.GetString(0);
                Guid mevcut = pgr.GetGuid(1);
                if (!legacyByCode.TryGetValue(kod, out var gid)) { mysqlYok++; continue; }
                var hedef = productGroupMap[gid];
                if (hedef == mevcut) { dogru++; continue; }
                codes.Add(kod);
                hedefler.Add(hedef);
                var key = $"{grupAdi.GetValueOrDefault(mevcut, "?")} → {grupAdi.GetValueOrDefault(hedef, "?")}";
                dagilim[key] = dagilim.GetValueOrDefault(key) + 1;
            }

        Log($"  zaten doğru: {dogru} · eski sistemde bulunamayan: {mysqlYok} · düzeltilecek: {codes.Count}");
        foreach (var kv in dagilim.OrderByDescending(k => k.Value).Take(25))
            Log($"    {kv.Value,6}  {kv.Key}");

        if (codes.Count > 0)
        {
            using var cmd = new NpgsqlCommand($@"
                UPDATE {CAT}.products AS p
                SET ""ProductGroupId"" = m.gid, ""UpdatedAt"" = now()
                FROM (SELECT unnest(@codes) AS code, unnest(@gids) AS gid) m
                WHERE p.""Code"" = m.code", pg);
            cmd.Parameters.AddWithValue("codes", codes.ToArray());
            cmd.Parameters.AddWithValue("gids", hedefler.ToArray());
            cmd.CommandTimeout = 600;
            int n = cmd.ExecuteNonQuery();
            Log($"  ✓ {n} ürünün grubu düzeltildi");
            PgExec($"ANALYZE {CAT}.products");
            Log("  ✓ ANALYZE catalog.products");
        }
        else Log("  ✓ düzeltilecek ürün yok");
        return Task.CompletedTask;
    }

    static async Task RunCatalogReload()
    {
        Log("╔══ FAZ 20: GÜVENLİ KATALOG RELOAD ══╗");
        Log("  (ClearAll YOK; Faz 1–4 YOK — gruplar/özellikler/kategori kuralları korunur)");
        await Phase5_Products();
        await Phase6_Variants();
        await Phase7_Images();
        await Phase11_ErpVariantData();
        await Phase12_ProductSpecs(null);
        await Phase13_ProductAttributeValues(null);
        Phase19_FiltreRengi();   // renk (Phase13) → filtre_rengi (dfcolors.colorGroup); reload'da kalıcı
        await Phase14_FirmsAndChannelData();
        Log("╚══ FAZ 20 tamamlandı — doğrulama sonrası stok aktarımı ayrı koşulur ══╝");
    }

    // ── Faz 19: filtre_rengi (kürasyonlu renk facet'i) — KAYNAK: dfcolors.colorGroup ─────────
    // Serbest-metin "renk" (colorName) → legacy'nin kürasyonlu grubu (colorGroup) → bizim 25
    // filtre_rengi bucket'ı. Metin sınıflandırma YOK; legacy verisi kullanılır. Bir colorName
    // dfcolors'ta >1 grup taşıyabilir (birleşik renk) → çoklu-değer; sıra dfcolors.Id (ilk = badge).
    // Reload'un parçası (Phase20) olduğundan bir daha silinmez.
    static readonly Dictionary<string, string?> ColorGroupToBucket = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Siyah"]="Siyah", ["Beyaz"]="Beyaz", ["Krem"]="Krem", ["Gri"]="Gri", ["Antrasit"]="Koyu Gri",
        ["Füme"]="Koyu Gri", ["Bej"]="Bej", ["Vizon"]="Bej", ["Kahve"]="Kahve", ["Bakır"]="Kahve",
        ["Lacivert"]="Lacivert", ["Mavi"]="Mavi", ["Saks Mavisi"]="Mavi", ["Turuncu"]="Turuncu",
        ["Kiremit"]="Turuncu", ["Kırmızı"]="Kırmızı", ["Bordo"]="Kırmızı", ["Pembe"]="Pembe",
        ["Pudra"]="Pembe", ["Fuşya"]="Pembe", ["Somon"]="Pembe", ["Mor"]="Mor", ["Sarı"]="Sarı",
        ["Hardal"]="Sarı", ["Yeşil"]="Yeşil", ["Haki"]="Haki",
        // renk olmayan gruplar → atla
        ["Diğer"]=null, ["Renksiz"]=null, ["Desenli"]=null,
    };

    static void Phase19_FiltreRengi()
    {
        Log("FAZ 19: filtre_rengi (dfcolors.colorGroup kaynaklı, kürasyonlu)...");
        var filtreTypeId = PgScalar<Guid>("SELECT \"Id\" FROM definition.attribute_types WHERE \"Code\"='filtre_rengi'");
        var renkTypeId = PgScalar<Guid>("SELECT \"Id\" FROM definition.attribute_types WHERE \"Code\"='renk'");

        // bucket adı → id (bizim 25 değer)
        var bucketId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        using (var r = new NpgsqlCommand(
            $"SELECT \"NameI18n\"->>'tr', \"Id\" FROM definition.attribute_values WHERE \"AttributeTypeId\"='{filtreTypeId}'", pg).ExecuteReader())
            while (r.Read()) bucketId[r.GetString(0)] = r.GetGuid(1);

        // colorName → sıralı distinct colorGroup (dfcolors, Id sırası = ilk kazanır)
        var nameGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        using (var r = MysqlQuery("SELECT colorName, colorGroup FROM dfcolors WHERE colorName<>'' AND colorGroup IS NOT NULL AND colorGroup<>'' ORDER BY Id"))
            while (r.Read())
            {
                string nm = r.GetString(0).Trim(), gp = r.GetString(1).Trim();
                if (!nameGroups.TryGetValue(nm, out var list)) nameGroups[nm] = list = new();
                if (!list.Contains(gp, StringComparer.OrdinalIgnoreCase)) list.Add(gp);
            }
        Log($"  {nameGroups.Count} dfcolors renk adı (grup eşlemeli) yüklendi.");

        // bizim renk değerleri: value id → metin
        var renkValues = new List<(Guid id, string txt)>();
        using (var r = new NpgsqlCommand(
            $"SELECT \"Id\", \"NameI18n\"->>'tr' FROM definition.attribute_values WHERE \"AttributeTypeId\"='{renkTypeId}'", pg).ExecuteReader())
            while (r.Read()) if (!r.IsDBNull(1)) renkValues.Add((r.GetGuid(0), r.GetString(1)));

        // renk value id → sıralı bucket id listesi (dfcolors grup üzerinden)
        var mappings = new List<(Guid renkId, Guid bkt, int ord)>();
        int eslesmeyenAd = 0, gruptanBucketYok = 0, cokluDeger = 0;
        foreach (var (rid, txt) in renkValues)
        {
            if (!nameGroups.TryGetValue(txt.Trim(), out var groups)) { eslesmeyenAd++; continue; }
            int ord = 0; var seen = new HashSet<Guid>();
            foreach (var gp in groups)
            {
                if (!ColorGroupToBucket.TryGetValue(gp, out var bname) || bname is null) continue; // Diğer/Renksiz/Desenli
                if (!bucketId.TryGetValue(bname, out var bid)) continue;
                if (seen.Add(bid)) mappings.Add((rid, bid, ord++));
            }
            if (seen.Count == 0) gruptanBucketYok++;
            else if (seen.Count > 1) cokluDeger++;
        }
        Log($"  {renkValues.Count} renk değeri → {mappings.Count} filtre_rengi eşleme ({eslesmeyenAd} dfcolors'ta yok, {gruptanBucketYok} yalnız renk-olmayan grup, {cokluDeger} çoklu-değer).");

        // Mevcut filtre_rengi atamalarını sil (idempotent) + temp eşleme tablosundan set-based yeniden kur
        PgExec($"DELETE FROM {CAT}.product_variant_attributes WHERE \"AttributeTypeId\" = @t", ("t", filtreTypeId));
        PgExec("DROP TABLE IF EXISTS tmp_renk_bucket");
        PgExec("CREATE TEMP TABLE tmp_renk_bucket (renk uuid, bucket uuid, ord int)");
        for (int i = 0; i < mappings.Count; i += 500)
        {
            var sb = new StringBuilder("INSERT INTO tmp_renk_bucket (renk,bucket,ord) VALUES ");
            using var cmd = new NpgsqlCommand { Connection = pg };
            int p = 0;
            var slice = mappings.Skip(i).Take(500).ToList();
            for (int j = 0; j < slice.Count; j++)
            {
                if (j > 0) sb.Append(',');
                string a = "p" + p++, b = "p" + p++, c = "p" + p++;
                sb.Append($"(@{a},@{b},@{c})");
                cmd.Parameters.AddWithValue(a, slice[j].renkId);
                cmd.Parameters.AddWithValue(b, slice[j].bkt);
                cmd.Parameters.AddWithValue(c, slice[j].ord);
            }
            cmd.CommandText = sb.ToString();
            cmd.ExecuteNonQuery();
        }

        // Her varyantın renk atamasına karşılık filtre_rengi ata (çoklu-grup → çoklu satır)
        using (var cmd = new NpgsqlCommand($@"INSERT INTO {CAT}.product_variant_attributes
            (""Id"",""VariantId"",""AttributeTypeId"",""AttributeValueId"",""CreatedAt"",""IsDeleted"")
            SELECT gen_random_uuid(), va.""VariantId"", @ft, m.bucket, @now, false
            FROM {CAT}.product_variant_attributes va
            JOIN tmp_renk_bucket m ON m.renk = va.""AttributeValueId""
            WHERE va.""AttributeTypeId"" = @rt", pg)
        { CommandTimeout = 300 })
        {
            cmd.Parameters.AddWithValue("ft", filtreTypeId);
            cmd.Parameters.AddWithValue("rt", renkTypeId);
            cmd.Parameters.AddWithValue("now", Now);
            int eklenen = cmd.ExecuteNonQuery();
            Log($"  ✓ {eklenen} filtre_rengi ataması yazıldı.");
        }
        PgExec("DROP TABLE IF EXISTS tmp_renk_bucket");
        PgExec($"ANALYZE {CAT}.product_variant_attributes");
        Log("FAZ 19 tamam.");
    }

    // ── Faz 16: Depo yapısı (üçlü: Depo → Kısım → Birim/Raf) ──────────────────────
    // Eski juludedb: dfstorages (38 "depo") = yeni KISIM; dfstorageunits (124K raf) = yeni BİRİM;
    // gerçek stok opproductlocations (1 satır = 1 fiziksel adet). Onaylanan tasarım (2026-07-14):
    //   3 fiziki depo — Merkez (IsCentral, D012) / Mağaza (M002) / Ayakkabı (M004).
    //   Tekkeköy (kod TD) KULLANIM DIŞI → oluşturulmaz (stoklu rafları düşer, raporlanır).
    //   İnternet satışına açıklık (IsSellableOnline) eski dfstorages.type'tan gelir: 1=AÇIK, 2=KAPALI
    //   (İade/Defo/Bağış + Mağaza Reyonu type=2 → kapalı; blok katları + Ayakkabı Reyon type=1 → açık).
    //   Yalnız STOKLU raflar taşınır (opproductlocations'ta geçen storageUnitId'ler).
    // BU FAZ YALNIZ YAPIYI yazar (inv_warehouses/sections/bins). Stok MİKTARI + rezervler
    // inv_stocks'un yeniden şekillenmesine (BinId — cutover) bağlı olduğundan burada YAZILMAZ;
    // yalnız dağılım read-only RAPORLANIR (doğrulama + düşen birim görünürlüğü için).
    static Task Phase16_WarehouseStructure()
    {
        Log("FAZ 16: Depo yapısı (Depo → Kısım → Birim)...");

        // 3 fiziki depo: Code → (Ad, ErpKodu, IsCentral)
        var warehouses = new (string Code, string Name, string Erp, bool Central)[]
        {
            ("MERKEZ",   "Merkez Depo", "D012", true),
            ("MAGAZA",   "Mağaza",      "M002", false),
            ("AYAKKABI", "Ayakkabı",    "M004", false),
        };

        // Eski dfstorages.code → hedef fiziki depo Code. Listede olmayan her kod bilinçli
        // olarak DIŞARIDA (boş WEBDEPO bölümleri, Tekkeköy TD, Güngören, sanal, stüdyo,
        // satıcı/kabul/sipariş/çağrı/resimlik/personel/kapalı/pazaryeri...).
        // İnternet satışına açıklık (IsSellableOnline) burada DEĞİL, dfstorages.type'tan
        // türetilir: eski sistemde type=1 internet satışına AÇIK, type=2 KAPALI
        // (fnGetStorageType + fninternetstokbyvaryantid tek kaynak). Örn. Mağaza Reyonu (MR)
        // type=2 → kapalı; Ayakkabı Reyon (AR) type=1 → açık.
        var sectionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Merkez — A/B blok katları + merdiven
            ["K0B"] = "MERKEZ", ["K1A"] = "MERKEZ", ["K1B"] = "MERKEZ",
            ["K2A"] = "MERKEZ", ["K2B"] = "MERKEZ", ["K3A"] = "MERKEZ",
            ["K3B"] = "MERKEZ", ["K4A"] = "MERKEZ", ["K4B"] = "MERKEZ",
            ["K5A"] = "MERKEZ", ["K5B"] = "MERKEZ", ["MDA"] = "MERKEZ",
            // Merkez — iade/defo/bağış kısımları
            ["IADE"] = "MERKEZ", ["DEFO"] = "MERKEZ", ["BAGIS"] = "MERKEZ",
            // Mağaza + Ayakkabı reyonları
            ["MR"] = "MAGAZA",
            ["AR"] = "AYAKKABI",
        };

        // 1) dfstorages: Id → (code, name, sortOrder, type) — type: 1=internet satışına açık, 2=kapalı
        var storages = new Dictionary<int, (string Code, string Name, int Sort, int Type)>();
        using (var r = MysqlQuery("SELECT Id, code, name, sortOrder, type FROM dfstorages"))
            while (r.Read())
                storages[r.GetInt32(0)] = (
                    r.IsDBNull(1) ? "" : r.GetString(1).Trim(),
                    r.IsDBNull(2) ? "" : r.GetString(2).Trim(),
                    r.IsDBNull(3) ? 0 : r.GetInt32(3),
                    r.IsDBNull(4) ? 2 : r.GetInt32(4));
        Log($"  {storages.Count} dfstorages yüklendi.");

        // 2) Stoklu birimler: storageUnitId → adet (opproductlocations satır sayısı) + rezerv
        var unitStock = new Dictionary<int, int>();
        var unitReserved = new Dictionary<int, int>();
        using (var r = MysqlQuery(
            "SELECT storageUnitId, COUNT(*) AS adet, " +
            "SUM(CASE WHEN transactionDetailId IS NOT NULL THEN 1 ELSE 0 END) AS rezerv " +
            "FROM opproductlocations GROUP BY storageUnitId"))
            while (r.Read())
            {
                int uid = r.GetInt32(0);
                unitStock[uid] = Convert.ToInt32(r.GetValue(1));
                unitReserved[uid] = Convert.ToInt32(r.GetValue(2));
            }
        long toplamAdet = unitStock.Values.Sum(v => (long)v);
        Log($"  {unitStock.Count} stoklu raf, toplam {toplamAdet} fiziksel adet (opproductlocations).");

        // 3) Stoklu birimlerin raf bilgisi: unitId → (storageId, barcode, shelfNo)
        var units = new Dictionary<int, (int StorageId, string Barcode, string ShelfNo)>();
        using (var r = MysqlQuery(
            "SELECT u.Id, u.storageId, u.barcode, u.shelfUnitNumber FROM dfstorageunits u " +
            "WHERE u.Id IN (SELECT DISTINCT storageUnitId FROM opproductlocations)"))
            while (r.Read())
                units[r.GetInt32(0)] = (
                    r.IsDBNull(1) ? 0 : r.GetInt32(1),
                    r.IsDBNull(2) ? "" : r.GetString(2).Trim(),
                    r.IsDBNull(3) ? "" : r.GetString(3).Trim());
        Log($"  {units.Count} stoklu rafın yeri çözüldü.");

        // 4) Hedef temizle (yeni/boş tablolar — GUID'ler yeniden üretilir, henüz referanslayan yok)
        PgExec("DELETE FROM inventory.inv_warehouse_bins");
        PgExec("DELETE FROM inventory.inv_warehouse_sections");
        PgExec("DELETE FROM inventory.inv_warehouses WHERE \"Code\" = ANY(@codes)",
            ("codes", warehouses.Select(w => w.Code).ToArray()));

        // 5) Depolar
        var whId = new Dictionary<string, Guid>();
        foreach (var w in warehouses)
        {
            var id = NewId();
            whId[w.Code] = id;
            PgExec(@"INSERT INTO inventory.inv_warehouses
                (""Id"",""Code"",""NameI18n"",""WarehouseType"",""IsSellableOnline"",""ReservePriority"",
                 ""IsActive"",""SortOrder"",""IsCentral"",""ErpCode"",""CreatedAt"",""IsDeleted"")
                VALUES (@id,@code,@name::jsonb,@type,TRUE,0,TRUE,@sort,@central,@erp,@now,FALSE)",
                ("id", id), ("code", w.Code), ("name", I18n(w.Name)),
                ("type", w.Central ? "depo" : "magaza"),
                ("sort", Array.IndexOf(warehouses, w)), ("central", w.Central), ("erp", w.Erp), ("now", Now));
        }
        Log($"  ✓ {warehouses.Length} depo.");

        // 6) Kısımlar — yalnız eşlenen VE ≥1 stoklu rafı olan dfstorages
        var stockedStorageIds = units.Values.Select(u => u.StorageId).ToHashSet();
        var sectionId = new Dictionary<int, Guid>(); // storageId → yeni section Id
        var sectionWarehouse = new Dictionary<int, Guid>(); // storageId → depo GUID (stok denormalizasyonu için)
        int sectionCount = 0;
        foreach (var (sid, s) in storages)
        {
            if (!stockedStorageIds.Contains(sid)) continue;             // boş kısım atlanır
            if (!sectionMap.TryGetValue(s.Code, out var whCode)) continue; // eşlenmeyen (Tekkeköy/Güngören/...) atlanır
            bool sellable = s.Type == 1;                                // eski dfstorages.type: 1=internet açık, 2=kapalı
            var id = NewId();
            sectionId[sid] = id;
            sectionWarehouse[sid] = whId[whCode];
            PgExec(@"INSERT INTO inventory.inv_warehouse_sections
                (""Id"",""WarehouseId"",""Code"",""Name"",""IsSellableOnline"",""PickingOrder"",
                 ""IsActive"",""SortOrder"",""CreatedAt"",""IsDeleted"")
                VALUES (@id,@wh,@code,@name,@sell,@pick,TRUE,@sort,@now,FALSE)",
                ("id", id), ("wh", whId[whCode]), ("code", s.Code),
                ("name", string.IsNullOrEmpty(s.Name) ? s.Code : s.Name),
                ("sell", sellable), ("pick", s.Sort), ("sort", s.Sort), ("now", Now));
            sectionCount++;
        }
        Log($"  ✓ {sectionCount} kısım (eşlenen + stoklu).");

        // 7) Raflar — stoklu birimler, yalnız taşınan kısımlarda; (SectionId,Code) tekilliği korunur
        var binRows = new List<object?[]>();
        var seenCode = new HashSet<string>();   // sectionId|code
        var seenBarcode = new HashSet<string>();
        var binByUnit = new Dictionary<int, (Guid Bin, Guid Section, Guid Warehouse)>(); // stok için: unitId → yeni raf/kısım/depo
        int binCount = 0;
        foreach (var (uid, u) in units)
        {
            if (!sectionId.TryGetValue(u.StorageId, out var secId)) continue; // düşen (Tekkeköy vs.) — 9. adımda raporlanır
            string code = string.IsNullOrEmpty(u.ShelfNo) ? (string.IsNullOrEmpty(u.Barcode) ? $"AUTO-{uid}" : u.Barcode) : u.ShelfNo;
            if (!seenCode.Add(secId + "|" + code.ToLowerInvariant())) code = $"{code}-{uid}";
            string barcode = string.IsNullOrEmpty(u.Barcode) ? $"AUTO-{uid}" : u.Barcode;
            if (!seenBarcode.Add(barcode.ToLowerInvariant())) barcode = $"{barcode}-{uid}";
            var binId = NewId();
            binByUnit[uid] = (binId, secId, sectionWarehouse[u.StorageId]);
            binRows.Add(new object?[] { binId, secId, code, barcode, DBNull.Value, 0, true, 0, Now, false });
            binCount++;
            if (binRows.Count >= 500)
            {
                PgBatchInsert("inventory.inv_warehouse_bins",
                    new[] { "Id", "SectionId", "Code", "Barcode", "Name", "PickingOrder", "IsActive", "SortOrder", "CreatedAt", "IsDeleted" },
                    new string?[] { null, null, null, null, null, null, null, null, null, null }, binRows);
                binRows.Clear();
            }
        }
        PgBatchInsert("inventory.inv_warehouse_bins",
            new[] { "Id", "SectionId", "Code", "Barcode", "Name", "PickingOrder", "IsActive", "SortOrder", "CreatedAt", "IsDeleted" },
            new string?[] { null, null, null, null, null, null, null, null, null, null }, binRows);
        Log($"  ✓ {binCount} raf (birim).");

        // 8) STOK MİKTARI + REZERVLER: opproductlocations → inv_stocks (varyant+raf başına adet).
        //    1 satır = 1 fiziksel adet; transactionDetailId dolu = rezerve. Handler cutover
        //    ERTELENDİ (kullanıcı kararı) — yalnız VERİ yazılır; mevcut handler'lara dokunulmaz.
        EnsureVariantMap();
        Log("  Stok aktarımı: opproductlocations → inv_stocks...");

        // Temizle (reload sonrası eski/demo inv_stocks yetim; rezerv FK'si önce silinir)
        PgExec("DELETE FROM inventory.inv_stock_reservations");
        PgExec("DELETE FROM inventory.inv_stocks");

        string[] stockCols = { "Id", "VariantId", "WarehouseId", "LocationId", "SectionId", "BinId", "StockType", "Quantity", "ReservedQuantity", "CreatedAt", "IsDeleted" };
        var stockByVarBin = new Dictionary<(Guid v, Guid b), Guid>();
        var stockRows = new List<object?[]>();
        long yazilanAdet = 0, yazilanRezerv = 0; int stokSatir = 0, atlananAdet = 0;
        using (var r = MysqlQuery(
            "SELECT productVariantId, storageUnitId, COUNT(*) AS adet, " +
            "SUM(CASE WHEN transactionDetailId IS NOT NULL THEN 1 ELSE 0 END) AS rezerv " +
            "FROM opproductlocations GROUP BY productVariantId, storageUnitId"))
            while (r.Read())
            {
                int lvid = r.GetInt32(0), luid = r.GetInt32(1);
                int adet = Convert.ToInt32(r.GetValue(2)), rez = Convert.ToInt32(r.GetValue(3));
                if (!variantMap.TryGetValue(lvid, out var vg) || !binByUnit.TryGetValue(luid, out var b)) { atlananAdet += adet; continue; }
                var stockId = NewId();
                stockByVarBin[(vg, b.Bin)] = stockId;
                stockRows.Add(new object?[] { stockId, vg, b.Warehouse, DBNull.Value, b.Section, b.Bin, "physical", adet, rez, Now, false });
                yazilanAdet += adet; yazilanRezerv += rez; stokSatir++;
                if (stockRows.Count >= 1000) { PgBatchInsert("inventory.inv_stocks", stockCols, new string?[stockCols.Length], stockRows); stockRows.Clear(); }
            }
        PgBatchInsert("inventory.inv_stocks", stockCols, new string?[stockCols.Length], stockRows);
        Log($"  ✓ {stokSatir} inv_stocks (adet={yazilanAdet}, rezerv={yazilanRezerv}); atlanan adet={atlananAdet} (eşleşmeyen varyant/raf).");

        // Rezervler: (variant, raf, tip, detailId) başına — LegacyReferenceId = eski detailId.
        string[] resCols = { "Id", "StockId", "VariantId", "WarehouseId", "LocationId", "Quantity", "ReferenceType", "ReferenceId", "LegacyReferenceId", "Status", "CreatedAt", "IsDeleted" };
        var resRows = new List<object?[]>();
        int resSatir = 0, resAtlanan = 0;
        using (var r = MysqlQuery(
            "SELECT productVariantId, storageUnitId, transactionType, transactionDetailId, COUNT(*) AS adet " +
            "FROM opproductlocations WHERE transactionDetailId IS NOT NULL " +
            "GROUP BY productVariantId, storageUnitId, transactionType, transactionDetailId"))
            while (r.Read())
            {
                int lvid = r.GetInt32(0), luid = r.GetInt32(1), ttype = r.GetInt32(2);
                long detailId = Convert.ToInt64(r.GetValue(3));
                int adet = Convert.ToInt32(r.GetValue(4));
                if (!variantMap.TryGetValue(lvid, out var vg) || !binByUnit.TryGetValue(luid, out var b)
                    || !stockByVarBin.TryGetValue((vg, b.Bin), out var stockId)) { resAtlanan++; continue; }
                string refType = ttype == 1 ? "legacy_order" : ttype == 2 ? "legacy_pick" : "legacy_other";
                resRows.Add(new object?[] { NewId(), stockId, vg, b.Warehouse, DBNull.Value, adet, refType, Guid.Empty, detailId, "reserved", Now, false });
                resSatir++;
                if (resRows.Count >= 1000) { PgBatchInsert("inventory.inv_stock_reservations", resCols, new string?[resCols.Length], resRows); resRows.Clear(); }
            }
        PgBatchInsert("inventory.inv_stock_reservations", resCols, new string?[resCols.Length], resRows);
        Log($"  ✓ {resSatir} inv_stock_reservations (LegacyReferenceId'li); atlanan={resAtlanan}.");
        Log($"  ── Stok aktarımı tamamlandı (adet={yazilanAdet}, düşen/eşleşmeyen={atlananAdet}) ──");

        return Task.CompletedTask;
    }

    static MySqlDataReader MysqlQuery(string sql)
    {
        var cmd = new MySqlCommand(sql, mysql) { CommandTimeout = 600 };
        return cmd.ExecuteReader();
    }

    // ─── FAZ 28: YALNIZ STOK TAZELEME (Faz 26 stok parçası, izole) ───────────
    static void Phase28_StockOnly(bool dryRun)
    {
        Log($"FAZ 28: Yalnız stok tazeleme{(dryRun ? " — KURU ÇALIŞMA" : "")}...");
        var barcodeToVariant = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var row in PgReadRows($"SELECT \"Barcode\", \"Id\" FROM {CAT}.product_variants WHERE \"IsDeleted\"=false AND \"Barcode\" IS NOT NULL AND \"Barcode\"<>''"))
            barcodeToVariant[(string)row[0]] = (Guid)row[1];
        var legacyVariantBarcode = new Dictionary<int, string>();
        using (var r = MysqlQuery("SELECT Id, barkod FROM apurunvaryantlari WHERE barkod IS NOT NULL AND barkod<>''"))
            while (r.Read()) legacyVariantBarcode[r.GetInt32(0)] = r.GetString(1);
        Phase26_Stock(dryRun, barcodeToVariant, legacyVariantBarcode);
    }

    // ─── FAZ 27: KANAL FİYATI TAZELEME (plurunler → channel_variants) ────────
    // Storefront filtre-kategorileri (ör. hersey-99-tl, platformPriceMax) ve gösterilen
    // fiyat KANAL fiyatını (channel_variants.Price) kullanır — BasePrice'ı değil. Faz 26
    // BasePrice'ı (apurunler) güncelledi; bu faz KANAL fiyatını (plurunler, platforma özel/
    // kampanya) tazeler. Barkod ile eşle (ID korunur); channel_variants'a FK yok. Yalnız
    // mishar platformu (misharitalia.com = legacy plurunler.platformId 41). args="dry"→rapor.
    static void Phase27_ChannelPriceRefresh(bool dryRun)
    {
        const int legacyPlatformId = 41; // mishar (misharitalia.com)
        Log($"FAZ 27: Kanal fiyatı tazeleme (plurunler platform {legacyPlatformId} → channel_variants){(dryRun ? " — KURU ÇALIŞMA" : "")}...");

        var fp = PgScalar<Guid>("SELECT \"Id\" FROM core.core_firm_platforms WHERE \"Code\"='mishar'");
        Log($"  Platform: mishar = {fp}");

        var barcodeToVariant = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var row in PgReadRows($"SELECT \"Barcode\", \"Id\" FROM {CAT}.product_variants WHERE \"IsDeleted\"=false AND \"Barcode\" IS NOT NULL AND \"Barcode\"<>''"))
            barcodeToVariant[(string)row[0]] = (Guid)row[1];
        var legacyVariantBarcode = new Dictionary<int, string>();
        using (var r = MysqlQuery("SELECT Id, barkod FROM apurunvaryantlari WHERE barkod IS NOT NULL AND barkod<>''"))
            while (r.Read()) legacyVariantBarcode[r.GetInt32(0)] = r.GetString(1);
        Log($"  Eşleme: {barcodeToVariant.Count} varyant (Barcode), {legacyVariantBarcode.Count} eski varyant.");

        // plurunler → temp (variant_id, price, compare_at, is_active). Kural
        // MigrateChannelDataForPlatform ile birebir: satisFiyati>0 ? satisFiyati : null;
        // compareAt = listeFiyati>0 && != satisFiyati ? listeFiyati : null; is_active = satista.
        PgExec("DROP TABLE IF EXISTS _f27_cv");
        PgExec("CREATE TEMP TABLE _f27_cv(variant_id uuid PRIMARY KEY, price numeric, compare_at numeric, is_active boolean)");
        var batch = new List<object?[]>();
        var eklendi = new HashSet<Guid>();
        int okunan = 0, atlanan = 0;
        using (var r = MysqlQuery($"SELECT urunAnaVaryantId, satisFiyati, listeFiyati, satista FROM plurunler WHERE platformId={legacyPlatformId}"))
            while (r.Read())
            {
                int lvid = r.GetInt32(0);
                if (!legacyVariantBarcode.TryGetValue(lvid, out var bc) || !barcodeToVariant.TryGetValue(bc, out var vg)) { atlanan++; continue; }
                if (!eklendi.Add(vg)) continue; // aynı varyant iki kez gelmesin (ilk kayıt)
                decimal satis = r.IsDBNull(1) ? 0 : (decimal)r.GetDouble(1);
                decimal liste = r.IsDBNull(2) ? 0 : (decimal)r.GetDouble(2);
                bool satista = !r.IsDBNull(3) && Convert.ToBoolean(r.GetValue(3));
                decimal? price = satis > 0 ? satis : null;
                decimal? cmp = liste > 0 && liste != satis ? liste : null;
                batch.Add(new object?[] { vg, price, cmp, satista });
                okunan++;
                if (batch.Count >= 1000) { PgBatchInsert("_f27_cv", new[] { "variant_id", "price", "compare_at", "is_active" }, new string?[4], batch); batch.Clear(); }
            }
        PgBatchInsert("_f27_cv", new[] { "variant_id", "price", "compare_at", "is_active" }, new string?[4], batch);

        long degisecek = PgScalar<long>($@"SELECT COUNT(*) FROM storefront.channel_variants cv JOIN _f27_cv t ON cv.""VariantId""=t.variant_id
            WHERE cv.""FirmPlatformId""='{fp}' AND cv.""IsDeleted""=false
            AND (cv.""Price"" IS DISTINCT FROM t.price OR cv.""CompareAtPrice"" IS DISTINCT FROM t.compare_at OR cv.""IsActive"" IS DISTINCT FROM t.is_active)");
        long yeni = PgScalar<long>($@"SELECT COUNT(*) FROM _f27_cv t WHERE NOT EXISTS (
            SELECT 1 FROM storefront.channel_variants cv WHERE cv.""FirmPlatformId""='{fp}' AND cv.""VariantId""=t.variant_id AND cv.""IsDeleted""=false)");
        // Etki: hersey-99-tl için kanal fiyatı ≤99.99 olacak varyant sayısı (önce/sonra)
        long oncePrice99 = PgScalar<long>($@"SELECT COUNT(*) FROM storefront.channel_variants cv WHERE cv.""FirmPlatformId""='{fp}' AND cv.""IsDeleted""=false AND cv.""Price""<=99.99");
        long sonraPrice99 = PgScalar<long>($@"SELECT COUNT(*) FROM _f27_cv t WHERE t.price<=99.99");
        Log($"    plurunler eşleşen: {okunan} varyant (atlanan={atlanan}).");
        Log($"    DEĞİŞECEK kanal varyantı: {degisecek}; YENİ: {yeni}.");
        Log($"    Kanal fiyatı ≤99.99 varyant — ÖNCE: {oncePrice99} → SONRA (yeni sette): {sonraPrice99}.");

        if (!dryRun)
        {
            PgExec($@"UPDATE storefront.channel_variants cv SET ""Price""=t.price, ""CompareAtPrice""=t.compare_at,
                ""IsActive""=t.is_active, ""PriceType""=CASE WHEN t.price IS NOT NULL THEN 'manual' ELSE NULL END, ""UpdatedAt""=now()
                FROM _f27_cv t WHERE cv.""VariantId""=t.variant_id AND cv.""FirmPlatformId""='{fp}' AND cv.""IsDeleted""=false
                AND (cv.""Price"" IS DISTINCT FROM t.price OR cv.""CompareAtPrice"" IS DISTINCT FROM t.compare_at OR cv.""IsActive"" IS DISTINCT FROM t.is_active)");
            PgExec($@"INSERT INTO storefront.channel_variants (""Id"",""FirmPlatformId"",""VariantId"",""PriceType"",""Price"",""CompareAtPrice"",""IsActive"",""CreatedAt"",""IsDeleted"")
                SELECT gen_random_uuid(), '{fp}', t.variant_id, CASE WHEN t.price IS NOT NULL THEN 'manual' ELSE NULL END, t.price, t.compare_at, t.is_active, now(), false
                FROM _f27_cv t WHERE NOT EXISTS (SELECT 1 FROM storefront.channel_variants cv WHERE cv.""FirmPlatformId""='{fp}' AND cv.""VariantId""=t.variant_id AND cv.""IsDeleted""=false)");
            Log($"    ✓ Kanal fiyatı güncellendi ({degisecek} değişen + {yeni} yeni). ANALYZE + Redis önbelleği önerilir.");
        }
    }

    // PG'den çok satır okuyup her satırı object[] verir (map kurmak için).
    static List<object[]> PgReadRows(string sql)
    {
        var rows = new List<object[]>();
        using var cmd = new NpgsqlCommand(sql, pg) { CommandTimeout = 300 };
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var row = new object[r.FieldCount];
            for (int i = 0; i < r.FieldCount; i++) row[i] = r.IsDBNull(i) ? null! : r.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    // ─── FAZ 26: HEDEFLİ GÜNCELLEME (fiyat/görsel/stok) ──────────────────────
    // Tam-reload Faz 5/6/7'den FARKI: ürün/varyant ID'leri KORUNUR (Code/Barkod ile
    // eşlenir), yalnız fiyat/görsel/stok güncellenir → kanal kategorileri, siparişler,
    // favoriler, rezervasyonlar bozulmaz. args[1]=="dry" → yalnız RAPOR (yazma yok).
    static async Task Phase26_TargetedUpdate(bool dryRun)
    {
        Log($"FAZ 26: Hedefli güncelleme (fiyat/görsel/stok){(dryRun ? " — KURU ÇALIŞMA (yazma yok)" : "")}...");

        // ── Bizim DB'den ID-koruyan eşlemeler ──
        var codeToProduct = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var row in PgReadRows($"SELECT \"Code\", \"Id\" FROM {CAT}.products WHERE \"IsDeleted\"=false"))
            codeToProduct[(string)row[0]] = (Guid)row[1];
        var barcodeToVariant = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var row in PgReadRows($"SELECT \"Barcode\", \"Id\" FROM {CAT}.product_variants WHERE \"IsDeleted\"=false AND \"Barcode\" IS NOT NULL AND \"Barcode\"<>''"))
            barcodeToVariant[(string)row[0]] = (Guid)row[1];
        Log($"  Eşleme: {codeToProduct.Count} ürün (Code), {barcodeToVariant.Count} varyant (Barcode).");

        // Eski Id → iş anahtarı (görsel/stok eski tablolarda Id ile gelir)
        var legacyProductCode = new Dictionary<int, string>();
        using (var r = MysqlQuery("SELECT Id, urunKodu FROM apurunler WHERE urunKodu IS NOT NULL AND urunKodu<>'' AND urunKodu IN (SELECT urunkodu FROM yeniurunkodlari)"))
            while (r.Read()) legacyProductCode[r.GetInt32(0)] = r.GetString(1);
        var legacyVariantBarcode = new Dictionary<int, string>();
        using (var r = MysqlQuery("SELECT Id, barkod FROM apurunvaryantlari WHERE barkod IS NOT NULL AND barkod<>''"))
            while (r.Read()) legacyVariantBarcode[r.GetInt32(0)] = r.GetString(1);
        Log($"  Eski: {legacyProductCode.Count} ürün, {legacyVariantBarcode.Count} varyant (barkodlu).");

        await Phase26_Price(dryRun, codeToProduct);
        Phase26_Images(dryRun, codeToProduct, barcodeToVariant, legacyProductCode, legacyVariantBarcode);
        Phase26_Stock(dryRun, barcodeToVariant, legacyVariantBarcode);

        Log(dryRun
            ? "  KURU ÇALIŞMA bitti — hiçbir şey YAZILMADI. Onaylarsanız 'dry' olmadan çalıştırın."
            : "  Faz 26 yazma tamamlandı. ANALYZE önerilir (products, product_images, inv_stocks).");
    }

    // ── FAZ 26a: FİYAT (temp tablo + tek toplu UPDATE, Code ile) ──
    static Task Phase26_Price(bool dryRun, Dictionary<string, Guid> codeToProduct)
    {
        Log("  [FİYAT] apurunler.satisFiyati/alisFiyati/kdvOrani → products...");
        PgExec("DROP TABLE IF EXISTS _f26_price");
        PgExec("CREATE TEMP TABLE _f26_price(code text PRIMARY KEY, price numeric, cost numeric, tax int)");
        var batch = new List<object?[]>();
        int okunan = 0;
        using (var r = MysqlQuery(@"SELECT urunKodu, satisFiyati, alisFiyati, kdvOrani FROM apurunler
            WHERE urunKodu IS NOT NULL AND urunKodu<>'' AND urunKodu IN (SELECT urunkodu FROM yeniurunkodlari)"))
            while (r.Read())
            {
                string kod = r.GetString(0);
                decimal price = r.IsDBNull(1) ? 0 : (decimal)r.GetDouble(1);
                decimal? cost = r.IsDBNull(2) ? null : (decimal)r.GetDouble(2);
                int tax = r.IsDBNull(3) ? 20 : r.GetInt32(3);
                batch.Add(new object?[] { kod, price, cost == 0m ? null : cost, tax });
                okunan++;
                if (batch.Count >= 1000) { PgBatchInsert("_f26_price", new[] { "code", "price", "cost", "tax" }, new string?[4], batch); batch.Clear(); }
            }
        PgBatchInsert("_f26_price", new[] { "code", "price", "cost", "tax" }, new string?[4], batch);

        long degisecek = (long)PgScalar<long>($@"SELECT COUNT(*) FROM {CAT}.products p JOIN _f26_price t ON p.""Code""=t.code
            WHERE p.""IsDeleted""=false AND (p.""BasePrice"" IS DISTINCT FROM t.price
                OR p.""BaseCost"" IS DISTINCT FROM t.cost OR p.""TaxRate"" IS DISTINCT FROM t.tax)");
        Log($"    Eski listede {okunan} ürün fiyatı; DEĞİŞECEK ürün: {degisecek}.");
        if (!dryRun)
        {
            PgExec($@"UPDATE {CAT}.products p SET ""BasePrice""=t.price, ""BaseCost""=t.cost, ""TaxRate""=t.tax,
                ""UpdatedAt""=now() FROM _f26_price t WHERE p.""Code""=t.code AND p.""IsDeleted""=false
                AND (p.""BasePrice"" IS DISTINCT FROM t.price OR p.""BaseCost"" IS DISTINCT FROM t.cost OR p.""TaxRate"" IS DISTINCT FROM t.tax)");
            Log($"    ✓ {degisecek} ürün fiyatı güncellendi.");
        }
        return Task.CompletedTask;
    }

    // ── FAZ 26b: GÖRSELLER (Code/Barkod ile eşleyip product_images'ı yeniden kur) ──
    // product_images'a gelen FK YOK → ürün başına silip yeniden yazmak güvenli. Değişen
    // dosya adları (resimDosyaAdi) böyle tazelenir. Phase7'nin set-seçimi + dedup mantığı.
    static void Phase26_Images(bool dryRun, Dictionary<string, Guid> codeToProduct,
        Dictionary<string, Guid> barcodeToVariant,
        Dictionary<int, string> legacyProductCode, Dictionary<int, string> legacyVariantBarcode)
    {
        Log("  [GÖRSEL] apurunresimleri.resimDosyaAdi → product_images (yeniden kur)...");
        EnsureImageSetMap();
        // varyant başına tek set (Phase7 ile aynı): en çok resimli set, eşitlikte küçük setId
        var chosenSet = new Dictionary<(int, int?), (int setId, int cnt)>();
        using (var rs = MysqlQuery(@"SELECT urunId, urunAnaVaryantId, IFNULL(resimSetId,1) AS setId, COUNT(*) AS c
            FROM apurunresimleri WHERE isSilindi=0 AND resimDosyaAdi IS NOT NULL AND resimDosyaAdi<>''
            GROUP BY urunId, urunAnaVaryantId, IFNULL(resimSetId,1)"))
            while (rs.Read())
            {
                var key = (rs.GetInt32(0), rs.IsDBNull(1) ? (int?)null : rs.GetInt32(1));
                int setId = rs.GetInt32(2), c = Convert.ToInt32(rs.GetValue(3));
                if (!chosenSet.TryGetValue(key, out var cur) || c > cur.cnt || (c == cur.cnt && setId < cur.setId))
                    chosenSet[key] = (setId, c);
            }

        // Yeni görsel setini temp tabloya kur (product_id, variant_id, set_id, file_name, sort, is_variant_cover)
        PgExec("DROP TABLE IF EXISTS _f26_img");
        PgExec("CREATE TEMP TABLE _f26_img(product_id uuid, variant_id uuid, set_id uuid, file_name text, sort_order int, is_variant_cover boolean)");
        Guid defaultSetId = imageSetMap.Values.First();
        var seen = new HashSet<(Guid, Guid?, string)>();
        var variantFirst = new HashSet<int>();
        var batch = new List<object?[]>();
        int yeniSatir = 0, atlanan = 0;
        using (var r = MysqlQuery(@"SELECT resimSetId, urunId, urunAnaVaryantId, resimDosyaAdi, siraNo
            FROM apurunresimleri WHERE isSilindi=0 AND resimDosyaAdi IS NOT NULL AND resimDosyaAdi<>''
            ORDER BY urunId, urunAnaVaryantId, siraNo"))
            while (r.Read())
            {
                int oldSetId = r.IsDBNull(0) ? 1 : r.GetInt32(0);
                int urunId = r.GetInt32(1);
                int? variantOldId = r.IsDBNull(2) ? null : r.GetInt32(2);
                string fileName = r.GetString(3);
                int siraNo = r.IsDBNull(4) ? 0 : r.GetInt32(4);

                // Eski urunId → Code → bizim ProductId (ID korunur)
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
                if (batch.Count >= 1000) { PgBatchInsert("_f26_img", new[] { "product_id", "variant_id", "set_id", "file_name", "sort_order", "is_variant_cover" }, new string?[6], batch); batch.Clear(); }
            }
        PgBatchInsert("_f26_img", new[] { "product_id", "variant_id", "set_id", "file_name", "sort_order", "is_variant_cover" }, new string?[6], batch);

        long mevcut = PgCount($"{CAT}.product_images");
        // Değişen dosya adları: yeni'de olup mevcutta olmayan (eklenecek/tazelenecek)
        long yeniDosya = PgScalar<long>($@"SELECT COUNT(*) FROM _f26_img n WHERE NOT EXISTS (
            SELECT 1 FROM {CAT}.product_images o WHERE o.""ProductId""=n.product_id AND o.""FileName""=n.file_name AND o.""IsDeleted""=false)");
        // Bayat dosya adları: mevcutta olup yeni'de olmayan (KIRIK — kaldırılacak)
        long bayatDosya = PgScalar<long>($@"SELECT COUNT(*) FROM {CAT}.product_images o WHERE o.""IsDeleted""=false AND NOT EXISTS (
            SELECT 1 FROM _f26_img n WHERE n.product_id=o.""ProductId"" AND n.file_name=o.""FileName"")");
        long etkilenenUrun = PgScalar<long>($@"SELECT COUNT(DISTINCT k) FROM (
            SELECT n.product_id AS k FROM _f26_img n WHERE NOT EXISTS (SELECT 1 FROM {CAT}.product_images o WHERE o.""ProductId""=n.product_id AND o.""FileName""=n.file_name AND o.""IsDeleted""=false)
            UNION SELECT o.""ProductId"" FROM {CAT}.product_images o WHERE o.""IsDeleted""=false AND NOT EXISTS (SELECT 1 FROM _f26_img n WHERE n.product_id=o.""ProductId"" AND n.file_name=o.""FileName"")) z");
        Log($"    Mevcut görsel: {mevcut}; yeni set: {yeniSatir} (atlanan eşleşmeyen: {atlanan}).");
        Log($"    TAZELENECEK dosya adı (yeni): {yeniDosya}; KALDIRILACAK bayat/kırık: {bayatDosya}; etkilenen ÜRÜN: {etkilenenUrun}.");

        if (!dryRun)
        {
            // Tek transaction: tümünü sil + temp'ten yeniden yaz (FK yok, güvenli; hata → rollback)
            using var tx = pg.BeginTransaction();
            try
            {
                new NpgsqlCommand($"DELETE FROM {CAT}.product_images", pg, tx) { CommandTimeout = 300 }.ExecuteNonQuery();
                var batchId = NewId();
                new NpgsqlCommand($@"INSERT INTO {CAT}.product_images
                    (""Id"",""ProductId"",""VariantId"",""ImageSetId"",""FileName"",""SortOrder"",""IsProductCover"",""IsVariantCover"",""Status"",""BatchId"",""CreatedAt"",""IsDeleted"")
                    SELECT gen_random_uuid(), product_id, variant_id, set_id, file_name, sort_order, false, COALESCE(is_variant_cover,false), 'Active', '{batchId}', now(), false FROM _f26_img",
                    pg, tx) { CommandTimeout = 600 }.ExecuteNonQuery();
                tx.Commit();
                Log($"    ✓ product_images yeniden kuruldu ({yeniSatir} satır).");
            }
            catch (Exception ex) { tx.Rollback(); Log($"    ✗ GÖRSEL yazma HATA, geri alındı: {ex.Message}"); throw; }
        }
    }

    // ── FAZ 26c: STOK (yerinde UPDATE, rezervasyon KORUNUR) ──
    // inv_stock_reservations FK'si + canlı rezervler var → SİLİNMEZ. Yalnız Quantity
    // güncellenir; ReservedQuantity ve rezervasyon satırlarına DOKUNULMAZ.
    static void Phase26_Stock(bool dryRun, Dictionary<string, Guid> barcodeToVariant, Dictionary<int, string> legacyVariantBarcode)
    {
        Log("  [STOK] opproductlocations → inv_stocks.Quantity (rezervasyon korunur)...");
        // Eski raf birimi (storageUnitId) → bizim BinId (bin.Barcode = storageUnit barkodu, Phase16)
        var binByBarcode = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var row in PgReadRows("SELECT \"Barcode\", \"Id\" FROM inventory.inv_warehouse_bins WHERE \"Barcode\" IS NOT NULL AND \"Barcode\"<>''"))
            binByBarcode[(string)row[0]] = (Guid)row[1];
        var unitBarcode = new Dictionary<int, string>();
        using (var r = MysqlQuery("SELECT Id, barcode FROM dfstorageunits WHERE barcode IS NOT NULL AND barcode<>''"))
            while (r.Read()) unitBarcode[r.GetInt32(0)] = r.GetString(1);

        // Yeni miktar: (variant, bin) → adet
        PgExec("DROP TABLE IF EXISTS _f26_stock");
        PgExec("CREATE TEMP TABLE _f26_stock(variant_id uuid, bin_id uuid, qty int, PRIMARY KEY(variant_id, bin_id))");
        var batch = new List<object?[]>();
        var eklendi = new HashSet<(Guid, Guid)>();
        int okunan = 0, atlanan = 0;
        // 2026-07-30 DÜZELTME: yalnız REZERVSİZ (transactionDetailId IS NULL) adetler sayılır.
        // transactionDetailId dolu = bir işleme TAHSİS edilmiş (satılmış/transfer/rezerve) →
        // available DEĞİL. Legacy fninternetstokbyvaryantid de böyle sayar. İlk sürüm COUNT(*)
        // ile toplam (rezerveler dahil) sayıyordu → tahsisli stok available görünüyordu (P-00021044).
        using (var r = MysqlQuery("SELECT productVariantId, storageUnitId, SUM(CASE WHEN transactionDetailId IS NULL THEN 1 ELSE 0 END) AS adet FROM opproductlocations GROUP BY productVariantId, storageUnitId"))
            while (r.Read())
            {
                int lvid = r.GetInt32(0), luid = r.GetInt32(1);
                int adet = Convert.ToInt32(r.GetValue(2));
                if (!legacyVariantBarcode.TryGetValue(lvid, out var vbc) || !barcodeToVariant.TryGetValue(vbc, out var vg)
                    || !unitBarcode.TryGetValue(luid, out var ubc) || !binByBarcode.TryGetValue(ubc, out var bg)) { atlanan += adet; continue; }
                if (!eklendi.Add((vg, bg))) continue; // aynı (varyant,bin) iki kez gelmesin
                batch.Add(new object?[] { vg, bg, adet });
                okunan++;
                if (batch.Count >= 1000) { PgBatchInsert("_f26_stock", new[] { "variant_id", "bin_id", "qty" }, new string?[3], batch); batch.Clear(); }
            }
        PgBatchInsert("_f26_stock", new[] { "variant_id", "bin_id", "qty" }, new string?[3], batch);

        long guncellenecek = PgScalar<long>(@"SELECT COUNT(*) FROM inventory.inv_stocks s JOIN _f26_stock t ON s.""VariantId""=t.variant_id AND s.""BinId""=t.bin_id
            WHERE s.""IsDeleted""=false AND s.""Quantity"" IS DISTINCT FROM GREATEST(t.qty, s.""ReservedQuantity"")");
        long yeniKombin = PgScalar<long>(@"SELECT COUNT(*) FROM _f26_stock t WHERE NOT EXISTS (
            SELECT 1 FROM inventory.inv_stocks s WHERE s.""VariantId""=t.variant_id AND s.""BinId""=t.bin_id AND s.""IsDeleted""=false)");
        long sifirlanacak = PgScalar<long>(@"SELECT COUNT(*) FROM inventory.inv_stocks s WHERE s.""IsDeleted""=false AND s.""Quantity"">s.""ReservedQuantity""
            AND NOT EXISTS (SELECT 1 FROM _f26_stock t WHERE t.variant_id=s.""VariantId"" AND t.bin_id=s.""BinId"")");
        long rezervCakisma = PgScalar<long>(@"SELECT COUNT(*) FROM inventory.inv_stocks s JOIN _f26_stock t ON s.""VariantId""=t.variant_id AND s.""BinId""=t.bin_id
            WHERE s.""IsDeleted""=false AND t.qty < s.""ReservedQuantity""");
        Log($"    Eski stok kombinasyonu: {okunan} (atlanan adet={atlanan}).");
        Log($"    GÜNCELLENECEK satır: {guncellenecek}; YENİ (varyant,bin): {yeniKombin}; sıfırlanacak (eski stok bitmiş): {sifirlanacak}; rezerv çakışması (adet<rezerv, rezerv korunur): {rezervCakisma}.");

        if (!dryRun)
        {
            // 1) Mevcut satırlar: Quantity = MAX(yeni adet, rezerv) — asla rezervin altına düşme
            PgExec(@"UPDATE inventory.inv_stocks s SET ""Quantity""=GREATEST(t.qty, s.""ReservedQuantity""), ""UpdatedAt""=now()
                FROM _f26_stock t WHERE s.""VariantId""=t.variant_id AND s.""BinId""=t.bin_id AND s.""IsDeleted""=false
                AND s.""Quantity"" IS DISTINCT FROM GREATEST(t.qty, s.""ReservedQuantity"")");
            // 2) Yeni (varyant,bin) kombinasyonları: bin'in section/warehouse'undan türet
            PgExec(@"INSERT INTO inventory.inv_stocks (""Id"",""VariantId"",""WarehouseId"",""LocationId"",""SectionId"",""BinId"",""StockType"",""Quantity"",""ReservedQuantity"",""CreatedAt"",""IsDeleted"")
                SELECT gen_random_uuid(), t.variant_id, sec.""WarehouseId"", NULL, b.""SectionId"", t.bin_id, 'physical', t.qty, 0, now(), false
                FROM _f26_stock t JOIN inventory.inv_warehouse_bins b ON b.""Id""=t.bin_id JOIN inventory.inv_warehouse_sections sec ON sec.""Id""=b.""SectionId""
                WHERE NOT EXISTS (SELECT 1 FROM inventory.inv_stocks s WHERE s.""VariantId""=t.variant_id AND s.""BinId""=t.bin_id AND s.""IsDeleted""=false)");
            // 3) Eski stok bitmiş: Quantity = ReservedQuantity (available 0; rezerv korunur)
            PgExec(@"UPDATE inventory.inv_stocks s SET ""Quantity""=s.""ReservedQuantity"", ""UpdatedAt""=now()
                WHERE s.""IsDeleted""=false AND s.""Quantity"">s.""ReservedQuantity""
                AND NOT EXISTS (SELECT 1 FROM _f26_stock t WHERE t.variant_id=s.""VariantId"" AND t.bin_id=s.""BinId"")");
            Log($"    ✓ Stok güncellendi ({guncellenecek} satır + {yeniKombin} yeni + {sifirlanacak} sıfırlandı; rezervasyonlara dokunulmadı).");
        }
    }


    static void PgExec(string sql, params (string name, object? value)[] parameters)
    {
        using var cmd = new NpgsqlCommand(sql, pg) { CommandTimeout = 120 };
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    static T PgScalar<T>(string sql)
    {
        using var cmd = new NpgsqlCommand(sql, pg);
        return (T)cmd.ExecuteScalar()!;
    }

    // Sonuç yoksa null döner (PgScalar<T> aksine — WHERE ile eşleşen kayıt olmayabilir).
    static object? PgScalarNullable(string sql, params (string name, object? value)[] parameters)
    {
        using var cmd = new NpgsqlCommand(sql, pg);
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        var result = cmd.ExecuteScalar();
        return result is DBNull ? null : result;
    }

    static long PgCount(string table)
    {
        using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {table}", pg);
        return (long)cmd.ExecuteScalar()!;
    }

    // Çok satırlı tek INSERT — yüksek hacimli fazlarda (ürün/varyant/resim) tek tek
    // round-trip yerine batch (varsayılan çağrı yeri: 500 satırlık öbekler).
    static void PgBatchInsert(string tableFull, string[] columns, string?[] casts, List<object?[]> rows)
    {
        if (rows.Count == 0) return;
        var sb = new StringBuilder();
        sb.Append("INSERT INTO ").Append(tableFull).Append(" (\"").Append(string.Join("\",\"", columns)).Append("\") VALUES ");
        using var cmd = new NpgsqlCommand { Connection = pg, CommandTimeout = 120 };
        int p = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('(');
            var row = rows[i];
            for (int c = 0; c < columns.Length; c++)
            {
                if (c > 0) sb.Append(',');
                string pname = "p" + p++;
                sb.Append('@').Append(pname);
                if (casts[c] != null) sb.Append("::").Append(casts[c]);
                cmd.Parameters.AddWithValue(pname, row[c] ?? DBNull.Value);
            }
            sb.Append(')');
        }
        cmd.CommandText = sb.ToString();
        cmd.ExecuteNonQuery();
    }

    static string Slugify(string s) => s.ToLowerInvariant()
        .Replace(" ", "_").Replace("ı", "i").Replace("ş", "s").Replace("ğ", "g")
        .Replace("ü", "u").Replace("ö", "o").Replace("ç", "c").Replace("â", "a")
        .Replace("î", "i").Replace("û", "u").Replace("/", "_").Replace("-", "_")
        .Replace("(", "").Replace(")", "").Replace(".", "");

    // Türkçe karakterleri ASCII'ye indirger — uzunluk korunur (1→1 dönüşüm)
    static string NormCompare(string s) => s.ToLowerInvariant()
        .Replace('ı', 'i').Replace('ş', 's').Replace('ğ', 'g')
        .Replace('ü', 'u').Replace('ö', 'o').Replace('ç', 'c')
        .Replace('â', 'a').Replace('î', 'i').Replace('û', 'u');

    // Grup adının başındaki cinsiyet prefixini kaldırır.
    // Typo toleranslı: "Kadin" → "Kadın" gibi normalize edip eşleştirir,
    // strip için normalize uzunluğu kullanır (char sayısı NormCompare'de korunur).
    static string StripGenderPrefix(string groupName, string genderName)
    {
        if (genderName.Equals("Cinsiyetsiz", StringComparison.OrdinalIgnoreCase))
            return groupName;

        string normGroup = NormCompare(groupName);
        string normGender = NormCompare(genderName);

        // normGender.Length == genderName.Length (char-by-char dönüşüm)
        if (normGroup.StartsWith(normGender + " ", StringComparison.Ordinal))
            return groupName[(normGender.Length + 1)..].TrimStart();

        return groupName;
    }

    // sinifId → cinsiyetAdi map'i MySQL'den yükler
    static Dictionary<int, string> LoadSinifGenderMap()
    {
        var map = new Dictionary<int, string>();
        using var r = MysqlQuery(@"SELECT s.Id, COALESCE(c.cinsiyet, 'Cinsiyetsiz')
            FROM dfurunsiniflari s LEFT JOIN dfcinsiyetler c ON s.cinsiyetId = c.Id");
        while (r.Read()) map[r.GetInt32(0)] = r.GetString(1);
        return map;
    }

    // ─── FAZ 12: ÜRÜN ÖZELLİK DEĞERLERİ (apurunbedenozellikleri + apurunaciklamalari) ──
    // Bu iki kaynak tablo migration'a hiç dahil edilmemişti (bkz. P-00022000 örneği).
    // apurunbedenozellikleri → beden başına ölçü (Product+Beden'e özgü) → catalog.product_axis_sub_attribute_values
    // apurunaciklamalari     → ürün başına tek özellik (Kalıp/Astar/Fermuar/Esneklik/...) → catalog.product_attributes
    // KURAL: definition.attribute_types'a asla yeni satır eklenmez — sadece var olan tiplerin
    // attribute_values'ına yeni değer eklenebilir (gerektiğinde), FilterColor kavramı kaldırıldığı için ona hiç dokunulmaz.

    // apurunaciklamalari anahtarı → hedef attribute_type kodu. Kumaş Özelliği (kompozisyon metni,
    // "%97 Polyester %3 Likra" gibi) kasıtlı olarak burada YOK — CustomValue'ya serbest metin olarak yazılır,
    // yeni attribute_value oluşturmaz (kumas_turu picklist'ini kirletmemek için). "Ekstra Askı" ve
    // "Açıklama ve Uyarı" ile Arapça i18n eş anahtarları bu geçişte atlanır (ayrı bir takip konusu).
    static readonly Dictionary<string, (string code, bool numeric)> AciklamaKeyMap = new()
    {
        ["Astar"] = ("astar_durumu", false),
        ["Fermuar"] = ("fermuar", false),
        ["Zipper"] = ("fermuar", false),
        ["Esneklik"] = ("esneklik", false),
        ["Kalıp"] = ("kalip", false),
        ["Ürün Boy"] = ("urun_boyu", true),
        ["İç Bacak Boyu"] = ("ic_uzunluk", true),
        ["Taban Özelliği"] = ("taban_ozelligi", false),
        ["Taban Yükseklik"] = ("taban_yuksekligi", true),
        ["Platform Boy"] = ("taban_yuksekligi", true),
        ["Dış Materyal"] = ("dis_materyal", false),
        ["Çanta Ağzı"] = ("canta_agzi", false),
        ["Askı Tipi"] = ("aski_tipi", false),
        ["Askı Boyu"] = ("aski_boyu", true),
        ["İç Cep"] = ("ic_cep", false),
        ["Balen"] = ("balen", false),
        ["Underwire"] = ("balen", false),
        ["Dolgu"] = ("dolgu", false),
        ["İç Yüzey"] = ("ic_yuzey", false),
        ["Primer"] = ("primer", false),
        ["Topuk Boyu"] = ("topuk_boyu", true),
    };

    static readonly Dictionary<string, string> KalipTypoFix = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Slim Feat"] = "Slim Fit",
        ["Normal Kalıp4"] = "Normal Kalıp",
        ["Standrat Beden"] = "Standart Beden",
    };

    // apurunbedenozellikleri.ozellik serbest metnini definition.attribute_types koduna eşler.
    // Öncelik sırası önemli: "iç bacak boyu" hem "bacak" hem "boy" içerir, genel "boy" kuralından önce kontrol edilmeli.
    static string? MapBedenOzellik(string ozellikRaw)
    {
        var norm = new string(ozellikRaw.ToLowerInvariant().Where(c => char.IsLetter(c) || c == ' ').ToArray());
        norm = System.Text.RegularExpressions.Regex.Replace(norm, @"\bcm\b|\bc\b|ölçüsü", " ");
        norm = System.Text.RegularExpressions.Regex.Replace(norm, @"\s+", " ").Trim();

        if (norm.Contains("bacak")) return "ic_uzunluk";
        if (norm.Contains("omuz")) return "omuz_genisligi";
        if (norm.Contains("basen")) return "basen";
        if (norm.Contains("göğüs")) return "gogus";
        if (norm.Contains("kol")) return "kol_boyu";
        if (norm.Contains("bel")) return "bel";
        if (norm.Contains("yırtmaç")) return null; // niche, karşılığı yok — atla
        if (norm.Contains("boy")) return "urun_boyu"; // Ürün/Alt/Üst/Elbise/Ceket/Yelek/Gömlek/Dış/İç Boy — kalan tüm "boy" varyasyonları
        return null;
    }

    static string StripCmUnit(string s)
    {
        var t = s.Trim();
        t = System.Text.RegularExpressions.Regex.Replace(t, @"(?i)\s*cm\)?\s*$", "");
        return t.Trim().TrimEnd(')').Trim();
    }

    static readonly Dictionary<(Guid type, string norm), Guid> specValueCache = new();

    static Guid GetOrCreateAttributeValue(Guid typeId, string rawName)
    {
        var name = rawName.Trim();
        var key = (typeId, NormCompare(name));
        if (specValueCache.TryGetValue(key, out var existing)) return existing;

        var found = PgScalarNullable(
            $"SELECT \"Id\" FROM {DEF}.attribute_values WHERE \"AttributeTypeId\" = @tid AND lower(\"NameI18n\"->>'tr') = lower(@name)",
            ("tid", typeId), ("name", name));
        if (found is Guid g)
        {
            specValueCache[key] = g;
            return g;
        }

        var newId = NewId();
        PgExec($@"INSERT INTO {DEF}.attribute_values
            (""Id"", ""AttributeTypeId"", ""NameI18n"", ""IsActive"", ""SortOrder"", ""CreatedAt"", ""IsDeleted"")
            VALUES (@id, @tid, @name::jsonb, TRUE, 0, @now, FALSE)",
            ("id", newId), ("tid", typeId), ("name", I18n(name)), ("now", Now));
        specValueCache[key] = newId;
        return newId;
    }

    static Task Phase12_ProductSpecs(string? testProductCode)
    {
        Log($"FAZ 12: Ürün özellik değerleri (beden ölçüleri + açıklamalar){(testProductCode is null ? "" : $" — TEST: {testProductCode}")}...");
        EnsureProductMap();

        // Hedef attribute type kodları → Guid (definition şemasından, hiçbiri burada oluşturulmaz)
        var neededCodes = AciklamaKeyMap.Values.Select(v => v.code)
            .Concat(new[] { "gogus", "bel", "basen", "kol_boyu", "omuz_genisligi", "urun_boyu", "ic_uzunluk", "kumas_turu" })
            .Distinct().ToList();
        var typeIdByCode = new Dictionary<string, Guid>();
        using (var pgr = new NpgsqlCommand($"SELECT \"Id\", \"Code\" FROM {DEF}.attribute_types", pg).ExecuteReader())
            while (pgr.Read())
            {
                var code = pgr.GetString(1);
                if (neededCodes.Contains(code)) typeIdByCode[code] = pgr.GetGuid(0);
            }
        foreach (var c in neededCodes)
            if (!typeIdByCode.ContainsKey(c))
                Log($"  ⚠ UYARI: definition.attribute_types içinde '{c}' bulunamadı — o hedefe yazılan her şey atlanacak.");

        // Beden (S/M/L/...) attribute_value adı → Guid
        var bedenValueByName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        using (var pgr = new NpgsqlCommand(
            $"SELECT av.\"Id\", av.\"NameI18n\"->>'tr' FROM {DEF}.attribute_values av JOIN {DEF}.attribute_types at ON at.\"Id\" = av.\"AttributeTypeId\" WHERE at.\"Code\" = 'beden'",
            pg).ExecuteReader())
            while (pgr.Read()) bedenValueByName[pgr.GetString(1)] = pgr.GetGuid(0);

        string? codeFilter = testProductCode;

        // ── 12a: apurunbedenozellikleri → product_axis_sub_attribute_values ──
        Log("  12a: Beden özellikleri...");
        PgExec($"DELETE FROM {CAT}.product_axis_sub_attribute_values"); // idempotent yeniden çalıştırma

        string bedenSql = @"SELECT b.urunId, u.urunKodu, b.beden, b.ozellik, b.deger
            FROM apurunbedenozellikleri b JOIN apurunler u ON u.Id = b.urunId
            AND u.urunKodu IN (SELECT urunkodu FROM yeniurunkodlari)
            WHERE b.deger IS NOT NULL AND b.deger != ''" +
            (codeFilter is null ? "" : " AND u.urunKodu = @code") +
            " ORDER BY b.urunId, b.beden";

        var grouped = new Dictionary<(int urunId, string beden), List<(string label, string deger)>>();
        using (var cmd = new MySqlCommand(bedenSql, mysql) { CommandTimeout = 600 })
        {
            if (codeFilter != null) cmd.Parameters.AddWithValue("@code", codeFilter);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                int urunId = r.GetInt32(0);
                string beden = r.GetString(2).Trim();
                string ozellik = r.GetString(3).Trim();
                string deger = r.GetString(4).Trim();
                var key = (urunId, beden);
                if (!grouped.TryGetValue(key, out var list)) grouped[key] = list = new();
                list.Add((ozellik, deger));
            }
        }

        int axisInserted = 0, axisSkippedNoProduct = 0, axisSkippedNoBeden = 0, axisSkippedNoType = 0;
        var axisBatch = new List<object?[]>();
        foreach (var ((urunId, beden), rows) in grouped)
        {
            if (!productMap.TryGetValue(urunId, out var productGuid)) { axisSkippedNoProduct++; continue; }
            if (!bedenValueByName.TryGetValue(beden, out var bedenValueId)) { axisSkippedNoBeden++; continue; }

            var byCode = new Dictionary<string, List<(string label, string deger)>>();
            foreach (var (label, deger) in rows)
            {
                var code = MapBedenOzellik(label);
                if (code is null || !typeIdByCode.TryGetValue(code, out _)) { axisSkippedNoType++; continue; }
                if (!byCode.TryGetValue(code, out var list)) byCode[code] = list = new();
                list.Add((label, deger));
            }

            foreach (var (code, items) in byCode)
            {
                var typeId = typeIdByCode[code];
                string value = items.Count == 1
                    ? items[0].deger
                    : string.Join(" / ", items.Select(i => $"{i.label}: {i.deger}"));

                axisBatch.Add(new object?[] { NewId(), productGuid, bedenValueId, typeId, value, Now, false });
                axisInserted++;
            }

            if (axisBatch.Count >= 500) { FlushAxisSubAttrValues(axisBatch); axisBatch.Clear(); }
        }
        FlushAxisSubAttrValues(axisBatch);
        Log($"  ✓ 12a: {axisInserted} satır eklendi. Atlanan: ürün yok={axisSkippedNoProduct}, beden eşleşmedi={axisSkippedNoBeden}, tip eşleşmedi={axisSkippedNoType}");

        // ── 12b: apurunaciklamalari → product_attributes ──
        Log("  12b: Ürün açıklama özellikleri (Kalıp/Astar/Fermuar/Esneklik/...)...");

        // İdempotent yeniden çalıştırma: sadece bu fazın yazdığı tipleri temizle (marka/cinsiyet gibi
        // başka fazların yazdığı satırlara dokunma). Tek-ürün test çalıştırmasından kalan satırlar da
        // burada temizlenir, aksi halde yeni unique index'e (ProductId, AttributeTypeId, AttributeValueId) çarpar.
        var aciklamaTypeIds = AciklamaKeyMap.Values.Select(v => v.code).Append("kumas_turu").Distinct()
            .Where(typeIdByCode.ContainsKey).Select(c => typeIdByCode[c]).ToList();
        if (aciklamaTypeIds.Count > 0)
        {
            using var delCmd = new NpgsqlCommand(
                $"DELETE FROM {CAT}.product_attributes WHERE \"AttributeTypeId\" = ANY(@ids)" +
                (codeFilter is null ? "" : $" AND \"ProductId\" IN (SELECT \"Id\" FROM {CAT}.products WHERE \"Code\" = @code)"),
                pg) { CommandTimeout = 120 };
            delCmd.Parameters.AddWithValue("ids", aciklamaTypeIds.ToArray());
            if (codeFilter != null) delCmd.Parameters.AddWithValue("code", codeFilter);
            delCmd.ExecuteNonQuery();
        }

        string aciklamaSql = "SELECT a.urunId, u.urunKodu, a.urunAciklama FROM apurunaciklamalari a JOIN apurunler u ON u.Id = a.urunId AND u.urunKodu IN (SELECT urunkodu FROM yeniurunkodlari) WHERE a.urunAciklama IS NOT NULL AND a.urunAciklama != ''" +
            (codeFilter is null ? "" : " AND u.urunKodu = @code");

        int attrInserted = 0, attrSkippedNoProduct = 0, compositionInserted = 0;
        var attrBatch = new List<object?[]>();
        var seenPerProduct = new HashSet<(Guid product, Guid type, Guid? value)>();

        using (var cmd = new MySqlCommand(aciklamaSql, mysql) { CommandTimeout = 600 })
        {
            if (codeFilter != null) cmd.Parameters.AddWithValue("@code", codeFilter);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                int urunId = r.GetInt32(0);
                string text = r.GetString(2);
                if (!productMap.TryGetValue(urunId, out var productGuid)) { attrSkippedNoProduct++; continue; }

                foreach (var seg in text.Split("</br>", StringSplitOptions.RemoveEmptyEntries))
                {
                    var idx = seg.IndexOf(':');
                    if (idx <= 0) continue;
                    var key = seg[..idx].Trim();
                    var rawVal = seg[(idx + 1)..].Trim();
                    if (rawVal.Length == 0) continue;

                    if (key == "Kumaş Özelliği")
                    {
                        if (!typeIdByCode.TryGetValue("kumas_turu", out var kumasTypeId)) continue;
                        var ck = (productGuid, kumasTypeId, (Guid?)null);
                        if (!seenPerProduct.Add(ck)) continue;
                        var customJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["composition"] = rawVal }, JsonOpts);
                        attrBatch.Add(new object?[] { NewId(), productGuid, kumasTypeId, null, customJson, Now, false });
                        compositionInserted++;
                        continue;
                    }

                    if (!AciklamaKeyMap.TryGetValue(key, out var mapping)) continue; // Arapça eş anahtarlar / Ekstra Askı / Açıklama ve Uyarı vb. — bu fazda atlanıyor
                    if (!typeIdByCode.TryGetValue(mapping.code, out var typeId)) continue;

                    string val = mapping.numeric ? StripCmUnit(rawVal) : rawVal;
                    if (mapping.code == "kalip" && KalipTypoFix.TryGetValue(val, out var fixedVal)) val = fixedVal;
                    if (val.Length == 0) continue;

                    var valueId = GetOrCreateAttributeValue(typeId, val);
                    var seenKey = (productGuid, typeId, (Guid?)valueId);
                    if (!seenPerProduct.Add(seenKey)) continue;

                    attrBatch.Add(new object?[] { NewId(), productGuid, typeId, valueId, null, Now, false });
                    attrInserted++;
                }

                if (attrBatch.Count >= 500) { FlushProductSpecAttributes(attrBatch); attrBatch.Clear(); }
            }
        }
        FlushProductSpecAttributes(attrBatch);
        Log($"  ✓ 12b: {attrInserted} özellik + {compositionInserted} kumaş kompozisyonu (CustomValue) eklendi. Atlanan: ürün yok={attrSkippedNoProduct}");

        return Task.CompletedTask;
    }

    static void FlushAxisSubAttrValues(List<object?[]> batch) => PgBatchInsert($"{CAT}.product_axis_sub_attribute_values",
        new[] { "Id", "ProductId", "AttributeValueId", "SubAttributeTypeId", "Value", "CreatedAt", "IsDeleted" },
        new string?[7], batch);

    static void FlushProductSpecAttributes(List<object?[]> batch) => PgBatchInsert($"{CAT}.product_attributes",
        new[] { "Id", "ProductId", "AttributeTypeId", "AttributeValueId", "CustomValue", "CreatedAt", "IsDeleted" },
        new string?[] { null, null, null, null, "jsonb", null, null }, batch);

    // ─── FAZ 13: apurunvaryanttipdegerleri → product_attributes (ürün bazlı gerçek özellik değerleri) ──
    // Kök neden: apurunvaryanttipleri sadece "bu ürüne bu tip atanmış" bilgisini taşır, DEĞERİ taşımaz.
    // Gerçek değer (örn. Cinsiyet=Kadın) apurunvaryanttipdegerleri'nde urunId+varyantTipId+varyantDegeri
    // olarak saklı — bu tablo hiçbir fazda okunmuyordu, bu yüzden P-00021204 gibi ürünlerde Cinsiyet,
    // Kumaş Tipi, Yaş Grubu, Desen, Yaka Tipi, Kol Tipi, Malzeme, Season hiç aktarılmamıştı (Faz 10
    // sadece ürün sınıfının varsayılan cinsiyetini kullanıyordu, ürüne özel gerçek atamayı değil).
    static Task Phase13_ProductAttributeValues(string? testProductCode)
    {
        Log($"FAZ 13: Ürün bazlı özellik değerleri (apurunvaryanttipdegerleri){(testProductCode is null ? "" : $" — TEST: {testProductCode}")}...");
        EnsureAttrTypeMaps();
        EnsureProductMap();

        // Legacy tipId → hedef definition.attribute_types kodu. Renk(1)/Beden(2) varyant ekseni
        // olduğu için Faz 6'da işleniyor, burada atlanıyor. Ops/takip alanları (Kampanya Kodu,
        // Tedarikçi, Ürün Grubu, Yıl Sezon, Ürün Tipi, Takipli Ürün, En Adetli/Çok Satan, İade &
        // Değişim, Satış Değerlendirme) gerçek attribute değil, atlanıyor. Bazı legacy tipler
        // önceki cleanup'ta silinip başka koda yönlendirilmişti: Kumaş Tipi(27)→kumas_turu,
        // Yaka Stili(33)→yaka_tipi, Meteryal(42)→malzeme (bkz. attribute_types_cleanup notları).
        // Cep(30)/Tipi(35)/Stil(31) belirsiz veya kasıtlı kaldırılmış — atlanıyor.
        var codeOverride = new Dictionary<int, string> { [27] = "kumas_turu", [33] = "yaka_tipi", [42] = "malzeme" };
        var skip = new HashSet<int> { 1, 2, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 29, 30, 31, 35 };

        var codeToId = new Dictionary<string, Guid>();
        using (var pgr = new NpgsqlCommand($"SELECT \"Id\", \"Code\" FROM {DEF}.attribute_types", pg).ExecuteReader())
            while (pgr.Read()) codeToId[pgr.GetString(1)] = pgr.GetGuid(0);

        Guid? ResolveTypeId(int tipId)
        {
            if (skip.Contains(tipId)) return null;
            if (codeOverride.TryGetValue(tipId, out var code))
                return codeToId.TryGetValue(code, out var g) ? g : null;
            return attrTypeMap.TryGetValue(tipId, out var g2) ? g2 : null;
        }

        string sql = @"SELECT avd.urunId, avd.varyantTipId, avd.varyantDegeri
            FROM apurunvaryanttipdegerleri avd
            JOIN apurunler u ON u.Id = avd.urunId
            AND u.urunKodu IN (SELECT urunkodu FROM yeniurunkodlari)
            WHERE avd.varyantDegeri IS NOT NULL AND avd.varyantDegeri != ''" +
            (testProductCode is null ? "" : " AND u.urunKodu = @code");

        int attempted = 0, actuallyInserted = 0, skippedNoProduct = 0, skippedNoType = 0;
        var batch = new List<object?[]>();

        void Flush()
        {
            if (batch.Count == 0) return;
            actuallyInserted += PgBatchInsertOnConflictDoNothing($"{CAT}.product_attributes",
                new[] { "Id", "ProductId", "AttributeTypeId", "AttributeValueId", "CreatedAt", "IsDeleted" },
                "ProductId\",\"AttributeTypeId\",\"AttributeValueId", batch);
            batch.Clear();
        }

        using (var cmd = new MySqlCommand(sql, mysql) { CommandTimeout = 600 })
        {
            if (testProductCode != null) cmd.Parameters.AddWithValue("@code", testProductCode);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                int urunId = r.GetInt32(0);
                int tipId = r.GetInt32(1);
                string deger = r.IsDBNull(2) ? "" : r.GetString(2).Trim();
                if (deger.Length == 0) continue;

                if (!productMap.TryGetValue(urunId, out var productGuid)) { skippedNoProduct++; continue; }

                var typeId = ResolveTypeId(tipId);
                if (typeId is null) { skippedNoType++; continue; }

                var valueId = GetOrCreateAttributeValue(typeId.Value, deger);
                batch.Add(new object?[] { NewId(), productGuid, typeId.Value, valueId, Now, false });
                attempted++;

                if (batch.Count >= 500) Flush();
                if (attempted % 50000 == 0) Log($"  {attempted} satır işlendi...");
            }
        }
        Flush();

        Log($"  ✓ Faz 13: {attempted} satır denendi, {actuallyInserted} yeni eklendi (geri kalanı zaten mevcuttu — ON CONFLICT DO NOTHING). Atlanan: ürün yok={skippedNoProduct}, tip atlandı/eşleşmedi={skippedNoType}");
        return Task.CompletedTask;
    }

    // ON CONFLICT (...) DO NOTHING destekli batch insert — Faz 13, diğer fazların (Faz 10/12) zaten
    // yazmış olabileceği aynı (ProductId, AttributeTypeId, AttributeValueId) üçlüsüyle çakışabilir.
    static int PgBatchInsertOnConflictDoNothing(string tableFull, string[] columns, string conflictCols, List<object?[]> rows)
    {
        if (rows.Count == 0) return 0;
        var sb = new StringBuilder();
        sb.Append("INSERT INTO ").Append(tableFull).Append(" (\"").Append(string.Join("\",\"", columns)).Append("\") VALUES ");
        using var cmd = new NpgsqlCommand { Connection = pg, CommandTimeout = 120 };
        int p = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('(');
            var row = rows[i];
            for (int c = 0; c < columns.Length; c++)
            {
                if (c > 0) sb.Append(',');
                string pname = "p" + p++;
                sb.Append('@').Append(pname);
                cmd.Parameters.AddWithValue(pname, row[c] ?? DBNull.Value);
            }
            sb.Append(')');
        }
        sb.Append(" ON CONFLICT (\"").Append(conflictCols).Append("\") DO NOTHING");
        cmd.CommandText = sb.ToString();
        return cmd.ExecuteNonQuery();
    }

    // ─── FAZ 14: GERÇEK FİRMA/SİTE + KANAL ÜRÜN VERİSİ (dfplatforms 1/2/41, plurunler) ──
    // Kapsam: sadece kendi sitelerimiz — Tozlu(1)/Julude(2)/Mishar(41), tipi "site".
    // Pazaryerleri bu fazda aktarılmıyor. Seed/demo Firm+FirmPlatform (Code'u hedef
    // kodlar dışında olan her şey) silinir, üzerlerine bağlı storefront demo verisi de temizlenir.
    // Sonra 2 gerçek firma (Mişaroğlu, Eldi Tekstil) + 3 site upsert edilir ve
    // plurunler'daki fiyat/satış bilgisi Storefront.ChannelProduct/ChannelVariant'a aktarılır.
    // Idempotent: Firma/Platform Code'a göre upsert, ürün verisi (FirmPlatformId,ProductId/VariantId)
    // unique index'ine göre ON CONFLICT DO UPDATE — yeniden çalıştırılabilir.
    static Task Phase14_FirmsAndChannelData()
    {
        Log("FAZ 14: Gerçek Firma/Site + kanal ürün verisi (platform 1/2/41)...");
        EnsureProductMap();
        EnsureVariantMap();

        var keepCodes = new[] { "misaroglu", "eldi" };

        // ── 1) Seed/demo Firma + FirmPlatform + bağlı storefront verisini temizle ──
        var seedFirmIds = new List<Guid>();
        using (var cmd = new NpgsqlCommand(
            "SELECT \"Id\" FROM core.core_firms WHERE NOT (\"Code\" = ANY(@codes))", pg))
        {
            cmd.Parameters.AddWithValue("codes", keepCodes);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read()) seedFirmIds.Add(rdr.GetGuid(0));
        }

        if (seedFirmIds.Count > 0)
        {
            var seedPlatformIds = new List<Guid>();
            using (var cmd = new NpgsqlCommand(
                "SELECT \"Id\" FROM core.core_firm_platforms WHERE \"FirmId\" = ANY(@fids)", pg))
            {
                cmd.Parameters.AddWithValue("fids", seedFirmIds.ToArray());
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read()) seedPlatformIds.Add(rdr.GetGuid(0));
            }

            if (seedPlatformIds.Count > 0)
            {
                // channel_categories'in çocukları (groups/products) DB'de ON DELETE CASCADE;
                // storefront ile core arasında FK yok (modüller arası), o yüzden burada elle siliyoruz.
                PgExecInGuid("DELETE FROM storefront.channel_categories WHERE \"FirmPlatformId\" = ANY(@ids)", seedPlatformIds);
                PgExecInGuid("DELETE FROM storefront.channel_product_groups WHERE \"FirmPlatformId\" = ANY(@ids)", seedPlatformIds);
                PgExecInGuid("DELETE FROM storefront.channel_products WHERE \"FirmPlatformId\" = ANY(@ids)", seedPlatformIds);
                PgExecInGuid("DELETE FROM storefront.channel_variants WHERE \"FirmPlatformId\" = ANY(@ids)", seedPlatformIds);
                PgExecInGuid("DELETE FROM storefront.nav_menus WHERE \"FirmPlatformId\" = ANY(@ids)", seedPlatformIds);
            }

            PgExecInGuid("DELETE FROM core.core_firm_notification_settings WHERE \"FirmId\" = ANY(@ids)", seedFirmIds);
            PgExecInGuid("DELETE FROM core.core_cargo_rules WHERE \"FirmId\" = ANY(@ids)", seedFirmIds);
            PgExecInGuid("DELETE FROM core.core_firm_integrations WHERE \"FirmId\" = ANY(@ids)", seedFirmIds);
            PgExecInGuid("DELETE FROM core.core_firm_platforms WHERE \"FirmId\" = ANY(@ids)", seedFirmIds);
            PgExecInGuid("DELETE FROM core.core_firms WHERE \"Id\" = ANY(@ids)", seedFirmIds);
            Log($"  ✓ {seedFirmIds.Count} seed/demo firma + bağlı kayıtlar temizlendi.");
        }

        // ── 2) Gerçek Firma + FirmPlatform upsert (Code'a göre) ──
        Guid misarogluId = UpsertFirm("misaroglu",
            "MİŞAROĞLU TEKSTİL PAZ. DAĞITIM SAN. ve TİC. LTD. ŞTİ.",
            "6221822519", "Gaziler VD", "Ulugazi Mah. İstiklal Cad. No: 2, Samsun");
        Guid eldiId = UpsertFirm("eldi",
            "ELDİ TEKSTİL SAN. VE TİC A.Ş.",
            "3311476170", "Gaziler VD", "Kurtuluş Mah. Baki Esen Cad. No: 44B/B, Samsun");

        Guid siteTypeId = PgScalar<Guid>("SELECT \"Id\" FROM core.core_platform_types WHERE \"Code\" = 'site'");

        Guid tozluId  = UpsertFirmPlatform("tozlu", misarogluId, siteTypeId, "Tozlu", "tozlu.com");
        Guid juludeId = UpsertFirmPlatform("julude", eldiId, siteTypeId, "Julude", "julude.com");
        Guid misharId = UpsertFirmPlatform("mishar", misarogluId, siteTypeId, "Mishar", "misharitalia.com");
        Log("  ✓ 2 firma + 3 site (Tozlu/Julude/Mishar) upsert edildi.");

        // ── 3) plurunler → ChannelProduct + ChannelVariant ──
        foreach (var (legacyPlatformId, firmPlatformId) in new[]
                 { (1, tozluId), (2, juludeId), (41, misharId) })
            MigrateChannelDataForPlatform(legacyPlatformId, firmPlatformId);

        return Task.CompletedTask;
    }

    // ── Faz 15: ChannelCategory ağacı (cinsiyet + ürün grubu kesişimine göre, FillType=filter) ──
    // Slug'a göre upsert — tekrar çalıştırılabilir. Sayılar hardcode edilmiyor; sadece
    // hangi (kök, cinsiyet[], ürün grubu) kombinasyonlarının anlamlı bir kategori oluşturacak
    // kadar ürünü olduğu (>=10) elle seçilip buraya yazıldı (bkz. docs/grup_eslesme.md +
    // canlı DB'den çekilen cinsiyet×ürün grubu sayıları).
    static Task Phase15_SeedChannelCategories(Guid firmPlatformId)
    {
        Log($"FAZ 15: ChannelCategory ağacı kuruluyor (FirmPlatformId={firmPlatformId})...");

        var kadin      = new[] { "Kadın" };
        var erkek      = new[] { "Erkek" };
        var cocukBebek = new[] { "Kız çocuk", "Erkek çocuk", "Çocuk", "Kız Bebek", "Erkek Bebek", "Bebek" };

        var roots = new[]
        {
            ("kadin", "Kadın", kadin, new (string Slug, string Name, string GroupCode)[]
            {
                ("kadin-pantolon", "Pantolon", "grp_3"),
                ("kadin-elbise", "Elbise", "grp_1"),
                ("kadin-ic-giyim", "İç Giyim", "grp_118"),
                ("kadin-bluz", "Bluz", "grp_6"),
                ("kadin-aksesuar", "Aksesuar", "grp_2"),
                ("kadin-canta", "Çanta", "grp_83"),
                ("kadin-triko", "Triko", "grp_14"),
                ("kadin-tunik", "Tunik", "grp_18"),
                ("kadin-tshirt", "T-Shirt", "grp_7"),
                ("kadin-gomlek", "Gömlek", "grp_5"),
                ("kadin-sweatshirt", "Sweatshirt", "grp_11"),
                ("kadin-bot", "Bot", "grp_21"),
                ("kadin-etek", "Etek", "grp_10"),
                ("kadin-pijama", "Pijama", "grp_15"),
                ("kadin-sandalet", "Sandalet", "grp_27"),
                ("kadin-babet", "Babet", "grp_25"),
                ("kadin-hirka", "Hırka", "grp_12"),
                ("kadin-yelek", "Yelek", "grp_17"),
                ("kadin-bustiyer", "Bustiyer", "grp_9"),
                ("kadin-plaj-giyim", "Plaj Giyim", "grp_123"),
                ("kadin-kap", "Kap", "grp_77"),
                ("kadin-cizme", "Çizme", "grp_24"),
                ("kadin-panco", "Panço", "grp_80"),
                ("kadin-bolero", "Bolero", "grp_16"),
                ("kadin-kimono", "Kimono", "grp_198"),
            }),
            ("erkek", "Erkek", erkek, new (string Slug, string Name, string GroupCode)[]
            {
                ("erkek-pantolon", "Pantolon", "grp_3"),
                ("erkek-mont", "Mont", "grp_73"),
                ("erkek-terlik", "Terlik", "grp_33"),
                ("erkek-aktif-spor", "Aktif Spor", "grp_70"),
            }),
            ("cocuk-bebek", "Çocuk & Bebek", cocukBebek, new (string Slug, string Name, string GroupCode)[]
            {
                ("cocuk-bebek-pantolon", "Pantolon", "grp_3"),
                ("cocuk-bebek-esofman", "Eşofman", "grp_47"),
                ("cocuk-bebek-ikili-takim", "İkili Takım", "grp_48"),
                ("cocuk-bebek-sort", "Şort", "grp_95"),
                ("cocuk-bebek-elbise", "Elbise", "grp_1"),
                ("cocuk-bebek-zibin", "Zıbın", "grp_159"),
                ("cocuk-bebek-body", "Body", "grp_44"),
                ("cocuk-bebek-ceket", "Ceket", "grp_46"),
            }),
            ("ev-yasam", "Ev & Yaşam", (string[]?)null, new (string Slug, string Name, string GroupCode)[]
            {
                ("ev-yasam-kisisel-bakim", "Kişisel Bakım", "grp_132"),
                ("ev-yasam-makyaj", "Makyaj Malzemeleri", "grp_137"),
                ("ev-yasam-ev-tekstil", "Ev Tekstil", "grp_183"),
                ("ev-yasam-telefon-aksesuar", "Telefon ve Aksesuarları", "grp_176"),
                ("ev-yasam-mutfak", "Mutfak Gereçleri", "grp_184"),
                ("ev-yasam-elektrikli-ev-aletleri", "Elektirikli Ev Aletleri", "grp_179"),
            }),
        };

        int rootSort = 0;
        foreach (var (slug, name, genders, children) in roots)
        {
            Guid?[] genderIds = genders?.Select(g => (Guid?)AttrValueId("cinsiyet", g)).ToArray()
                ?? Array.Empty<Guid?>();

            // Cinsiyet filtresi olmayan kökler (Ev & Yaşam) için, "her şeyi göster" olmasın diye
            // kök de alt kategorilerin ürün gruplarının birleşimiyle sınırlanır.
            Guid[]? rootGroupIds = genders is null
                ? children.Select(c => ProductGroupId(c.GroupCode)).Distinct().ToArray()
                : null;

            Guid rootId = UpsertChannelCategory(firmPlatformId, null, slug, name, rootSort++,
                rootGroupIds, genderIds.Where(g => g.HasValue).Select(g => g!.Value).ToArray());

            int childSort = 0;
            foreach (var (cSlug, cName, cGroupCode) in children)
            {
                UpsertChannelCategory(firmPlatformId, rootId, cSlug, cName, childSort++,
                    new[] { ProductGroupId(cGroupCode) },
                    genderIds.Where(g => g.HasValue).Select(g => g!.Value).ToArray());
            }
            Log($"  ✓ {name}: kök + {children.Length} alt kategori.");
        }

        Log("FAZ 15 tamamlandı.");
        return Task.CompletedTask;
    }

    static Guid ProductGroupId(string code)
    {
        var r = PgScalarNullable("SELECT \"Id\" FROM definition.product_groups WHERE \"Code\"=@c", ("c", code));
        return r is Guid g ? g : throw new Exception($"ProductGroup kod bulunamadı: {code}");
    }

    static Guid AttrTypeId(string code)
    {
        var r = PgScalarNullable("SELECT \"Id\" FROM definition.attribute_types WHERE \"Code\"=@c", ("c", code));
        return r is Guid g ? g : throw new Exception($"AttributeType kod bulunamadı: {code}");
    }

    static Guid AttrValueId(string typeCode, string nameTr)
    {
        var r = PgScalarNullable(
            "SELECT v.\"Id\" FROM definition.attribute_values v JOIN definition.attribute_types t ON t.\"Id\"=v.\"AttributeTypeId\" WHERE t.\"Code\"=@tc AND v.\"NameI18n\"->>'tr'=@n",
            ("tc", typeCode), ("n", nameTr));
        return r is Guid g ? g : throw new Exception($"AttributeValue bulunamadı: {typeCode}/{nameTr}");
    }

    static Guid UpsertChannelCategory(
        Guid firmPlatformId, Guid? parentId, string slug, string nameTr, int sortOrder,
        Guid[]? groupIds, Guid[]? genderValueIds)
    {
        var filterDef = new Dictionary<string, object>();
        if (groupIds is { Length: > 0 }) filterDef["productGroupIds"] = groupIds;
        if (genderValueIds is { Length: > 0 })
            filterDef["attributeFilters"] = new object[]
            {
                new Dictionary<string, object> { ["attributeTypeId"] = AttrTypeId("cinsiyet"), ["valueIds"] = genderValueIds }
            };
        var filterDefJson = JsonSerializer.Serialize(filterDef, JsonOpts);

        var existing = PgScalarNullable(
            "SELECT \"Id\" FROM storefront.channel_categories WHERE \"FirmPlatformId\"=@fpid AND \"Slug\"=@slug",
            ("fpid", firmPlatformId), ("slug", slug));

        if (existing is Guid gid)
        {
            PgExec(@"UPDATE storefront.channel_categories SET
                    ""ParentId""=@pid, ""NameI18n""=@name::jsonb, ""Status""='published',
                    ""FillType""='filter', ""FilterDef""=@fdef::jsonb, ""SortOrder""=@sort, ""UpdatedAt""=@now
                WHERE ""Id""=@id",
                ("pid", (object?)parentId ?? DBNull.Value), ("name", I18n(nameTr)),
                ("fdef", filterDefJson), ("sort", sortOrder), ("now", Now), ("id", gid));
            return gid;
        }

        var id = NewId();
        PgExec(@"INSERT INTO storefront.channel_categories
                (""Id"",""FirmPlatformId"",""ParentId"",""NameI18n"",""Slug"",""Status"",""FillType"",""FilterDef"",""SortOrder"",""CreatedAt"",""IsDeleted"")
                VALUES (@id,@fpid,@pid,@name::jsonb,@slug,'published','filter',@fdef::jsonb,@sort,@now,FALSE)",
            ("id", id), ("fpid", firmPlatformId), ("pid", (object?)parentId ?? DBNull.Value),
            ("name", I18n(nameTr)), ("slug", slug), ("fdef", filterDefJson), ("sort", sortOrder), ("now", Now));
        return id;
    }

    static void PgExecInGuid(string sqlWithIdsParam, List<Guid> ids)
    {
        if (ids.Count == 0) return;
        using var cmd = new NpgsqlCommand(sqlWithIdsParam, pg) { CommandTimeout = 120 };
        cmd.Parameters.AddWithValue("ids", ids.ToArray());
        cmd.ExecuteNonQuery();
    }

    static Guid UpsertFirm(string code, string legalName, string taxNumber, string taxOffice, string address)
    {
        var existing = PgScalarNullable("SELECT \"Id\" FROM core.core_firms WHERE \"Code\" = @c", ("c", code));
        if (existing is Guid gid)
        {
            PgExec(@"UPDATE core.core_firms SET
                    ""NameI18n""=@name::jsonb, ""TaxNumber""=@tn, ""TaxOffice""=@to,
                    ""Address""=@addr, ""IsActive""=TRUE, ""UpdatedAt""=@now
                WHERE ""Id""=@id",
                ("name", I18n(legalName)), ("tn", taxNumber), ("to", taxOffice),
                ("addr", address), ("now", Now), ("id", gid));
            return gid;
        }

        var id = NewId();
        PgExec(@"INSERT INTO core.core_firms
                (""Id"",""Code"",""NameI18n"",""TaxOffice"",""TaxNumber"",""Address"",""Phone"",""Email"",""IsMain"",""IsActive"",""CreatedAt"",""IsDeleted"")
                VALUES (@id,@code,@name::jsonb,@to,@tn,@addr,'','',FALSE,TRUE,@now,FALSE)",
            ("id", id), ("code", code), ("name", I18n(legalName)),
            ("to", taxOffice), ("tn", taxNumber), ("addr", address), ("now", Now));
        return id;
    }

    static Guid UpsertFirmPlatform(string code, Guid firmId, Guid platformTypeId, string displayNameTr, string domain)
    {
        var settings = JsonSerializer.Serialize(new Dictionary<string, object> { ["domain"] = domain }, JsonOpts);

        var existing = PgScalarNullable("SELECT \"Id\" FROM core.core_firm_platforms WHERE \"Code\" = @c", ("c", code));
        if (existing is Guid gid)
        {
            PgExec(@"UPDATE core.core_firm_platforms SET
                    ""FirmId""=@fid, ""PlatformTypeId""=@ptid, ""NameI18n""=@name::jsonb,
                    ""Settings""=@settings::jsonb, ""IsActive""=TRUE, ""UpdatedAt""=@now
                WHERE ""Id""=@id",
                ("fid", firmId), ("ptid", platformTypeId), ("name", I18n(displayNameTr)),
                ("settings", settings), ("now", Now), ("id", gid));
            return gid;
        }

        var id = NewId();
        PgExec(@"INSERT INTO core.core_firm_platforms
                (""Id"",""FirmId"",""PlatformTypeId"",""Code"",""NameI18n"",""Credentials"",""Settings"",""IsActive"",""CreatedAt"",""IsDeleted"")
                VALUES (@id,@fid,@ptid,@code,@name::jsonb,'{}'::jsonb,@settings::jsonb,TRUE,@now,FALSE)",
            ("id", id), ("fid", firmId), ("ptid", platformTypeId), ("code", code),
            ("name", I18n(displayNameTr)), ("settings", settings), ("now", Now));
        return id;
    }

    // plurunler (platformId=X) → ChannelProduct (varlık/aktivasyon) + ChannelVariant (fiyat + satista bayrağı).
    // ChannelProduct: bu platformda plurunler'de en az bir satırı olan her ürün için oluşturulur (IsActive=true —
    //   "kanala atanmış" anlamında). ChannelVariant.IsActive ise satista bayrağını taşır (asıl satılabilirlik).
    // satisFiyati/listeFiyati 0 ise "fiyat girilmemiş" sayılır → null (ürünün BasePrice'ına düşer).
    static void MigrateChannelDataForPlatform(int legacyPlatformId, Guid firmPlatformId)
    {
        Log($"  Platform {legacyPlatformId} → ürün/varyant fiyat+durum aktarımı...");

        // Reload sonrası katalog GUID'leri değiştiğinden bu platformun MEVCUT channel_variants/
        // channel_products'ı YETİM kalır (eski VariantId/ProductId'lere bağlı). Upsert yeni
        // GUID'leri ekler ama eskiyi TEMİZLEMEZ → önce bu platformun kaydını sil, sonra yeniden
        // kur. (Bilinen kayıp: admin'in channel_products'ta ayarladığı Featured/NameI18n
        // override'ları — zaten ProductId GUID'i değiştiği için kaçınılmaz.)
        PgExec("DELETE FROM storefront.channel_variants WHERE \"FirmPlatformId\" = @fp", ("fp", firmPlatformId));
        PgExec("DELETE FROM storefront.channel_products WHERE \"FirmPlatformId\" = @fp", ("fp", firmPlatformId));

        var seenProducts = new HashSet<Guid>();
        var variantBatch = new List<(Guid variantId, decimal? price, decimal? compareAt, bool isActive)>();
        int matched = 0, skipped = 0;

        using (var r = MysqlQuery($@"SELECT urunId, urunAnaVaryantId, satisFiyati, listeFiyati, satista
            FROM plurunler WHERE platformId = {legacyPlatformId}"))
        {
            while (r.Read())
            {
                int urunId = r.GetInt32(0);
                int urunAnaVaryantId = r.GetInt32(1);

                if (!productMap.TryGetValue(urunId, out var productGuid)
                    || !variantMap.TryGetValue(urunAnaVaryantId, out var variantGuid))
                {
                    skipped++;
                    continue;
                }

                decimal satisFiyati = r.IsDBNull(2) ? 0 : (decimal)r.GetDouble(2);
                decimal listeFiyati = r.IsDBNull(3) ? 0 : (decimal)r.GetDouble(3);
                bool satista = !r.IsDBNull(4) && Convert.ToBoolean(r.GetValue(4));

                decimal? price = satisFiyati > 0 ? satisFiyati : null;
                decimal? compareAt = listeFiyati > 0 && listeFiyati != satisFiyati ? listeFiyati : null;

                seenProducts.Add(productGuid);
                variantBatch.Add((variantGuid, price, compareAt, satista));
                matched++;

                if (variantBatch.Count >= 500)
                {
                    FlushChannelVariants(firmPlatformId, variantBatch);
                    variantBatch.Clear();
                }
                if (matched % 50000 == 0) Log($"    {matched} eşleşen satır aktarıldı...");
            }
        }
        FlushChannelVariants(firmPlatformId, variantBatch);

        var productBatch = seenProducts.Select(pid => (pid, true)).ToList();
        for (int i = 0; i < productBatch.Count; i += 500)
            FlushChannelProducts(firmPlatformId, productBatch.Skip(i).Take(500).ToList());

        Log($"  ✓ Platform {legacyPlatformId}: {matched} ChannelVariant, {seenProducts.Count} ChannelProduct ({skipped} satır atlandı — eşleşen ürün/varyant yok).");
    }

    static void FlushChannelVariants(Guid firmPlatformId,
        List<(Guid variantId, decimal? price, decimal? compareAt, bool isActive)> batch)
    {
        if (batch.Count == 0) return;
        var sb = new StringBuilder();
        sb.Append(@"INSERT INTO storefront.channel_variants
            (""Id"",""FirmPlatformId"",""VariantId"",""PriceType"",""Price"",""CompareAtPrice"",""IsActive"",""CreatedAt"",""IsDeleted"") VALUES ");
        using var cmd = new NpgsqlCommand { Connection = pg, CommandTimeout = 120 };
        int p = 0;
        for (int i = 0; i < batch.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var (vid, price, cmp, active) = batch[i];
            string pid = "p" + p++, pvid = "p" + p++, pprice = "p" + p++, pcmp = "p" + p++, pact = "p" + p++, ptype = "p" + p++, pnow = "p" + p++;
            sb.Append($"(@{pid},@fpid,@{pvid},@{ptype},@{pprice},@{pcmp},@{pact},@{pnow},FALSE)");
            cmd.Parameters.AddWithValue(pid, NewId());
            cmd.Parameters.AddWithValue(pvid, vid);
            cmd.Parameters.AddWithValue(ptype, price.HasValue ? "manual" : (object)DBNull.Value);
            cmd.Parameters.AddWithValue(pprice, (object?)price ?? DBNull.Value);
            cmd.Parameters.AddWithValue(pcmp, (object?)cmp ?? DBNull.Value);
            cmd.Parameters.AddWithValue(pact, active);
            cmd.Parameters.AddWithValue(pnow, Now);
        }
        cmd.Parameters.AddWithValue("fpid", firmPlatformId);
        sb.Append(@" ON CONFLICT (""FirmPlatformId"",""VariantId"") DO UPDATE SET
            ""PriceType""=EXCLUDED.""PriceType"", ""Price""=EXCLUDED.""Price"",
            ""CompareAtPrice""=EXCLUDED.""CompareAtPrice"", ""IsActive""=EXCLUDED.""IsActive"",
            ""UpdatedAt""=EXCLUDED.""CreatedAt""");
        cmd.CommandText = sb.ToString();
        cmd.ExecuteNonQuery();
    }

    static void FlushChannelProducts(Guid firmPlatformId, List<(Guid productId, bool isActive)> batch)
    {
        if (batch.Count == 0) return;
        var sb = new StringBuilder();
        sb.Append(@"INSERT INTO storefront.channel_products
            (""Id"",""FirmPlatformId"",""ProductId"",""IsActive"",""SortOrder"",""CreatedAt"",""IsDeleted"") VALUES ");
        using var cmd = new NpgsqlCommand { Connection = pg, CommandTimeout = 120 };
        int p = 0;
        for (int i = 0; i < batch.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var (pid, active) = batch[i];
            string pidp = "p" + p++, ppid = "p" + p++, pact = "p" + p++, pnow = "p" + p++;
            sb.Append($"(@{pidp},@fpid,@{ppid},@{pact},0,@{pnow},FALSE)");
            cmd.Parameters.AddWithValue(pidp, NewId());
            cmd.Parameters.AddWithValue(ppid, pid);
            cmd.Parameters.AddWithValue(pact, active);
            cmd.Parameters.AddWithValue(pnow, Now);
        }
        cmd.Parameters.AddWithValue("fpid", firmPlatformId);
        sb.Append(@" ON CONFLICT (""FirmPlatformId"",""ProductId"") DO UPDATE SET
            ""IsActive""=EXCLUDED.""IsActive"", ""UpdatedAt""=EXCLUDED.""CreatedAt""");
        cmd.CommandText = sb.ToString();
        cmd.ExecuteNonQuery();
    }

    // ─── FAZ 17: KANAL SEÇİMİ (K2) + DURDURMA (K3) DEĞER AKTARIMI (satış görünürlüğü M2/M3) ──
    // Hedefli/reload'suz. Legacy plurunler (per-varyant satır) → ürün düzeyine indirgenir:
    //   K2 IsActive     = MAX(satista)   — ürünün EN AZ BİR varyantı satıştaysa kanalda (yelpaze).
    //   K3 durdurma      = MAX(yayinda)=0 → SaleStoppedFrom=now (süresiz); aksi halde durdurma yok.
    // Mevcut channel_products satırını UPSERT eder (Phase14 satırları korunur; Featured alanlarına
    // dokunulmaz). Kolon eşlemesi kullanıcı kararı (2026-07-15): satisaAcik YOK → satista=K2, yayinda=K3.
    static void Phase17_ChannelSaleFlags()
    {
        Log("FAZ 17: Kanal seçimi (K2=satista) + durdurma (K3=yayinda) değer aktarımı (M2/M3)...");
        EnsureProductMap();

        var tozluId  = PgScalar<Guid>("SELECT \"Id\" FROM core.core_firm_platforms WHERE \"Code\"='tozlu'");
        var juludeId = PgScalar<Guid>("SELECT \"Id\" FROM core.core_firm_platforms WHERE \"Code\"='julude'");
        var misharId = PgScalar<Guid>("SELECT \"Id\" FROM core.core_firm_platforms WHERE \"Code\"='mishar'");

        foreach (var (legacyPlatformId, firmPlatformId) in new[] { (1, tozluId), (2, juludeId), (41, misharId) })
            MigrateChannelSaleFlagsForPlatform(legacyPlatformId, firmPlatformId);

        PgExec("ANALYZE storefront.channel_products");
        Log("FAZ 17 tamam.");
    }

    static void MigrateChannelSaleFlagsForPlatform(int legacyPlatformId, Guid firmPlatformId)
    {
        Log($"  Platform {legacyPlatformId} → kanal seçimi/durdurma aktarımı...");
        var now = Now;
        var batch = new List<(Guid productId, bool isActive, DateTime? stoppedFrom)>();
        int matched = 0, skipped = 0, cikarilan = 0, durdurulan = 0;

        using (var r = MysqlQuery($@"SELECT urunId, MAX(satista+0) AS sat, MAX(yayinda) AS yay
            FROM plurunler WHERE platformId = {legacyPlatformId} AND urunId IS NOT NULL GROUP BY urunId"))
        {
            while (r.Read())
            {
                int urunId = r.GetInt32(0);
                if (!productMap.TryGetValue(urunId, out var productGuid)) { skipped++; continue; }

                bool isActive = Convert.ToInt32(r.GetValue(1)) >= 1;   // K2: en az bir varyant satışta
                int yayMax = r.IsDBNull(2) ? 0 : Convert.ToInt32(r.GetValue(2));
                DateTime? stoppedFrom = yayMax == 0 ? now : null;      // K3: hiç yayında değilse durdur

                if (!isActive) cikarilan++;
                if (stoppedFrom.HasValue) durdurulan++;

                batch.Add((productGuid, isActive, stoppedFrom));
                matched++;

                if (batch.Count >= 500) { FlushChannelSaleFlags(firmPlatformId, batch, now); batch.Clear(); }
            }
        }
        FlushChannelSaleFlags(firmPlatformId, batch, now);
        Log($"  ✓ Platform {legacyPlatformId}: {matched} ürün ({cikarilan} kanaldan çıkarıldı, {durdurulan} durduruldu; {skipped} eşleşmeyen atlandı).");
    }

    // ─── FAZ 18: LEGACY ÜRÜN URL'LERİ → ChannelVariant.Slug (per-platform, per-varyant) ──────
    // Eski sitenin gerçek URL'leri (plurunler.urunUrl, slug biçimi) yeni sitede çalışsın diye
    // her (platform × varyant) çiftinin URL'i channel_variants.Slug'a taşınır. Hedefli/reload'suz/
    // idempotent. urunAnaVaryantId = legacy varyant id (apurunvaryantlari.Id) → variantMap ile GUID.
    static void Phase18_ChannelVariantUrls()
    {
        Log("FAZ 18: Legacy ürün URL'leri → channel_variants.Slug (per-platform/varyant)...");
        EnsureVariantMap();

        var tozluId  = PgScalar<Guid>("SELECT \"Id\" FROM core.core_firm_platforms WHERE \"Code\"='tozlu'");
        var juludeId = PgScalar<Guid>("SELECT \"Id\" FROM core.core_firm_platforms WHERE \"Code\"='julude'");
        var misharId = PgScalar<Guid>("SELECT \"Id\" FROM core.core_firm_platforms WHERE \"Code\"='mishar'");

        foreach (var (legacyPlatformId, firmPlatformId) in new[] { (1, tozluId), (2, juludeId), (41, misharId) })
            MigrateChannelVariantUrlsForPlatform(legacyPlatformId, firmPlatformId);

        PgExec("ANALYZE storefront.channel_variants");
        Log("FAZ 18 tamam.");
    }

    static void MigrateChannelVariantUrlsForPlatform(int legacyPlatformId, Guid firmPlatformId)
    {
        Log($"  Platform {legacyPlatformId} → varyant URL aktarımı...");
        var batch = new List<(Guid variantId, string slug)>();
        var seenVariant = new HashSet<Guid>();   // aynı platformda ilk gelen kazanır (savunma)
        int matched = 0, skipped = 0, dup = 0;

        using (var r = MysqlQuery($@"SELECT urunAnaVaryantId, urunUrl FROM plurunler
            WHERE platformId = {legacyPlatformId} AND urunUrl IS NOT NULL AND urunUrl <> ''"))
        {
            while (r.Read())
            {
                int lvid = r.GetInt32(0);
                string url = r.GetString(1).Trim();
                if (url.Length == 0) { skipped++; continue; }
                if (!variantMap.TryGetValue(lvid, out var variantGuid)) { skipped++; continue; }
                if (!seenVariant.Add(variantGuid)) { dup++; continue; }

                batch.Add((variantGuid, url.Length > 255 ? url[..255] : url));
                matched++;
                if (batch.Count >= 500) { FlushChannelVariantSlugs(firmPlatformId, batch); batch.Clear(); }
            }
        }
        FlushChannelVariantSlugs(firmPlatformId, batch);
        Log($"  ✓ Platform {legacyPlatformId}: {matched} varyant slug'landı ({skipped} eşleşmeyen atlandı, {dup} yinelenen varyant atlandı).");
    }

    static void FlushChannelVariantSlugs(Guid firmPlatformId, List<(Guid variantId, string slug)> batch)
    {
        if (batch.Count == 0) return;
        var sb = new StringBuilder();
        sb.Append(@"INSERT INTO storefront.channel_variants
            (""Id"",""FirmPlatformId"",""VariantId"",""Slug"",""IsActive"",""CreatedAt"",""IsDeleted"") VALUES ");
        using var cmd = new NpgsqlCommand { Connection = pg, CommandTimeout = 120 };
        int p = 0;
        var now = Now;
        for (int i = 0; i < batch.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var (vid, slug) = batch[i];
            string pvid = "p" + p++, pslug = "p" + p++, pid = "p" + p++, pnow = "p" + p++;
            sb.Append($"(@{pid},@fpid,@{pvid},@{pslug},TRUE,@{pnow},FALSE)");
            cmd.Parameters.AddWithValue(pid, NewId());
            cmd.Parameters.AddWithValue(pvid, vid);
            cmd.Parameters.AddWithValue(pslug, slug);
            cmd.Parameters.AddWithValue(pnow, now);
        }
        cmd.Parameters.AddWithValue("fpid", firmPlatformId);
        // Mevcut ChannelVariant satırını (Phase14 fiyat/durum) KORU, yalnız Slug'ı yaz.
        sb.Append(@" ON CONFLICT (""FirmPlatformId"",""VariantId"") DO UPDATE SET
            ""Slug""=EXCLUDED.""Slug"", ""UpdatedAt""=EXCLUDED.""CreatedAt""");
        cmd.CommandText = sb.ToString();
        cmd.ExecuteNonQuery();
    }

    static void FlushChannelSaleFlags(Guid firmPlatformId,
        List<(Guid productId, bool isActive, DateTime? stoppedFrom)> batch, DateTime now)
    {
        if (batch.Count == 0) return;
        var sb = new StringBuilder();
        sb.Append(@"INSERT INTO storefront.channel_products
            (""Id"",""FirmPlatformId"",""ProductId"",""IsActive"",""SaleStoppedFrom"",""SaleStoppedUntil"",""SortOrder"",""CreatedAt"",""IsDeleted"") VALUES ");
        using var cmd = new NpgsqlCommand { Connection = pg, CommandTimeout = 120 };
        int p = 0;
        for (int i = 0; i < batch.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var (pid, active, sfrom) = batch[i];
            string pidp = "p" + p++, ppid = "p" + p++, pact = "p" + p++, psf = "p" + p++, pnow = "p" + p++;
            sb.Append($"(@{pidp},@fpid,@{ppid},@{pact},@{psf},NULL,0,@{pnow},FALSE)");
            cmd.Parameters.AddWithValue(pidp, NewId());
            cmd.Parameters.AddWithValue(ppid, pid);
            cmd.Parameters.AddWithValue(pact, active);
            cmd.Parameters.AddWithValue(psf, (object?)sfrom ?? DBNull.Value);
            cmd.Parameters.AddWithValue(pnow, now);
        }
        cmd.Parameters.AddWithValue("fpid", firmPlatformId);
        // Mevcut satırı güncelle (Featured/Name alanlarına dokunma). Durdurma bitişi bu aktarımda
        // her zaman NULL (süresiz veya durdurma yok) — panelden tarih penceresi kurulur.
        sb.Append(@" ON CONFLICT (""FirmPlatformId"",""ProductId"") DO UPDATE SET
            ""IsActive""=EXCLUDED.""IsActive"", ""SaleStoppedFrom""=EXCLUDED.""SaleStoppedFrom"",
            ""SaleStoppedUntil""=EXCLUDED.""SaleStoppedUntil"", ""UpdatedAt""=EXCLUDED.""CreatedAt""");
        cmd.CommandText = sb.ToString();
        cmd.ExecuteNonQuery();
    }

    // ============================================================================
    // FAZ 22-24: MISHAR go-live aktarımı (2026-07-23) — üye/adres/sipariş/favori.
    // Yalnız legacy platformId=41 (Mishar). Tekrar çalıştırılabilir (LegacyMemberId/
    // LegacyOrderId upsert; adres/favori önce sil-sonra-ekle). Modüller arası FK yok →
    // eşleşmeyen varyant/geo Guid.Empty + snapshot alanlarıyla korunur.
    // ============================================================================
    const int MISHAR_PLATFORM = 41;
    static readonly System.Globalization.CultureInfo TR = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
    static readonly Dictionary<int, Guid> memberMap = new(); // legacy webmembers.Id -> crm_members.Id
    static Guid geoCountryId = Guid.Empty;
    static readonly Dictionary<string, Guid> cityByKey = new();
    static readonly Dictionary<string, Guid> districtByKey = new(); // "cityGuid|TRKEY(ad)"

    static string Sm(MySqlDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetValue(i).ToString()!.Trim();
    static bool Bit(MySqlDataReader r, int i)
    {
        if (r.IsDBNull(i)) return false;
        var v = r.GetValue(i);
        return v is bool b ? b : Convert.ToInt64(v) != 0;
    }
    static DateTime? Dt(MySqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetDateTime(i);
    static string TrKey(string? s) => (s ?? "").Trim().ToUpper(TR);

    static Guid MisharFp() => PgScalar<Guid>("SELECT \"Id\" FROM core.core_firm_platforms WHERE \"Code\"='mishar'");

    static void EnsureGeoMaps()
    {
        if (cityByKey.Count > 0) return;
        geoCountryId = (Guid?)PgScalarNullable("SELECT \"Id\" FROM crm.crm_countries ORDER BY \"CreatedAt\" LIMIT 1") ?? Guid.Empty;
        using (var r = new NpgsqlCommand("SELECT \"Id\", \"NameI18n\"->>'tr' FROM crm.crm_cities", pg).ExecuteReader())
            while (r.Read()) if (!r.IsDBNull(1)) cityByKey[TrKey(r.GetString(1))] = r.GetGuid(0);
        using (var r = new NpgsqlCommand("SELECT \"CityId\", \"Id\", \"NameI18n\"->>'tr' FROM crm.crm_districts", pg).ExecuteReader())
            while (r.Read()) if (!r.IsDBNull(2)) districtByKey[r.GetGuid(0) + "|" + TrKey(r.GetString(2))] = r.GetGuid(1);
        Log($"  [geo: {cityByKey.Count} il, {districtByKey.Count} ilçe, ülke={(geoCountryId==Guid.Empty?"YOK":"var")}]");
    }
    static Guid? CityIdOrNull(string? name) => !string.IsNullOrWhiteSpace(name) && cityByKey.TryGetValue(TrKey(name), out var g) ? g : null;
    static Guid DistrictIdOrEmpty(Guid cityId, string? name) => cityId != Guid.Empty && !string.IsNullOrWhiteSpace(name) && districtByKey.TryGetValue(cityId + "|" + TrKey(name), out var g) ? g : Guid.Empty;

    static void EnsureMemberMap()
    {
        if (memberMap.Count > 0) return;
        using var r = new NpgsqlCommand("SELECT \"LegacyMemberId\", \"Id\" FROM crm.crm_members WHERE \"LegacyMemberId\" IS NOT NULL", pg).ExecuteReader();
        while (r.Read()) memberMap[r.GetInt32(0)] = r.GetGuid(1);
    }

    static async Task Phase22_MembersAndAddresses()
    {
        Log("=== FAZ 22: Mishar üye + adres aktarımı ===");
        await Task.CompletedTask;
        EnsureGeoMaps();
        var defaultGroup = PgScalar<Guid>("SELECT \"Id\" FROM crm.crm_member_groups WHERE \"IsDefault\"=true AND \"IsDeleted\"=false LIMIT 1");

        // 1) Üyeler (tek reader → önce List'e)
        var rows = new List<object[]>();
        using (var r = MysqlQuery(
            "SELECT Id, tcKimlikNo, firstName, lastName, phone, email, password, birthDate, gender, cityName, " +
            "isActive, epostaOnayli, telefonOnayli, emailSubscribed, smsSubscribed, createdDate " +
            "FROM webmembers WHERE platformId=" + MISHAR_PLATFORM))
        {
            while (r.Read())
                rows.Add(new object[] {
                    r.GetInt32(0), Sm(r,1), Sm(r,2), Sm(r,3), Sm(r,4), Sm(r,5), Sm(r,6),
                    Dt(r,7)!, Sm(r,8), Sm(r,9), Bit(r,10), Bit(r,11), Bit(r,12), Bit(r,13), Bit(r,14), Dt(r,15)!
                });
        }
        Log($"  {rows.Count} üye okundu.");

        int ins = 0, upd = 0;
        foreach (var row in rows)
        {
            int legacyId = (int)row[0];
            string tc = (string)row[1], fn = (string)row[2], ln = (string)row[3], phoneRaw = (string)row[4], emailRaw = (string)row[5], pwdRaw = (string)row[6];
            var birth = (DateTime?)row[7];
            string genderRaw = (string)row[8], city = (string)row[9];
            bool active = (bool)row[10], emailOk = (bool)row[11], phoneOk = (bool)row[12], emailSub = (bool)row[13], smsSub = (bool)row[14];
            var created = (DateTime?)row[15];

            // email/phone tekil kısıt — boş veya başka üyede kullanılıyorsa null
            string? email = emailRaw.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email)) email = null;
            else if (PgScalarNullable("SELECT 1 FROM crm.crm_members WHERE \"Email\"=@e AND \"LegacyMemberId\" IS DISTINCT FROM @lid", ("e", email), ("lid", legacyId)) != null) email = null;
            string? phone = string.IsNullOrWhiteSpace(phoneRaw) ? null : phoneRaw;
            if (phone != null && PgScalarNullable("SELECT 1 FROM crm.crm_members WHERE \"Phone\"=@p AND \"LegacyMemberId\" IS DISTINCT FROM @lid", ("p", phone), ("lid", legacyId)) != null) phone = null;

            string? pwd = (pwdRaw.Length == 32 && pwdRaw.All(Uri.IsHexDigit)) ? pwdRaw.ToUpperInvariant() : null; // MD5; junk/placeholder → null
            string? gender = TrKey(genderRaw) switch { "ERKEK" => "male", "KADIN" => "female", "KADİN" => "female", _ => (string?)null };
            object cityId = (object?)CityIdOrNull(city) ?? DBNull.Value;
            string? identity = string.IsNullOrWhiteSpace(tc) ? null : tc;
            string consents = JsonSerializer.Serialize(new Dictionary<string, object> { ["legacyMarketing"] = new Dictionary<string, bool> { ["email"] = emailSub, ["sms"] = smsSub } }, JsonOpts);
            var createdAt = created ?? Now;

            var existing = PgScalarNullable("SELECT \"Id\" FROM crm.crm_members WHERE \"LegacyMemberId\"=@lid", ("lid", legacyId));
            if (existing is Guid gid)
            {
                PgExec(@"UPDATE crm.crm_members SET ""Email""=@email,""Phone""=@phone,""PasswordHash""=@pwd,""FirstName""=@fn,""LastName""=@ln,
                    ""Gender""=@g,""BirthDate""=@bd,""CityId""=@city,""IdentityNumber""=@id,""Consents""=@cons::jsonb,
                    ""IsRegistered""=TRUE,""IsEmailVerified""=@eok,""IsPhoneVerified""=@pok,""IsActive""=@act,""UpdatedAt""=@now WHERE ""Id""=@mid",
                    ("email", (object?)email ?? DBNull.Value), ("phone", (object?)phone ?? DBNull.Value), ("pwd", (object?)pwd ?? DBNull.Value),
                    ("fn", fn), ("ln", ln), ("g", (object?)gender ?? DBNull.Value), ("bd", birth.HasValue ? DateOnly.FromDateTime(birth.Value) : (object)DBNull.Value),
                    ("city", cityId), ("id", (object?)identity ?? DBNull.Value), ("cons", consents), ("eok", emailOk), ("pok", phoneOk), ("act", active), ("now", Now), ("mid", gid));
                memberMap[legacyId] = gid; upd++;
            }
            else
            {
                var mid = NewId();
                PgExec(@"INSERT INTO crm.crm_members (""Id"",""MemberGroupId"",""LegacyMemberId"",""Email"",""Phone"",""PasswordHash"",""FirstName"",""LastName"",
                    ""Gender"",""BirthDate"",""CityId"",""IdentityNumber"",""Consents"",""IsRegistered"",""IsEmailVerified"",""IsPhoneVerified"",""IsActive"",""CreatedAt"",""IsDeleted"")
                    VALUES (@mid,@grp,@lid,@email,@phone,@pwd,@fn,@ln,@g,@bd,@city,@id,@cons::jsonb,TRUE,@eok,@pok,@act,@created,FALSE)",
                    ("mid", mid), ("grp", defaultGroup), ("lid", legacyId), ("email", (object?)email ?? DBNull.Value), ("phone", (object?)phone ?? DBNull.Value),
                    ("pwd", (object?)pwd ?? DBNull.Value), ("fn", fn), ("ln", ln), ("g", (object?)gender ?? DBNull.Value),
                    ("bd", birth.HasValue ? DateOnly.FromDateTime(birth.Value) : (object)DBNull.Value), ("city", cityId), ("id", (object?)identity ?? DBNull.Value),
                    ("cons", consents), ("eok", emailOk), ("pok", phoneOk), ("act", active), ("created", createdAt));
                memberMap[legacyId] = mid; ins++;
            }
        }
        Log($"  Üye: {ins} eklendi, {upd} güncellendi.");

        // 2) Adresler — aktarılan üyelerin adreslerini sil-sonra-ekle (idempotent)
        var addrRows = new List<object[]>();
        using (var r = MysqlQuery(
            "SELECT memberId, addressTitle, addressDetail, postalCode, neighborhoodName, districtName, cityName, countryName, contactFirstName, contactLastName, contactPhone " +
            "FROM webmemberaddresses WHERE platformId=" + MISHAR_PLATFORM))
        {
            while (r.Read())
                addrRows.Add(new object[] { r.GetInt32(0), Sm(r,1), Sm(r,2), Sm(r,3), Sm(r,4), Sm(r,5), Sm(r,6), Sm(r,7), Sm(r,8), Sm(r,9), Sm(r,10) });
        }
        foreach (var mid in memberMap.Values)
            PgExec("DELETE FROM crm.crm_addresses WHERE \"MemberId\"=@m", ("m", mid));

        var defaultDone = new HashSet<Guid>();
        int addrIns = 0;
        foreach (var a in addrRows)
        {
            int legacyMemberId = (int)a[0];
            if (!memberMap.TryGetValue(legacyMemberId, out var mid)) continue;
            string title = (string)a[1], detail = (string)a[2], postal = (string)a[3], mah = (string)a[4], ilce = (string)a[5], il = (string)a[6], ulke = (string)a[7], cfn = (string)a[8], cln = (string)a[9], phone = (string)a[10];
            var cityId = CityIdOrNull(il);
            var distId = cityId.HasValue ? DistrictIdOrEmpty(cityId.Value, ilce) : Guid.Empty;
            bool isDefault = defaultDone.Add(mid);
            PgExec(@"INSERT INTO crm.crm_addresses (""Id"",""MemberId"",""Title"",""CountryId"",""CountryName"",""CityId"",""CityName"",""DistrictId"",""DistrictName"",
                ""NeighborhoodName"",""AddressLine"",""PostalCode"",""RecipientName"",""RecipientPhone"",""IsDefault"",""IsValidated"",""CreatedAt"",""IsDeleted"")
                VALUES (@id,@mid,@title,@cid,@cname,@city,@cityn,@dist,@distn,@mah,@line,@postal,@rname,@rphone,@def,FALSE,@now,FALSE)",
                ("id", NewId()), ("mid", mid), ("title", string.IsNullOrWhiteSpace(title) ? "Adres" : title),
                ("cid", geoCountryId == Guid.Empty ? (object)DBNull.Value : geoCountryId), ("cname", string.IsNullOrWhiteSpace(ulke) ? "Türkiye" : ulke),
                ("city", (object?)cityId ?? DBNull.Value), ("cityn", il), ("dist", distId == Guid.Empty ? (object)DBNull.Value : distId), ("distn", ilce),
                ("mah", string.IsNullOrWhiteSpace(mah) ? (object)DBNull.Value : mah), ("line", detail), ("postal", string.IsNullOrWhiteSpace(postal) ? (object)DBNull.Value : postal),
                ("rname", ($"{cfn} {cln}").Trim()), ("rphone", phone), ("def", isDefault), ("now", Now));
            addrIns++;
        }
        Log($"  Adres: {addrIns} eklendi (aktarılan üyelere).");
    }

    static string MapOrderStatus(string legacy) => TrKey(legacy) switch
    {
        "İPTAL EDİLDİ" => "cancelled",
        "TESLİM EDİLDİ" => "delivered",
        "KARGOYA VERİLDİ" => "shipped",
        "TESLİM EDİLEMEDEN İADE GELDİ" => "cancelled",
        "FATURASI KESİLDİ" => "processing",
        "HAZIRLANIYOR" => "processing",
        _ => "confirmed",
    };

    static async Task Phase23_Orders()
    {
        Log("=== FAZ 23: Mishar sipariş aktarımı ===");
        await Task.CompletedTask;
        EnsureGeoMaps();
        EnsureMemberMap();
        EnsureVariantMap();
        var fp = MisharFp();

        // Teslimat adresi haritası (shippingAddressId -> adres)
        var addrMap = new Dictionary<int, string[]>();
        using (var r = MysqlQuery("SELECT Id, cityName, districtName, neighborhoodName, addressDetail, postalCode, contactFirstName, contactLastName, contactPhone FROM webmemberaddresses WHERE platformId=" + MISHAR_PLATFORM))
            while (r.Read()) addrMap[r.GetInt32(0)] = new[] { Sm(r,1), Sm(r,2), Sm(r,3), Sm(r,4), Sm(r,5), Sm(r,6), Sm(r,7), Sm(r,8) };

        // Siparişler
        var orders = new List<object[]>();
        using (var r = MysqlQuery(
            "SELECT Id, orderNumber, sourcePlatformOrderNumber, kaynakSiparisId, siparisKaynagi, orderStatus, orderDate, memberId, shippingAddressId, " +
            "memberFirstName, memberLastName, memberPhone, currency, exchangeRate, subTotal, discountTotal, expenseTotal, taxTotal, orderTotal, customerNote " +
            "FROM oporders WHERE platformId=" + MISHAR_PLATFORM))
        {
            while (r.Read())
                orders.Add(new object[] {
                    r.GetInt32(0), Sm(r,1), Sm(r,2), Sm(r,3), Sm(r,4), Sm(r,5), Dt(r,6)!, r.IsDBNull(7)?0:r.GetInt32(7), r.IsDBNull(8)?0:r.GetInt32(8),
                    Sm(r,9), Sm(r,10), Sm(r,11), Sm(r,12), r.IsDBNull(13)?0d:Convert.ToDouble(r.GetValue(13)),
                    r.IsDBNull(14)?0d:Convert.ToDouble(r.GetValue(14)), r.IsDBNull(15)?0d:Convert.ToDouble(r.GetValue(15)), r.IsDBNull(16)?0d:Convert.ToDouble(r.GetValue(16)),
                    r.IsDBNull(17)?0d:Convert.ToDouble(r.GetValue(17)), r.IsDBNull(18)?0d:Convert.ToDouble(r.GetValue(18)), Sm(r,19)
                });
        }
        Log($"  {orders.Count} sipariş okundu.");

        // Kalemler (tüm mishar sipariş kalemleri — orderId'ye göre grupla)
        var orderIds = orders.Select(o => (int)o[0]).ToHashSet();
        var linesByOrder = new Dictionary<int, List<object[]>>();
        using (var r = MysqlQuery(
            "SELECT ol.orderId, ol.productVariantId, ol.barcode, ol.productCode, ol.productName, ol.color, ol.variantValue, ol.sellingPrice, ol.quantity, ol.discountAmount " +
            "FROM oporderlines ol JOIN oporders o ON o.Id=ol.orderId WHERE o.platformId=" + MISHAR_PLATFORM))
        {
            while (r.Read())
            {
                int oid = r.GetInt32(0);
                if (!linesByOrder.TryGetValue(oid, out var list)) linesByOrder[oid] = list = new();
                list.Add(new object[] { r.IsDBNull(1)?0:r.GetInt32(1), Sm(r,2), Sm(r,3), Sm(r,4), Sm(r,5), Sm(r,6),
                    r.IsDBNull(7)?0d:Convert.ToDouble(r.GetValue(7)), r.IsDBNull(8)?0:r.GetInt32(8), r.IsDBNull(9)?0d:Convert.ToDouble(r.GetValue(9)) });
            }
        }

        int ins = 0, upd = 0, lineCount = 0, unresolvedVar = 0;
        foreach (var o in orders)
        {
            int legacyId = (int)o[0];
            string orderNo = (string)o[1], srcNo = (string)o[2], kaynakNo = (string)o[3], kaynak = (string)o[4], statusRaw = (string)o[5];
            var orderDate = (DateTime?)o[6];
            int legacyMemberId = (int)o[7], shipAddrId = (int)o[8];
            string mFn = (string)o[9], mLn = (string)o[10], mPhone = (string)o[11], currency = (string)o[12];
            double exRate = (double)o[13], sub = (double)o[14], disc = (double)o[15], exp = (double)o[16], tax = (double)o[17], total = (double)o[18];
            string custNote = (string)o[19];

            string status = MapOrderStatus(statusRaw);
            string paymentStatus = status == "cancelled" ? "cancelled" : "paid";
            object memberId = memberMap.TryGetValue(legacyMemberId, out var mg) ? mg : (object)DBNull.Value;
            string extNo = !string.IsNullOrWhiteSpace(kaynakNo) ? kaynakNo : (!string.IsNullOrWhiteSpace(srcNo) ? srcNo : orderNo);

            // teslimat adresi
            string il = "", ilce = "", mah = "", detay = "", postal = "", rName = ($"{mFn} {mLn}").Trim(), rPhone = mPhone;
            if (addrMap.TryGetValue(shipAddrId, out var ad))
            {
                il = ad[0]; ilce = ad[1]; mah = ad[2]; detay = ad[3]; postal = ad[4];
                var an = ($"{ad[5]} {ad[6]}").Trim(); if (an != "") rName = an;
                if (!string.IsNullOrWhiteSpace(ad[7])) rPhone = ad[7];
            }
            var cityId = CityIdOrNull(il) ?? Guid.Empty;
            var distId = DistrictIdOrEmpty(cityId, ilce);
            // Geo Guid çözülemese de metin kaybolmasın: adres satırına il/ilçe/mahalle önekle
            string addrLine = string.Join(" / ", new[] { il, ilce, mah, detay }.Where(x => !string.IsNullOrWhiteSpace(x)));
            string internalNote = $"[Aktarım] Kaynak: {(string.IsNullOrWhiteSpace(kaynak) ? "?" : kaynak)} | Eski No: {orderNo}" + (string.IsNullOrWhiteSpace(custNote) ? "" : $" | Müşteri notu: {custNote}");

            var existing = PgScalarNullable("SELECT \"Id\" FROM \"order\".ord_orders WHERE \"LegacyOrderId\"=@lid", ("lid", legacyId));
            Guid oid;
            if (existing is Guid g) { oid = g; PgExec("DELETE FROM \"order\".ord_order_items WHERE \"OrderId\"=@o", ("o", oid)); upd++; }
            else { oid = NewId(); ins++; }

            var pars = new (string, object?)[] {
                ("id", oid), ("lid", legacyId), ("no", orderNo), ("ext", extNo), ("fp", fp), ("mid", memberId),
                ("st", status), ("ps", paymentStatus), ("cur", string.IsNullOrWhiteSpace(currency) ? "TRY" : currency),
                ("ex", (decimal)(exRate <= 0 ? 1 : exRate)), ("rn", rName), ("rp", rPhone),
                ("cc", geoCountryId == Guid.Empty ? Guid.Empty : geoCountryId), ("ci", cityId), ("di", distId),
                ("al", addrLine == "" ? "-" : addrLine), ("sub", (decimal)sub), ("disc", (decimal)disc), ("exp", (decimal)exp),
                ("tax", (decimal)tax), ("gt", (decimal)total), ("inote", internalNote), ("created", (object?)orderDate ?? Now)
            };
            if (existing is Guid)
                PgExec(@"UPDATE ""order"".ord_orders SET ""OrderNumber""=@no,""OrderNumberSource""='external',""ExternalOrderNumber""=@ext,""FirmPlatformId""=@fp,""MemberId""=@mid,
                    ""Status""=@st,""PaymentStatus""=@ps,""OrderType""='retail',""CurrencyCode""=@cur,""InvoiceCurrencyCode""=@cur,""ExchangeRate""=@ex,
                    ""ShippingRecipientName""=@rn,""ShippingRecipientPhone""=@rp,""ShippingCountryId""=@cc,""ShippingCityId""=@ci,""ShippingDistrictId""=@di,""ShippingAddressLine""=@al,
                    ""BillingSameAsShipping""=TRUE,""Subtotal""=@sub,""TotalDiscount""=@disc,""TotalExpense""=@exp,""TotalTax""=@tax,""GrandTotal""=@gt,
                    ""InternalNotes""=@inote,""RequiresApproval""=FALSE,""ConfirmationRequired""=FALSE,""UpdatedAt""=@created WHERE ""Id""=@id", pars);
            else
                PgExec(@"INSERT INTO ""order"".ord_orders (""Id"",""LegacyOrderId"",""OrderNumber"",""OrderNumberSource"",""ExternalOrderNumber"",""FirmPlatformId"",""MemberId"",
                    ""Status"",""PaymentStatus"",""OrderType"",""CurrencyCode"",""InvoiceCurrencyCode"",""ExchangeRate"",
                    ""ShippingRecipientName"",""ShippingRecipientPhone"",""ShippingCountryId"",""ShippingCityId"",""ShippingDistrictId"",""ShippingAddressLine"",
                    ""BillingSameAsShipping"",""Subtotal"",""TotalDiscount"",""TotalExpense"",""TotalTax"",""GrandTotal"",""InternalNotes"",""RequiresApproval"",""ConfirmationRequired"",""CreatedAt"",""IsDeleted"")
                    VALUES (@id,@lid,@no,'external',@ext,@fp,@mid,@st,@ps,'retail',@cur,@cur,@ex,@rn,@rp,@cc,@ci,@di,@al,TRUE,@sub,@disc,@exp,@tax,@gt,@inote,FALSE,FALSE,@created,FALSE)", pars);

            // kalemler
            if (linesByOrder.TryGetValue(legacyId, out var lines))
                foreach (var l in lines)
                {
                    int legacyVarId = (int)l[0];
                    string barcode = (string)l[1], code = (string)l[2], pName = (string)l[3], color = (string)l[4], vval = (string)l[5];
                    double price = (double)l[6]; int qty = (int)l[7]; double ldisc = (double)l[8];
                    var variantId = variantMap.TryGetValue(legacyVarId, out var vg) ? vg : Guid.Empty;
                    if (variantId == Guid.Empty) unresolvedVar++;
                    decimal lineSub = (decimal)(price * qty);
                    PgExec(@"INSERT INTO ""order"".ord_order_items (""Id"",""OrderId"",""VariantId"",""Sku"",""ProductName"",""VariantInfo"",""Quantity"",""UnitPrice"",
                        ""Subtotal"",""DiscountAmount"",""TaxAmount"",""Total"",""Status"",""SortingBinQuantity"",""FinalSortQuantity"",""FinalScanQuantity"",""CreatedAt"",""IsDeleted"")
                        VALUES (@id,@oid,@vid,@sku,@pn,@vi,@q,@up,@sub,@disc,0,@tot,@st,0,0,0,@now,FALSE)",
                        ("id", NewId()), ("oid", oid), ("vid", variantId), ("sku", string.IsNullOrWhiteSpace(code) ? barcode : code),
                        ("pn", pName), ("vi", ($"{color} {vval}").Trim()), ("q", qty), ("up", (decimal)price),
                        ("sub", lineSub), ("disc", (decimal)ldisc), ("tot", lineSub - (decimal)ldisc), ("st", "confirmed"), ("now", Now));
                    lineCount++;
                }
        }
        Log($"  Sipariş: {ins} eklendi, {upd} güncellendi; {lineCount} kalem ({unresolvedVar} varyant eşleşmedi → snapshot).");
    }

    static async Task Phase24_Favorites()
    {
        Log("=== FAZ 24: Mishar favori aktarımı ===");
        await Task.CompletedTask;
        EnsureMemberMap();
        var fp = MisharFp();

        var favs = new List<(int uyeId, int varId)>();
        using (var r = MysqlQuery("SELECT f.uyeId, f.urunAnaVaryantId FROM webuyefavorileri f JOIN webmembers m ON m.Id=f.uyeId WHERE m.platformId=" + MISHAR_PLATFORM))
            while (r.Read()) favs.Add((r.GetInt32(0), r.IsDBNull(1) ? 0 : r.GetInt32(1)));
        Log($"  {favs.Count} favori okundu.");

        // varyant Id -> ürün kodu (apurunvaryantlari -> apurunler.urunKodu)
        var varIds = favs.Select(f => f.varId).Where(v => v > 0).Distinct().ToList();
        var codeByVar = new Dictionary<int, string>();
        if (varIds.Count > 0)
            using (var r = MysqlQuery($"SELECT v.Id, p.urunKodu FROM apurunvaryantlari v JOIN apurunler p ON p.Id=v.urunId WHERE v.Id IN ({string.Join(",", varIds)})"))
                while (r.Read()) if (!r.IsDBNull(1)) codeByVar[r.GetInt32(0)] = r.GetString(1);

        // idempotent: aktarılan üyelerin mishar favorilerini sil-sonra-ekle
        foreach (var mid in memberMap.Values)
            PgExec("DELETE FROM storefront.favorites WHERE \"FirmPlatformId\"=@fp AND \"MemberId\"=@m", ("fp", fp), ("m", mid));

        int ins = 0, skip = 0;
        var seen = new HashSet<string>();
        foreach (var f in favs)
        {
            if (!memberMap.TryGetValue(f.uyeId, out var mid) || !codeByVar.TryGetValue(f.varId, out var code)) { skip++; continue; }
            if (!seen.Add(mid + "|" + code)) continue; // aynı üye+ürün tekrarı (renk yok sayıldı)
            PgExec(@"INSERT INTO storefront.favorites (""Id"",""FirmPlatformId"",""MemberId"",""ProductCode"",""CreatedAt"",""IsDeleted"") VALUES (@id,@fp,@m,@c,@now,FALSE)",
                ("id", NewId()), ("fp", fp), ("m", mid), ("c", code), ("now", Now));
            ins++;
        }
        Log($"  Favori: {ins} eklendi, {skip} atlandı (üye/ürün eşleşmedi).");
    }

    // ─── FAZ 29: ÜRÜN VİDEOSU + YORUM/PUAN AKTARIMI ──────────────────────────
    // Eski sistemden (apurunvideolari + opyorumlar) mishar kanalına aktarır. Tekrarlanabilir:
    //   • Video: apurunvideolari.videoUrl'i olduğu gibi (hotlink) product_videos.VideoUrl'e yazar;
    //     ürün kodu videoUrl'in dosya adından çözülür (P-00001666.mov → P-00001666). Sabit
    //     IMPORT_BATCH ile idempotent (yeniden koşuda o batch silinip yeniden yazılır; FTP/panel
    //     yüklemesi videolara DOKUNMAZ). ImageSetId = ürün görsellerinin kullandığı 'julude' seti.
    //   • Yorum: opyorumlar TÜM platformlardan (misharitalia'nın kendi yorumu yok; aynı ürünlerin
    //     yorumları ürün bazında getirilir). onay=1 → Status='approved' (sitede görünür + kart puanı),
    //     onay=0 → 'pending' (moderasyon havuzu, görünmez). MemberId = sentinel (Guid.Empty), görünüm
    //     MemberName kullanır. CreatedBy = IMPORT_MARKER → yeniden koşuda yalnız içe-aktarılanlar
    //     silinir, kullanıcının sonradan yazdığı yorumlar korunur. Tekrar eden legacy satırlar elenir.
    //   • Yorum fotoğrafı (opyorumresimler) bu fazda AKTARILMAZ — kaynak barındırma adresi çözülemedi;
    //     bulununca /media/reviews'e kopyalanıp product_review_photos'a eklenecek (ayrı geçiş).
    // args[1]=="dry" → yalnız rapor, yazma yok.
    static async Task Phase29_VideosAndReviews(bool dryRun)
    {
        Log($"=== FAZ 29: Video + yorum aktarımı{(dryRun ? " (DRY RUN — yazma yok)" : "")} ===");
        await Task.CompletedTask;
        var fp = MisharFp();
        var setId = Guid.Parse("a2b8502b-d947-48d8-9b06-1127c3c4c909");     // julude image set (ürün görselleri onda)
        var importBatch = Guid.Parse("29000000-0000-0000-0000-000000000001"); // sabit video batch (idempotent)
        var importMarker = Guid.Parse("29000000-0000-0000-0000-000000000002"); // yorum CreatedBy işareti (idempotent)
        var sentinelMember = Guid.Empty;                                     // içe-aktarılan yorumcu (üye eşlemesi yok)

        // katalog: kod → productId (büyük/küçük harf duyarsız)
        var productIdByCode = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        using (var r = new NpgsqlCommand("SELECT \"Id\",\"Code\" FROM catalog.products WHERE \"IsDeleted\"=FALSE", pg).ExecuteReader())
            while (r.Read()) productIdByCode[r.GetString(1)] = r.GetGuid(0);
        Log($"  Katalog ürün: {productIdByCode.Count}");

        // ── VİDEOLAR ──
        var videos = new List<(string code, string url, int sira)>();
        using (var r = MysqlQuery("SELECT videoUrl, COALESCE(siraNo,1) FROM apurunvideolari WHERE videoUrl IS NOT NULL AND videoUrl<>''"))
            while (r.Read())
            {
                var url = r.GetString(0);
                var baseName = url.Split('/').Last();                        // P-00001666.mov
                var dot = baseName.LastIndexOf('.');
                var code = dot > 0 ? baseName[..dot] : baseName;
                videos.Add((code, url, r.GetInt32(1)));
            }
        var videoEsel = videos.Where(v => productIdByCode.ContainsKey(v.code)).ToList();
        Log($"  Video: {videos.Count} okundu, {videoEsel.Count} kataloğumuzda eşleşti.");

        if (!dryRun)
        {
            PgExec("DELETE FROM catalog.product_videos WHERE \"BatchId\"=@b", ("b", importBatch));
            int vi = 0;
            foreach (var v in videoEsel)
            {
                PgExec(@"INSERT INTO catalog.product_videos
                    (""Id"",""ProductId"",""ImageSetId"",""FileName"",""SortOrder"",""Status"",""BatchId"",""VideoUrl"",""CreatedAt"",""CreatedBy"",""IsDeleted"")
                    VALUES (@id,@pid,@set,'',@sira,'Active',@b,@url,@now,@by,FALSE)",
                    ("id", NewId()), ("pid", productIdByCode[v.code]), ("set", setId),
                    ("sira", v.sira), ("b", importBatch), ("url", v.url), ("now", Now), ("by", importMarker));
                vi++;
            }
            Log($"  Video: {vi} kayıt yazıldı.");
        }

        // ── YORUMLAR ── (opyorumlar, tüm platformlar; ürün bazında)
        var reviews = new List<(string code, int puan, string yorum, string ad, DateTime created, bool onay, DateTime? onayTar)>();
        using (var r = MysqlQuery(
            "SELECT a.urunKodu, COALESCE(o.puan,0), o.yorum, COALESCE(o.memberName,''), o.createdDate, o.onay+0, o.onayTarihi " +
            "FROM opyorumlar o JOIN apurunler a ON a.Id=o.urunId WHERE a.urunKodu IS NOT NULL AND o.yorum IS NOT NULL"))
            while (r.Read())
            {
                var puan = r.GetInt32(1);
                if (puan < 1 || puan > 5) continue;                          // geçersiz/0 puan atlanır
                var created = r.IsDBNull(4) ? Now : r.GetDateTime(4);
                reviews.Add((r.GetString(0), puan, r.GetString(2), r.GetString(3), created,
                    r.GetInt64(5) != 0, r.IsDBNull(6) ? (DateTime?)null : r.GetDateTime(6)));
            }
        Log($"  Yorum: {reviews.Count} okundu (geçerli puanlı).");

        int onayli = 0, bekleyen = 0, atlanan = 0;
        var seen = new HashSet<string>();
        var yazilacak = new List<(string code, int puan, string text, string ad, DateTime created, string status, DateTime? modAt)>();
        foreach (var rv in reviews)
        {
            if (!productIdByCode.ContainsKey(rv.code)) { atlanan++; continue; } // kataloğumuzda yok
            var textFull = rv.yorum.Trim();
            var text = textFull.Length > 2000 ? textFull[..2000] : textFull;
            var ad = string.IsNullOrWhiteSpace(rv.ad) ? "Müşteri" : rv.ad.Trim();
            if (ad.Length > 100) ad = ad[..100];
            // legacy'deki birebir tekrar satırları ele (aynı ürün+ad+puan+metin+tarih)
            var key = rv.code + "|" + ad + "|" + rv.puan + "|" + text + "|" + rv.created.Ticks;
            if (!seen.Add(key)) { atlanan++; continue; }
            var status = rv.onay ? "approved" : "pending";
            if (rv.onay) onayli++; else bekleyen++;
            yazilacak.Add((rv.code, rv.puan, string.IsNullOrEmpty(text) ? null! : text, ad, rv.created, status,
                rv.onay ? (rv.onayTar ?? rv.created) : null));
        }
        Log($"  Yorum eşleşen: {yazilacak.Count} (onaylı {onayli}, bekleyen {bekleyen}), atlanan {atlanan} (katalogda yok/tekrar).");

        if (!dryRun)
        {
            PgExec("DELETE FROM storefront.product_reviews WHERE \"FirmPlatformId\"=@fp AND \"CreatedBy\"=@m", ("fp", fp), ("m", importMarker));
            int ri = 0;
            foreach (var y in yazilacak)
            {
                PgExec(@"INSERT INTO storefront.product_reviews
                    (""Id"",""FirmPlatformId"",""MemberId"",""ProductCode"",""Rating"",""Text"",""Status"",""MemberName"",""ModeratedAt"",""CreatedAt"",""CreatedBy"",""IsDeleted"")
                    VALUES (@id,@fp,@mem,@code,@rating,@text,@status,@name,@modat,@created,@by,FALSE)",
                    ("id", NewId()), ("fp", fp), ("mem", sentinelMember), ("code", y.code),
                    ("rating", y.puan), ("text", (object?)y.text ?? DBNull.Value), ("status", y.status),
                    ("name", y.ad), ("modat", (object?)y.modAt ?? DBNull.Value),
                    ("created", y.created), ("by", importMarker));
                ri++;
            }
            Log($"  Yorum: {ri} kayıt yazıldı.");
            Log("  Not: toplu yazımdan sonra ANALYZE önerilir (catalog.product_videos, storefront.product_reviews).");
        }
        Log("  Yorum fotoğrafları (opyorumresimler, 319) bu fazda atlandı — kaynak adres çözülünce ayrı geçişte.");
    }

    // ─── FAZ 25: MİSHAR MENÜ AKTARIMI ────────────────────────────────────────
    // misharitalia.com menüsünü (plmenuyeni, platform 41) birebir taşır:
    //   1) Menüde görünen her benzersiz url için bir storefront.channel_categories kaydı.
    //      Doldurma önceliği: plfiltreler SQL'i → dinamikse FilterDef, statik parça
    //      (altgrup/keyword/LIKE/kod listesi) içeriyorsa MySQL'den ürün kodları materyalize
    //      edilip channel_category_products'a yazılır; filtre yoksa dftumkategoriler
    //      hiyerarşisi; o da yoksa webkategoriurunleri cache'i.
    //   2) 'header' kodlu nav_menus + nav_nodes yerleşimi — aynı kategorinin birden çok
    //      üst bölümde görünmesi (çapraz yerleşim) düğüm tekrarıyla korunur.
    // Tekrarlanabilir: her koşuda header menü + platformun tüm kanal kategorileri silinip
    // yeniden kurulur (footer menüsüne dokunulmaz; nav_nodes.ChannelCategoryId SET NULL).
    static async Task Phase25_MisharMenu()
    {
        Log("=== FAZ 25: Mishar menü aktarımı ===");
        await Task.CompletedTask;
        var fp = MisharFp();

        // ── MySQL kaynakları ──
        var menuRows = new List<(int Id, int ParentId, string Ad, string Url, int Sira, bool Tik, bool Gost, string UrlTipi, string ListTipi, string? Resim, string? KatBaslik)>();
        using (var r = MysqlQuery("SELECT Id, parentId, menuAdi, COALESCE(url,''), siraNo, tiklanabilir+0, menudeGoster+0, " +
                "COALESCE(urlTipi,'URUN'), COALESCE(urunListelemeTipi,'RENK'), resimUrl, kategoriBasligi " +
                $"FROM plmenuyeni WHERE platformId={MISHAR_PLATFORM} ORDER BY parentId, siraNo, Id"))
            while (r.Read())
                menuRows.Add((r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetString(3), r.GetInt32(4),
                    r.GetInt64(5) != 0, r.GetInt64(6) != 0, r.GetString(7), r.GetString(8),
                    r.IsDBNull(9) ? null : r.GetString(9), r.IsDBNull(10) ? null : r.GetString(10)));

        var urlInfo = new Dictionary<string, (int KurlId, int FiltreId, string? MetaTitle, string? MetaDesc, string? KatBaslik, string ListTipi)>();
        using (var r = MysqlQuery("SELECT url, Id, COALESCE(filtreId,0), metatitle, metadescription, kategoriBasligi, COALESCE(urunListelemeTipi,'RENK') " +
                $"FROM plkategoriurl WHERE platformId={MISHAR_PLATFORM}"))
            while (r.Read())
            {
                string u = r.GetString(0);
                if (!urlInfo.ContainsKey(u))
                    urlInfo[u] = (r.GetInt32(1), r.GetInt32(2), r.IsDBNull(3) ? null : r.GetString(3),
                        r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.GetString(6));
            }

        // Filtre platform kısıtsız yüklenir: bazı kategoriler başka platformda tanımlanmış
        // ortak filtreyi işaret eder (örn. kiz-cocuk-plaj-urunleri).
        var filtreSqlById = new Dictionary<int, string>();
        using (var r = MysqlQuery("SELECT Id, filtreSql FROM plfiltreler WHERE filtreSql IS NOT NULL"))
            while (r.Read()) filtreSqlById[r.GetInt32(0)] = r.GetString(1);

        var tumkat = new Dictionary<string, (string Tablo, int TabloId)>();
        using (var r = MysqlQuery("SELECT url, tabloAdi, tabloId FROM dftumkategoriler WHERE url IS NOT NULL AND tabloAdi IS NOT NULL AND tabloId IS NOT NULL"))
            while (r.Read()) tumkat[r.GetString(0)] = (r.GetString(1), r.GetInt32(2));

        var sinifCinsiyet = new Dictionary<int, int>();   // sınıf → cinsiyet
        using (var r = MysqlQuery("SELECT Id, COALESCE(cinsiyetId,0) FROM dfurunsiniflari"))
            while (r.Read()) sinifCinsiyet[r.GetInt32(0)] = r.GetInt32(1);
        var grupSinif = new Dictionary<int, int>();       // grup → sınıf
        using (var r = MysqlQuery("SELECT Id, COALESCE(urunSinifId,0) FROM dfurungruplari"))
            while (r.Read()) grupSinif[r.GetInt32(0)] = r.GetInt32(1);

        // ── Yeni taraf eşlemeleri ──
        var grpGuid = new Dictionary<int, Guid>();        // legacy grup id → product_groups.Id (Code=grp_N)
        using (var r = new NpgsqlCommand("SELECT \"Id\", \"Code\" FROM definition.product_groups WHERE \"IsDeleted\"=FALSE AND \"Code\" LIKE 'grp_%'", pg).ExecuteReader())
            while (r.Read())
                if (int.TryParse(r.GetString(1)[4..], out int lid)) grpGuid[lid] = r.GetGuid(0);

        var productIdByCode = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var groupIdByCode = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase); // ürün kodu → yeni ProductGroupId
        using (var r = new NpgsqlCommand("SELECT \"Id\", \"Code\", \"ProductGroupId\" FROM catalog.products WHERE \"IsDeleted\"=FALSE", pg).ExecuteReader())
            while (r.Read()) { productIdByCode[r.GetString(1)] = r.GetGuid(0); groupIdByCode[r.GetString(1)] = r.GetGuid(2); }

        // Faz 9'da birleştirilip kodu silinen legacy gruplar için yönlendirme: legacy grubun
        // ürünlerinin yeni katalogda EN ÇOK düştüğü ProductGroupId esas alınır (baskın grup).
        var legacyGrupByCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using (var r = MysqlQuery("SELECT urunKodu, COALESCE(urunGrupId,0) FROM apurunler " +
                "WHERE urunKodu IS NOT NULL AND urunKodu != '' AND urunKodu IN (SELECT urunkodu FROM yeniurunkodlari)"))
            while (r.Read()) legacyGrupByCode[r.GetString(0)] = r.GetInt32(1);
        var dominantGrpGuid = legacyGrupByCode
            .Where(kv => kv.Value > 0 && groupIdByCode.ContainsKey(kv.Key))
            .GroupBy(kv => kv.Value)
            .ToDictionary(g => g.Key, g => g
                .GroupBy(kv => groupIdByCode[kv.Key])
                .OrderByDescending(x => x.Count())
                .First().Key);
        foreach (var (lid, guid) in dominantGrpGuid)
            if (!grpGuid.ContainsKey(lid)) grpGuid[lid] = guid;
        Log($"  Grup eşlemesi: {grpGuid.Count} legacy grup ({dominantGrpGuid.Count(kv => grpGuid[kv.Key] == kv.Value)} baskın-ürün yönlendirmesi dahil).");

        Guid cinsiyetTypeId = AttrTypeId("cinsiyet");
        var cinsiyetAdlari = new Dictionary<int, string>
        {
            [1] = "Kadın", [2] = "Erkek", [3] = "Kız çocuk", [4] = "Erkek çocuk", [5] = "Çocuk",
            [6] = "Bebek", [7] = "Unisex", [8] = "Cinsiyetsiz", [9] = "Kız Bebek", [10] = "Erkek Bebek"
        };
        var cinsiyetGuid = new Dictionary<int, Guid>();
        foreach (var (cid, ad) in cinsiyetAdlari)
            try { cinsiyetGuid[cid] = AttrValueId("cinsiyet", ad); }
            catch { Log($"  ! cinsiyet değeri yeni DB'de yok: {ad} (legacy {cid})"); }

        // ── Görünür menü ağacı (parent zinciri boyunca menudeGoster=1) ──
        var byParent = menuRows.GroupBy(m => m.ParentId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Sira).ThenBy(x => x.Id).ToList());
        var tree = new List<(int RowIdx, int? ParentTreeIdx)>();
        void Walk(int parentRowId, int? parentTreeIdx)
        {
            if (!byParent.TryGetValue(parentRowId, out var cocuklar)) return;
            foreach (var m in cocuklar)
            {
                if (!m.Gost || string.IsNullOrWhiteSpace(m.Url)) continue;
                int idx = tree.Count;
                tree.Add((menuRows.IndexOf(m), parentTreeIdx));
                Walk(m.Id, idx);
            }
        }
        Walk(0, null);
        Log($"  Menü: {menuRows.Count} satır, görünür ağaç {tree.Count} düğüm.");

        // ── Temizlik (tekrarlanabilirlik) ──
        PgExec("DELETE FROM storefront.nav_menus WHERE \"FirmPlatformId\"=@fp AND \"Code\"='header'", ("fp", fp));
        PgExec("UPDATE storefront.channel_categories SET \"ParentId\"=NULL WHERE \"FirmPlatformId\"=@fp", ("fp", fp));
        PgExec("DELETE FROM storefront.channel_categories WHERE \"FirmPlatformId\"=@fp", ("fp", fp));

        // ── Filtre SQL çözümleme ──
        (bool Statik, List<int> Cins, List<int> SinifInc, List<int> SinifExc, List<int> GrupInc, List<int> GrupExc,
         List<int> AltGrup, List<string> Kodlar, int? ImageDays, (int TipId, string Deger)? Keyword, string? Like)
            ParseFiltre(string sql)
        {
            List<int> Nums(string pattern, int grp = 1) =>
                Regex.Matches(sql, pattern).Select(m => int.Parse(m.Groups[grp].Value)).Distinct().ToList();
            List<int> InList(string pattern) =>
                Regex.Matches(sql, pattern)
                    .SelectMany(m => m.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    .Select(int.Parse).Distinct().ToList();

            var cins = Nums(@"u\.cinsiyetId\s*=\s*(\d+)").Concat(InList(@"u\.cinsiyetId\s+IN\s*\(([\d,\s]+)\)")).Distinct().ToList();
            var sinifExc = InList(@"u\.urunSinifId\s+NOT\s+IN\s*\(([\d,\s]+)\)");
            var sinifInc = Nums(@"u\.urunSinifId\s*=\s*(\d+)").Concat(InList(@"u\.urunSinifId\s+IN\s*\(([\d,\s]+)\)")).Except(sinifExc).Distinct().ToList();
            var grupExc = Nums(@"u\.urunGrupId\s*!=\s*(\d+)").Concat(InList(@"u\.urunGrupId\s+NOT\s+IN\s*\(([\d,\s]+)\)")).Distinct().ToList();
            var grupInc = Nums(@"u\.urunGrupId\s*=\s*(\d+)").Concat(InList(@"u\.urunGrupId\s+IN\s*\(([\d,\s]+)\)")).Except(grupExc).Distinct().ToList();
            var altGrup = Nums(@"u\.urunAltGrupId\s*=\s*(\d+)").Concat(InList(@"u\.urunAltGrupId\s+IN\s*\(([\d,\s]+)\)")).Distinct().ToList();
            var kodlar = Regex.Matches(sql, @"u\.urunKodu\s+IN\s*\(([^)]*)\)")
                .SelectMany(m => Regex.Matches(m.Groups[1].Value, @"'([^']+)'").Select(k => k.Groups[1].Value))
                .Distinct().ToList();
            int? imageDays = Regex.Match(sql, @"INTERVAL\s*-(\d+)\s*DAY") is { Success: true } im ? int.Parse(im.Groups[1].Value) : null;
            (int, string)? keyword = Regex.Match(sql, @"varyantTipId\s*=\s*(\d+)\s+AND\s+varyantDegeri\s*=\s*'([^']*)'") is { Success: true } kw
                ? (int.Parse(kw.Groups[1].Value), kw.Groups[2].Value) : null;
            string? like = Regex.Match(sql, @"u\.urunAdi\s+LIKE\s+'([^']*)'") is { Success: true } lk ? lk.Groups[1].Value : null;

            bool statik = altGrup.Count > 0 || kodlar.Count > 0 || keyword is not null || like is not null;
            return (statik, cins, sinifInc, sinifExc, grupInc, grupExc, altGrup, kodlar, imageDays, keyword, like);
        }

        List<string> MaterializeCodes(
            List<int> cins, List<int> sinifInc, List<int> sinifExc, List<int> grupInc, List<int> grupExc,
            List<int> altGrup, List<string> kodlar, int? imageDays, (int TipId, string Deger)? keyword, string? like)
        {
            // Yalnız kod listesi varsa liste sırası korunur (merchandising sırası)
            if (kodlar.Count > 0 && cins.Count == 0 && sinifInc.Count == 0 && grupInc.Count == 0 && altGrup.Count == 0 && keyword is null && like is null)
                return kodlar;

            var sb = new StringBuilder("SELECT DISTINCT u.urunKodu FROM apurunler u WHERE u.urunKodu IN (SELECT urunkodu FROM yeniurunkodlari)");
            if (cins.Count > 0) sb.Append($" AND u.cinsiyetId IN ({string.Join(",", cins)})");
            if (sinifInc.Count > 0) sb.Append($" AND u.urunSinifId IN ({string.Join(",", sinifInc)})");
            if (sinifExc.Count > 0) sb.Append($" AND u.urunSinifId NOT IN ({string.Join(",", sinifExc)})");
            if (grupInc.Count > 0) sb.Append($" AND u.urunGrupId IN ({string.Join(",", grupInc)})");
            if (grupExc.Count > 0) sb.Append($" AND u.urunGrupId NOT IN ({string.Join(",", grupExc)})");
            if (altGrup.Count > 0) sb.Append($" AND u.urunAltGrupId IN ({string.Join(",", altGrup)})");
            if (kodlar.Count > 0) sb.Append($" AND u.urunKodu IN ({string.Join(",", kodlar.Select(k => $"'{k.Replace("'", "''")}'"))})");
            if (like is not null) sb.Append($" AND u.urunAdi LIKE '{like.Replace("'", "''")}'");
            if (keyword is { } k)
                sb.Append($" AND EXISTS (SELECT 1 FROM apurunvaryanttipdegerleri v WHERE v.urunId=u.Id AND v.varyantTipId={k.TipId} AND v.varyantDegeri='{k.Deger.Replace("'", "''")}')");
            if (imageDays is { } d)
                sb.Append($" AND EXISTS (SELECT 1 FROM apurunresimleri ar WHERE ar.urunId=u.Id AND ar.olusturmaTarihi>=DATE_ADD(NOW(), INTERVAL -{d} DAY))");
            sb.Append(" ORDER BY u.Id DESC");

            var sonuc = new List<string>();
            using var r = MysqlQuery(sb.ToString());
            while (r.Read()) if (!r.IsDBNull(0)) sonuc.Add(r.GetString(0));
            return sonuc;
        }

        // Dinamik FilterDef'in kaba ürün karşılığı (stok/kanal geçitleri HARİÇ): 0 ise bu
        // kategorinin yeni katalogda hiç ürünü yok demektir (örn. keep-listesi dışı) —
        // dinamik bırakmak kalıcı boş dal üretir, cache listesine düşmek daha doğru.
        // Stok-yok durumları burada >0 döner ve dinamik kalır (stok gelince kendiliğinden görünür).
        long KabaUrunSayisi(Guid[] grupGuidler, Guid[] cinsGuidler)
        {
            var sql = new StringBuilder("SELECT COUNT(*) FROM catalog.products p WHERE p.\"IsDeleted\"=FALSE");
            if (grupGuidler.Length > 0)
                sql.Append(" AND p.\"ProductGroupId\" = ANY(@grp)");
            if (cinsGuidler.Length > 0)
                sql.Append(" AND EXISTS (SELECT 1 FROM catalog.product_attributes a WHERE a.\"ProductId\"=p.\"Id\" AND a.\"IsDeleted\"=FALSE AND a.\"AttributeValueId\" = ANY(@cins))");
            using var cmd = new NpgsqlCommand(sql.ToString(), pg);
            if (grupGuidler.Length > 0) cmd.Parameters.AddWithValue("grp", grupGuidler);
            if (cinsGuidler.Length > 0) cmd.Parameters.AddWithValue("cins", cinsGuidler);
            return (long)cmd.ExecuteScalar()!;
        }

        int grpEksik = 0;
        (string? Json, Guid[] GrupGuidler, Guid[] CinsGuidler) BuildFilterDef(List<int> cins, List<int> sinifInc, List<int> sinifExc, List<int> grupInc, List<int> grupExc, int? imageDays)
        {
            IEnumerable<int> SinifUyeleri(IEnumerable<int> siniflar) =>
                grupSinif.Where(kv => siniflar.Contains(kv.Value)).Select(kv => kv.Key);

            var grupIds = new HashSet<int>();
            if (grupInc.Count > 0) grupIds.UnionWith(grupInc);
            else if (sinifInc.Count > 0) grupIds.UnionWith(SinifUyeleri(sinifInc));
            else if (sinifExc.Count > 0 || grupExc.Count > 0)
            {
                // Dışlama listesi include listesine çevrilir (FilterDef dışlama desteklemez):
                // evren = (cinsiyet verilmişse o cinsiyetin sınıflarındaki) tüm gruplar
                var evren = grupSinif.Where(kv =>
                        cins.Count == 0 ||
                        (sinifCinsiyet.TryGetValue(kv.Value, out int c) && cins.Contains(c)))
                    .Select(kv => kv.Key);
                grupIds.UnionWith(evren);
                grupIds.ExceptWith(SinifUyeleri(sinifExc));
            }
            grupIds.ExceptWith(grupExc);
            grupIds.ExceptWith(SinifUyeleri(sinifExc));

            var grupGuidler = grupIds.Select(id =>
            {
                if (grpGuid.TryGetValue(id, out var g)) return (Guid?)g;
                grpEksik++; return null;
            }).Where(g => g.HasValue).Select(g => g!.Value).Distinct().ToArray();

            var def = new Dictionary<string, object>();
            if (grupGuidler.Length > 0) def["productGroupIds"] = grupGuidler;
            var cinsGuidler = cins.Where(cinsiyetGuid.ContainsKey).Select(c => cinsiyetGuid[c]).ToArray();
            if (cinsGuidler.Length > 0)
                def["attributeFilters"] = new object[]
                {
                    new Dictionary<string, object> { ["attributeTypeId"] = cinsiyetTypeId, ["valueIds"] = cinsGuidler }
                };
            if (imageDays is { } d) def["imageUpdatedAfterDays"] = d;
            return (def.Count > 0 ? JsonSerializer.Serialize(def, JsonOpts) : null, grupGuidler, cinsGuidler);
        }

        // Eski sitenin gerçekte gösterdiği materyalize liste (webkategoriurunleri cache'i) —
        // filtre SQL'i bugünkü veriyle 0 dönse bile eski site bu cache'ten ürün basar
        // (örn. tesettur-bluz). Materyalize sonucu boş kalan her kategori buna düşer.
        List<string> CacheKodlari(int kurlId)
        {
            var codes = new List<string>();
            if (kurlId <= 0) return codes;
            using var r = MysqlQuery("SELECT DISTINCT p.urunKodu FROM webkategoriurunleri w " +
                "JOIN apurunanavaryantlari av ON av.Id=w.urunAnaVaryantId JOIN apurunler p ON p.Id=av.urunId " +
                $"WHERE w.kategoriUrlId={kurlId} AND p.urunKodu IN (SELECT urunkodu FROM yeniurunkodlari) ORDER BY p.urunKodu");
            while (r.Read()) if (!r.IsDBNull(0)) codes.Add(r.GetString(0));
            return codes;
        }

        var kaynaksiz = new List<string>();
        (string Fill, string? Json, List<string>? Codes) ResolveFill(string url)
        {
            var info = urlInfo.TryGetValue(url, out var i) ? i : default;
            string? sql = info.FiltreId > 0 ? filtreSqlById.GetValueOrDefault(info.FiltreId) : null;
            if (sql is not null)
            {
                var f = ParseFiltre(sql);
                if (f.Statik)
                {
                    var codes = MaterializeCodes(f.Cins, f.SinifInc, f.SinifExc, f.GrupInc, f.GrupExc, f.AltGrup, f.Kodlar, f.ImageDays, f.Keyword, f.Like);
                    if (codes.Count == 0) codes = CacheKodlari(info.KurlId);
                    if (codes.Count > 0) return ("manual", null, codes);
                    // filtre bugünkü veriyle boş — eski site bu durumda hiyerarşi
                    // çözümlemesine (dftumkategoriler) düşer; biz de aynısını yapalım
                }
                else
                {
                    var (json, grpG, cinsG) = BuildFilterDef(f.Cins, f.SinifInc, f.SinifExc, f.GrupInc, f.GrupExc, f.ImageDays);
                    if (json is not null)
                    {
                        if (KabaUrunSayisi(grpG, cinsG) > 0) return ("filter", json, null);
                        var cache = CacheKodlari(info.KurlId);
                        if (cache.Count > 0) return ("manual", null, cache);
                        return ("filter", json, null); // cache de boş — dinamik kalsın
                    }
                }
            }
            if (tumkat.TryGetValue(url, out var t))
            {
                switch (t.Tablo)
                {
                    case "dfcinsiyetler":
                    case "dfurunsiniflari":
                    case "dfurungruplari":
                    {
                        var (json, grpG, cinsG) = t.Tablo switch
                        {
                            "dfcinsiyetler" => BuildFilterDef(new List<int> { t.TabloId }, new(), new(), new(), new(), null),
                            "dfurunsiniflari" => BuildFilterDef(
                                sinifCinsiyet.TryGetValue(t.TabloId, out int sc) && sc > 0 ? new List<int> { sc } : new List<int>(),
                                new List<int> { t.TabloId }, new(), new(), new(), null),
                            _ => BuildFilterDef(
                                grupSinif.TryGetValue(t.TabloId, out int gs) && sinifCinsiyet.TryGetValue(gs, out int gc) && gc > 0
                                    ? new List<int> { gc } : new List<int>(),
                                new(), new(), new List<int> { t.TabloId }, new(), null),
                        };
                        if (json is not null && KabaUrunSayisi(grpG, cinsG) == 0)
                        {
                            var cache = CacheKodlari(info.KurlId);
                            if (cache.Count > 0) return ("manual", null, cache);
                        }
                        return ("filter", json ?? "{}", null);
                    }
                    case "dfurunaltgruplari":
                    {
                        var codes = MaterializeCodes(new(), new(), new(), new(), new(), new List<int> { t.TabloId }, new(), null, null, null);
                        if (codes.Count == 0) codes = CacheKodlari(info.KurlId);
                        return ("manual", null, codes);
                    }
                }
            }
            {
                // Son çare: eski sitenin materyalize cache'i (webkategoriurunleri)
                var codes = CacheKodlari(info.KurlId);
                if (codes.Count > 0) return ("manual", null, codes);
            }
            kaynaksiz.Add(url);
            return ("manual", null, new List<string>());
        }

        // ── Kategoriler (benzersiz url başına bir kayıt; ilk görünüm ana yerleşim) ──
        var catByUrl = new Dictionary<string, Guid>();
        int katSayisi = 0, manuelKat = 0, filtreKat = 0, urunSatiri = 0, kodEslesmedi = 0;
        foreach (var (rowIdx, parentTreeIdx) in tree)
        {
            var m = menuRows[rowIdx];
            if (catByUrl.ContainsKey(m.Url)) continue;

            Guid? parentCat = parentTreeIdx is int pi ? catByUrl[menuRows[tree[pi].RowIdx].Url] : null;
            var info = urlInfo.TryGetValue(m.Url, out var inf) ? inf : default;
            string ad = info.KatBaslik ?? m.KatBaslik ?? m.Ad;
            string listingMode = (info.ListTipi ?? m.ListTipi) == "MODEL" ? "model" : "color";
            var (fill, json, codes) = ResolveFill(m.Url);

            var id = NewId();
            PgExec(@"INSERT INTO storefront.channel_categories
                    (""Id"",""FirmPlatformId"",""ParentId"",""NameI18n"",""Slug"",""Status"",""FillType"",""FilterDef"",
                     ""SortOrder"",""MetaTitleI18n"",""MetaDescriptionI18n"",""ListingMode"",""CreatedAt"",""IsDeleted"")
                    VALUES (@id,@fp,@pid,@name::jsonb,@slug,'published',@fill,@fdef::jsonb,@sort,@mt::jsonb,@md::jsonb,@lm,@now,FALSE)",
                ("id", id), ("fp", fp), ("pid", (object?)parentCat ?? DBNull.Value), ("name", I18n(ad)),
                ("slug", m.Url), ("fill", fill), ("fdef", (object?)json ?? DBNull.Value), ("sort", katSayisi),
                ("mt", string.IsNullOrWhiteSpace(info.MetaTitle) ? DBNull.Value : I18n(info.MetaTitle)),
                ("md", string.IsNullOrWhiteSpace(info.MetaDesc) ? DBNull.Value : I18n(info.MetaDesc)),
                ("lm", listingMode), ("now", Now));
            catByUrl[m.Url] = id;
            katSayisi++;

            if (codes is not null)
            {
                manuelKat++;
                int sira = 0;
                foreach (var kod in codes)
                {
                    if (!productIdByCode.TryGetValue(kod, out var pidGuid)) { kodEslesmedi++; continue; }
                    PgExec(@"INSERT INTO storefront.channel_category_products (""Id"",""ChannelCategoryId"",""ProductId"",""SortOrder"",""IsExcluded"")
                            VALUES (@id,@cid,@pid,@s,FALSE) ON CONFLICT (""ChannelCategoryId"",""ProductId"") DO NOTHING",
                        ("id", NewId()), ("cid", id), ("pid", pidGuid), ("s", sira++));
                    urunSatiri++;
                }
            }
            else filtreKat++;
        }
        Log($"  Kategori: {katSayisi} oluşturuldu ({filtreKat} dinamik filtre, {manuelKat} materyalize liste; {urunSatiri} ürün satırı, {kodEslesmedi} kod eşleşmedi, {grpEksik} grup kodu yeni DB'de yok).");
        if (kaynaksiz.Count > 0) Log($"  ! Kaynaksız (boş) kategoriler: {string.Join(", ", kaynaksiz)}");

        // ── Nav menü + düğümler ──
        var menuId = NewId();
        PgExec(@"INSERT INTO storefront.nav_menus (""Id"",""FirmPlatformId"",""Code"",""NameI18n"",""MenuType"",""IsActive"",""SortOrder"",""CreatedAt"",""IsDeleted"")
                VALUES (@id,@fp,'header',@name::jsonb,'header',TRUE,0,@now,FALSE)",
            ("id", menuId), ("fp", fp), ("name", I18n("Ana Menü")), ("now", Now));

        var nodeIds = new Guid[tree.Count];
        var siblingSira = new Dictionary<int, int>(); // parentTreeIdx(-1=kök) → sıradaki SortOrder
        int resimliDugum = 0;
        foreach (var (i, (rowIdx, parentTreeIdx)) in tree.Select((t, i) => (i, t)))
        {
            var m = menuRows[rowIdx];
            int siraKey = parentTreeIdx ?? -1;
            int sort = siblingSira.TryGetValue(siraKey, out int s) ? s : 0;
            siblingSira[siraKey] = sort + 1;

            string nodeType = m.UrlTipi == "SAYFA" ? "link" : (!m.Tik ? "label" : "category");
            Guid? catId = nodeType == "category" ? catByUrl[m.Url] : null;

            string? imageUrl = null;
            if (!string.IsNullOrWhiteSpace(m.Resim))
            {
                string dosya = Path.GetFileName(m.Resim);
                if (File.Exists($"/opt/ECSProsAI/media/menu/{dosya}")) { imageUrl = $"/media/menu/{dosya}"; resimliDugum++; }
            }

            nodeIds[i] = NewId();
            PgExec(@"INSERT INTO storefront.nav_nodes
                    (""Id"",""NavigationMenuId"",""ParentNavNodeId"",""ChannelCategoryId"",""NameOverrideI18n"",""Slug"",
                     ""ImageUrl"",""OpenInNewTab"",""NodeType"",""CustomUrl"",""IsActive"",""SortOrder"",""CreatedAt"",""IsDeleted"")
                    VALUES (@id,@mid,@pid,@cid,@name::jsonb,@slug,@img,FALSE,@nt,@curl,TRUE,@sort,@now,FALSE)",
                ("id", nodeIds[i]), ("mid", menuId),
                ("pid", parentTreeIdx is int p2 ? nodeIds[p2] : DBNull.Value),
                ("cid", (object?)catId ?? DBNull.Value), ("name", I18n(m.Ad)), ("slug", m.Url),
                ("img", (object?)imageUrl ?? DBNull.Value), ("nt", nodeType),
                ("curl", nodeType == "link" ? "/" + m.Url : DBNull.Value), ("sort", sort), ("now", Now));
        }
        Log($"  Nav: 'header' menüsü + {tree.Count} düğüm ({resimliDugum} görselli).");

        PgExec("ANALYZE storefront.channel_categories");
        PgExec("ANALYZE storefront.channel_category_products");
        PgExec("ANALYZE storefront.nav_nodes");
        Log("FAZ 25 tamamlandı.");
    }
}
