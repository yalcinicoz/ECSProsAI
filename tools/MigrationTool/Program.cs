using System.Text.Json;
using MySql.Data.MySqlClient;
using Npgsql;

await Migration.RunAsync(args);

static class Migration
{
    const string MYSQL_CONN = "Server=51.178.208.50;Port=3306;Database=juludedb;Uid=web;Pwd={wb9&HqD&_zwg~?;Connection Timeout=30;SslMode=None;CharSet=utf8mb4;";
    const string PG_CONN = "Host=localhost;Port=5432;Database=ecommerce_db;Username=ecommerce;Password=***KALDIRILDI***;";

    // Schema: "catalog", tablo prefix: "catalog_", kolonlar PascalCase
    const string S = "catalog";

    static readonly Dictionary<int, Guid> imageSetMap = new();
    static readonly Dictionary<int, Guid> attrTypeMap = new();
    static readonly Dictionary<(int typeId, string value), Guid> attrValueMap = new();
    static readonly Dictionary<int, Guid> productGroupMap = new();
    static readonly Dictionary<int, Guid> productMap = new();
    static readonly Dictionary<int, Guid> variantMap = new();
    static readonly Dictionary<int, Guid> brandValueMap = new();
    static Guid markaTypeId = Guid.Empty;

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

        Log("=== Migration tamamlandı! ===");
        Log($"  catalog_image_sets          : {PgCount($"{S}.catalog_image_sets")}");
        Log($"  catalog_attribute_types     : {PgCount($"{S}.catalog_attribute_types")}");
        Log($"  catalog_attribute_values    : {PgCount($"{S}.catalog_attribute_values")}");
        Log($"  catalog_product_groups      : {PgCount($"{S}.catalog_product_groups")}");
        Log($"  catalog_products            : {PgCount($"{S}.catalog_products")}");
        Log($"  catalog_product_attributes  : {PgCount($"{S}.catalog_product_attributes")}");
        Log($"  catalog_product_variants    : {PgCount($"{S}.catalog_product_variants")}");
        Log($"  catalog_product_variant_attributes: {PgCount($"{S}.catalog_product_variant_attributes")}");
        Log($"  catalog_product_images      : {PgCount($"{S}.catalog_product_images")}");

        mysql.Close();
        pg.Close();
    }

    // ─── CLEAR ALL (FK sırası) ───────────────────────────────────────────────
    static void ClearAll()
    {
        Log("Tüm tablolar temizleniyor...");
        PgExec($"DELETE FROM {S}.catalog_product_images WHERE TRUE");
        ClearAttributeTables();
        PgExec($"DELETE FROM {S}.catalog_product_variants WHERE TRUE");
        PgExec($"DELETE FROM {S}.catalog_products WHERE TRUE");
        PgExec($"DELETE FROM {S}.catalog_product_groups WHERE TRUE");
        PgExec($"DELETE FROM {S}.catalog_image_sets WHERE TRUE");
        Log("  ✓ Temizlendi.");
    }

    static void ClearAttributeTables()
    {
        // Attribute'a bağlı tüm bağımlı tablolar — doğru FK sırası
        PgExec($"DELETE FROM {S}.catalog_product_variant_attributes WHERE TRUE");
        PgExec($"DELETE FROM {S}.catalog_product_attributes WHERE TRUE");
        PgExec($"DELETE FROM {S}.catalog_product_group_axis_sub_attributes WHERE TRUE");
        PgExec($"DELETE FROM {S}.catalog_product_group_attributes WHERE TRUE");
        PgExec($"DELETE FROM {S}.catalog_attribute_value_properties WHERE TRUE");
        PgExec($"DELETE FROM {S}.catalog_attribute_value_filter_colors WHERE TRUE");
        PgExec($"DELETE FROM {S}.catalog_attribute_values WHERE TRUE");
        PgExec($"DELETE FROM {S}.catalog_attribute_types WHERE TRUE");
    }

    // ─── FAZ 1: IMAGE SETS ───────────────────────────────────────────────────
    static Task Phase1_ImageSets()
    {
        Log("FAZ 1: ImageSets...");
        // Tek faz çalışıyorsa önce image_sets'e bağlı product_images'ı temizle
        PgExec($"DELETE FROM {S}.catalog_product_images WHERE TRUE");
        PgExec($"DELETE FROM {S}.catalog_image_sets WHERE TRUE");

        using var r = MysqlQuery("SELECT Id, setAdi FROM dfresimsetleri");
        int count = 0;
        while (r.Read())
        {
            int oldId = r.GetInt32(0);
            string name = r.GetString(1);
            string code = Slugify(name);
            var newId = NewId();
            imageSetMap[oldId] = newId;

            PgExec($@"INSERT INTO {S}.catalog_image_sets
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

            PgExec($@"INSERT INTO {S}.catalog_attribute_types
                (""Id"", ""Code"", ""NameI18n"", ""DataType"", ""IsActive"", ""SortOrder"", ""RequiresFilterColor"", ""CreatedAt"", ""IsDeleted"")
                VALUES (@id, @code, @name::jsonb, 'select', TRUE, @sort, FALSE, @now, FALSE)",
                ("id", newId), ("code", code), ("name", I18n(name)), ("sort", oldId), ("now", Now));
            count++;
        }

        // Marka
        markaTypeId = NewId();
        PgExec($@"INSERT INTO {S}.catalog_attribute_types
            (""Id"", ""Code"", ""NameI18n"", ""DataType"", ""IsActive"", ""SortOrder"", ""RequiresFilterColor"", ""CreatedAt"", ""IsDeleted"")
            VALUES (@id, 'marka', @name::jsonb, 'select', TRUE, 0, FALSE, @now, FALSE)",
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
        PgExec($"DELETE FROM {S}.catalog_attribute_values WHERE TRUE");

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
            PgExec($@"INSERT INTO {S}.catalog_attribute_values
                (""Id"", ""AttributeTypeId"", ""NameI18n"", ""IsActive"", ""SortOrder"", ""CreatedAt"", ""IsDeleted"")
                VALUES (@id, @tid, @name::jsonb, TRUE, @sort, @now, FALSE)",
                ("id", newId), ("tid", typeGuid), ("name", I18n(valueName)), ("sort", siraNo), ("now", Now));
            count++;
        }

        foreach (var (oldId, name) in brands)
        {
            var newId = NewId();
            brandValueMap[oldId] = newId;
            PgExec($@"INSERT INTO {S}.catalog_attribute_values
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
        PgExec($"DELETE FROM {S}.catalog_product_images WHERE TRUE");
        PgExec($"DELETE FROM {S}.catalog_product_variant_attributes WHERE TRUE");
        PgExec($"DELETE FROM {S}.catalog_product_variants WHERE TRUE");
        PgExec($"DELETE FROM {S}.catalog_product_attributes WHERE TRUE");
        PgExec($"DELETE FROM {S}.catalog_products WHERE TRUE");
        PgExec($"DELETE FROM {S}.catalog_product_groups WHERE TRUE");

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

            PgExec($@"INSERT INTO {S}.catalog_product_groups
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

            PgExec($@"UPDATE {S}.catalog_product_groups SET ""NameI18n"" = @name::jsonb WHERE ""Code"" = @code",
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

        PgExec($"DELETE FROM {S}.catalog_product_attributes WHERE TRUE");
        PgExec($"DELETE FROM {S}.catalog_products WHERE TRUE");

        Guid defaultGroupId = PgScalar<Guid>($"SELECT \"Id\" FROM {S}.catalog_product_groups LIMIT 1");

        using var r = MysqlQuery(@"SELECT Id, urunKodu, urunAdi, urunInternetAdi, markaId, urunGrupId,
            alisFiyati, satisFiyati, kdvOrani, tedarikciUrunKodu,
            interneteAcik, satisaAcik, olusturmaTarihi, guncellemeTarihi
            FROM apurunler WHERE urunKodu IS NOT NULL AND urunKodu != ''
            ORDER BY Id");

        int count = 0;
        var attrBatch = new List<(Guid productId, Guid attrTypeId, Guid attrValueId)>();

        while (r.Read())
        {
            int oldId = r.GetInt32(0);
            string kod = r.GetString(1);
            string ad = r.IsDBNull(2) ? kod : r.GetString(2);
            string? internetAdi = r.IsDBNull(3) ? null : r.GetString(3);
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
            // Slug unique constraint var — migration'da NULL bırak
            string? slug = null;
            bool isActive = interneteAcik && satisaAcik;
            DateTime created = createdAt.HasValue ? DateTime.SpecifyKind(createdAt.Value, DateTimeKind.Utc) : Now;
            object? updated = updatedAt.HasValue ? (object)DateTime.SpecifyKind(updatedAt.Value, DateTimeKind.Utc) : null;

            PgExec($@"INSERT INTO {S}.catalog_products
                (""Id"", ""ProductGroupId"", ""Code"", ""NameI18n"", ""BasePrice"", ""BaseCost"", ""TaxRate"",
                 ""IsActive"", ""SupplierProductCode"", ""Slug"", ""Tags"", ""CreatedAt"", ""UpdatedAt"", ""IsDeleted"")
                VALUES (@id, @grp, @code, @name::jsonb, @price, @cost, @kdv,
                        @active, @tedkod, @slug, '[]'::jsonb, @created, @updated, FALSE)",
                ("id", newId), ("grp", groupId), ("code", kod), ("name", I18n(ad)),
                ("price", satisFiyati), ("cost", alisFiyati == 0m ? null : (object)alisFiyati),
                ("kdv", kdv), ("active", isActive),
                ("tedkod", string.IsNullOrEmpty(tedUrunKod) ? null : (object)tedUrunKod),
                ("slug", (object?)slug), ("created", (object)created), ("updated", (object?)updated));

            if (markaId > 0 && brandValueMap.TryGetValue(markaId, out var brandValId))
                attrBatch.Add((newId, markaTypeId, brandValId));

            count++;
            if (count % 1000 == 0)
            {
                FlushProductAttributes(attrBatch);
                attrBatch.Clear();
                Log($"  {count} ürün...");
            }
        }

        FlushProductAttributes(attrBatch);
        Log($"  ✓ {count} Product");
        return Task.CompletedTask;
    }

    static void FlushProductAttributes(List<(Guid productId, Guid attrTypeId, Guid attrValueId)> batch)
    {
        foreach (var (pid, tid, vid) in batch)
        {
            PgExec($@"INSERT INTO {S}.catalog_product_attributes
                (""Id"", ""ProductId"", ""AttributeTypeId"", ""AttributeValueId"", ""CreatedAt"", ""IsDeleted"")
                VALUES (@id, @pid, @tid, @vid, @now, FALSE)",
                ("id", NewId()), ("pid", pid), ("tid", tid), ("vid", vid), ("now", Now));
        }
    }

    // ─── FAZ 6: VARIANTS ─────────────────────────────────────────────────────
    static Task Phase6_Variants()
    {
        Log("FAZ 6: ProductVariants...");
        EnsureAttrTypeMaps();
        EnsureAttrValueMap();
        EnsureProductMap();

        PgExec($"DELETE FROM {S}.catalog_product_variant_attributes WHERE TRUE");
        PgExec($"DELETE FROM {S}.catalog_product_variants WHERE TRUE");

        using var r = MysqlQuery(@"SELECT Id, urunId, barkod,
            varyant1TipId, varyant1Degeri,
            varyant2TipId, varyant2Degeri,
            varyant3TipId, varyant3Degeri,
            olusturmaTarihi
            FROM apurunvaryantlari ORDER BY urunId, Id");

        int count = 0, skipped = 0;
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
            PgExec($@"INSERT INTO {S}.catalog_product_variants
                (""Id"", ""ProductId"", ""Sku"", ""Barcode"", ""BasePrice"", ""IsActive"", ""CreatedAt"", ""IsDeleted"")
                VALUES (@id, @pid, @sku, @barcode, 0, TRUE, @now, FALSE)",
                ("id", newId), ("pid", productGuid), ("sku", sku),
                ("barcode", string.IsNullOrWhiteSpace(barkod) ? null : (object)barkod),
                ("now", (object)created));

            for (int ax = 0; ax < 3; ax++)
            {
                int tipId = r.GetInt32(3 + ax * 2);
                string val = r.IsDBNull(4 + ax * 2) ? "" : r.GetString(4 + ax * 2);
                if (tipId != 0 && !string.IsNullOrWhiteSpace(val))
                    attrQueue.Add((newId, tipId, val));
            }

            count++;
            if (count % 2000 == 0)
            {
                FlushVariantAttributes(attrQueue);
                attrQueue.Clear();
                Log($"  {count} varyant...");
            }
        }

        FlushVariantAttributes(attrQueue);
        Log($"  ✓ {count} ProductVariant ({skipped} atlandı)");
        return Task.CompletedTask;
    }

    static void FlushVariantAttributes(List<(Guid variantId, int tipId, string val)> queue)
    {
        foreach (var (variantId, tipId, val) in queue)
        {
            if (!attrTypeMap.TryGetValue(tipId, out var typeGuid)) continue;

            if (!attrValueMap.TryGetValue((tipId, val), out var valGuid))
            {
                valGuid = NewId();
                PgExec($@"INSERT INTO {S}.catalog_attribute_values
                    (""Id"", ""AttributeTypeId"", ""NameI18n"", ""IsActive"", ""SortOrder"", ""CreatedAt"", ""IsDeleted"")
                    VALUES (@id, @tid, @name::jsonb, TRUE, 0, @now, FALSE)",
                    ("id", valGuid), ("tid", typeGuid), ("name", I18n(val)), ("now", Now));
                attrValueMap[(tipId, val)] = valGuid;
            }

            PgExec($@"INSERT INTO {S}.catalog_product_variant_attributes
                (""Id"", ""VariantId"", ""AttributeTypeId"", ""AttributeValueId"", ""CreatedAt"", ""IsDeleted"")
                VALUES (@id, @vid, @tid, @avid, @now, FALSE)",
                ("id", NewId()), ("vid", variantId), ("tid", typeGuid), ("avid", valGuid), ("now", Now));
        }
    }

    // ─── FAZ 7: IMAGES ───────────────────────────────────────────────────────
    static Task Phase7_Images()
    {
        Log("FAZ 7: ProductImages...");
        EnsureImageSetMap();
        EnsureProductMap();
        EnsureVariantMap();

        PgExec($"DELETE FROM {S}.catalog_product_images WHERE TRUE");
        Guid defaultSetId = imageSetMap.Values.First();
        Guid batchId = NewId();
        var variantFirstImage = new HashSet<int>();

        using var r = MysqlQuery(@"SELECT resimSetId, urunId, urunAnaVaryantId, resimDosyaAdi, siraNo
            FROM apurunresimleri
            WHERE isSilindi = 0 AND resimDosyaAdi IS NOT NULL AND resimDosyaAdi != ''
            ORDER BY urunId, urunAnaVaryantId, siraNo");

        int count = 0;
        while (r.Read())
        {
            int oldSetId = r.IsDBNull(0) ? 1 : r.GetInt32(0);
            int urunId = r.GetInt32(1);
            int? variantOldId = r.IsDBNull(2) ? null : r.GetInt32(2);
            string fileName = r.GetString(3);
            int siraNo = r.IsDBNull(4) ? 0 : r.GetInt32(4);

            if (!productMap.TryGetValue(urunId, out var productGuid)) continue;

            Guid? variantGuid = null;
            if (variantOldId.HasValue && variantMap.TryGetValue(variantOldId.Value, out var vg))
                variantGuid = vg;

            var setId = imageSetMap.TryGetValue(oldSetId, out var sid) ? sid : defaultSetId;
            bool isVariantCover = variantOldId.HasValue && variantFirstImage.Add(variantOldId.Value);

            PgExec($@"INSERT INTO {S}.catalog_product_images
                (""Id"", ""ProductId"", ""VariantId"", ""ImageSetId"", ""FileName"", ""SortOrder"",
                 ""IsProductCover"", ""IsVariantCover"", ""Status"", ""BatchId"", ""CreatedAt"", ""IsDeleted"")
                VALUES (@id, @pid, @vid, @setId, @fn, @sort, FALSE, @cover, 'Active', @batch, @now, FALSE)",
                ("id", NewId()), ("pid", productGuid),
                ("vid", variantGuid.HasValue ? (object)variantGuid.Value : null),
                ("setId", setId), ("fn", fileName), ("sort", siraNo),
                ("cover", isVariantCover), ("batch", batchId), ("now", Now));

            count++;
            if (count % 5000 == 0) Log($"  {count} resim...");
        }
        Log($"  ✓ {count} ProductImage");
        return Task.CompletedTask;
    }

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
            $"SELECT \"Id\", \"Code\", \"NameI18n\"->>'tr' FROM {S}.catalog_product_groups", pg).ExecuteReader())
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
                $"UPDATE {S}.catalog_product_groups SET \"NameI18n\" = @name::jsonb WHERE \"Id\" = @id", pg);
            nameCmd.Parameters.AddWithValue("name", I18n(canonical.nameTr));
            nameCmd.Parameters.AddWithValue("id", canonical.id);
            nameCmd.ExecuteNonQuery();

            // Ürünleri canonical gruba yönlendir
            using var updCmd = new NpgsqlCommand(
                $"UPDATE {S}.catalog_products SET \"ProductGroupId\" = @can WHERE \"ProductGroupId\" = ANY(@dupes)", pg);
            updCmd.Parameters.AddWithValue("can", canonical.id);
            updCmd.Parameters.AddWithValue("dupes", duplicateIds);
            int affected = updCmd.ExecuteNonQuery();
            updatedProducts += affected;

            // Duplicate grupları sil
            using var delCmd = new NpgsqlCommand(
                $"DELETE FROM {S}.catalog_product_groups WHERE \"Id\" = ANY(@dupes)", pg);
            delCmd.Parameters.AddWithValue("dupes", duplicateIds);
            delCmd.ExecuteNonQuery();

            Log($"  Birleştirildi: [{string.Join(", ", sorted.Select(g => g.nameTr))}] → \"{canonical.nameTr}\" ({affected} ürün)");
            mergedCount += duplicateIds.Length;
        }

        var remaining = PgCount($"{S}.catalog_product_groups");
        Log($"  ✓ {mergedCount} duplicate grup silindi, {updatedProducts} ürün yönlendirildi");
        Log($"  Kalan ürün grubu sayısı: {remaining}");
        return Task.CompletedTask;
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

        using var pgr = new NpgsqlCommand($"SELECT \"Id\", \"Code\" FROM {S}.catalog_attribute_types", pg).ExecuteReader();
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

        using var pgr = new NpgsqlCommand($"SELECT \"Id\", \"AttributeTypeId\", \"NameI18n\"->>'tr' FROM {S}.catalog_attribute_values", pg).ExecuteReader();
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
        using var pgr = new NpgsqlCommand($"SELECT \"Id\", \"Code\" FROM {S}.catalog_product_groups", pg).ExecuteReader();
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
        using var r0 = MysqlQuery("SELECT Id, urunKodu FROM apurunler WHERE urunKodu IS NOT NULL AND urunKodu != ''");
        var codes = new Dictionary<string, int>();
        while (r0.Read()) codes[r0.GetString(1)] = r0.GetInt32(0);

        using var pgr = new NpgsqlCommand($"SELECT \"Id\", \"Code\" FROM {S}.catalog_products", pg).ExecuteReader();
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

        using var pgr = new NpgsqlCommand($"SELECT \"Id\", \"Barcode\" FROM {S}.catalog_product_variants WHERE \"Barcode\" IS NOT NULL", pg).ExecuteReader();
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

        using var pgr = new NpgsqlCommand($"SELECT \"Id\", \"NameI18n\"->>'tr' FROM {S}.catalog_attribute_values WHERE \"AttributeTypeId\" = @tid", pg);
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

        using var pgr = new NpgsqlCommand($"SELECT \"Id\", \"Name\" FROM {S}.catalog_image_sets", pg).ExecuteReader();
        var pgSets = new Dictionary<string, Guid>();
        while (pgr.Read()) pgSets[pgr.GetString(1)] = pgr.GetGuid(0);

        foreach (var (id, name) in names)
            if (pgSets.TryGetValue(name, out var g)) imageSetMap[id] = g;
    }

    // ─── DB HELPERS ──────────────────────────────────────────────────────────
    static MySqlDataReader MysqlQuery(string sql)
    {
        var cmd = new MySqlCommand(sql, mysql) { CommandTimeout = 600 };
        return cmd.ExecuteReader();
    }

    static void PgExec(string sql, params (string name, object? value)[] parameters)
    {
        using var cmd = new NpgsqlCommand(sql, pg);
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    static T PgScalar<T>(string sql)
    {
        using var cmd = new NpgsqlCommand(sql, pg);
        return (T)cmd.ExecuteScalar()!;
    }

    static long PgCount(string table)
    {
        using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {table}", pg);
        return (long)cmd.ExecuteScalar()!;
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
}
