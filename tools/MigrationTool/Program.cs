using System.Text;
using System.Text.Json;
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
        if (phase == 20) await RunCatalogReload();

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

        Guid defaultGroupId = PgScalar<Guid>($"SELECT \"Id\" FROM {DEF}.product_groups LIMIT 1");

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

            var groupId = productGroupMap.TryGetValue(grupId, out var g) ? g : defaultGroupId;
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
        using var pgr = new NpgsqlCommand($"SELECT \"Id\", \"Code\" FROM {DEF}.product_groups", pg).ExecuteReader();
        while (pgr.Read())
        {
            string code = pgr.GetString(1); // "grp_123"
            if (code.StartsWith("grp_") && int.TryParse(code[4..], out int mid))
                productGroupMap[mid] = pgr.GetGuid(0);
        }
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
        await Phase14_FirmsAndChannelData();
        Log("╚══ FAZ 20 tamamlandı — doğrulama sonrası stok aktarımı ayrı koşulur ══╝");
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
}
