using System.Text;
using System.Text.Json;
using Npgsql;

// Telemania demo import aracı.
// Kullanım:  dotnet run --project tools/TelemaniaImport [manifestPath] [connectionString]
// Varsayılanlar: import-manifest.json + ecommerce_demo DB.
// Idempotent değil — "temizle + yeniden yükle" modeliyle çalışır (ürün verisi değişince tekrar koş).

await Import.RunAsync(args);

static class Import
{
    static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };
    static readonly string Tag = "demo-telemania";
    static readonly string[] ProductCols =
        { "Id", "ProductGroupId", "Code", "NameI18n", "BasePrice", "BaseCost", "TaxRate",
          "IsSaleOpen", "SupplierProductCode", "Slug", "Tags", "CreatedAt", "UpdatedAt", "IsDeleted" };
    static readonly string?[] ProductCasts =
        { null, null, null, "jsonb", null, null, null, null, null, null, "jsonb", null, null, null };

    static string I18n(string? tr) => JsonSerializer.Serialize(new Dictionary<string, string> { ["tr"] = tr ?? "" }, JsonOpts);
    static string Jsonb(object o) => JsonSerializer.Serialize(o, JsonOpts);
    static Guid NewId() => Guid.NewGuid();
    static DateTime Now => DateTime.UtcNow;
    static void Log(string m) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {m}");

    class Manifest { public List<Product> Products { get; set; } = new(); }
    class Product
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public string? Brand { get; set; }
        public string? Category { get; set; }
        public string? GroupCode { get; set; }
        public decimal? Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        public decimal? DiscountedPrice { get; set; }
        public string? ItemNumber { get; set; }
        public string? Url { get; set; }
        public RatingScore? RatingScore { get; set; }
        public List<string> LocalImages { get; set; } = new();
    }

    class RatingScore
    {
        public double AverageRating { get; set; }
        public int TotalCount { get; set; }
    }

    public static async Task RunAsync(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var manifestPath = args.Length > 0 ? args[0] : "/opt/ECSProsAI/data/demo/kozmetik/telemania/import-manifest.json";
        var connStr = args.Length > 1 ? args[1] : "Host=localhost;Port=5432;Database=ecommerce_demo;Username=ecommerce;Password=***KALDIRILDI***";

        var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })!;
        Log($"Manifest: {manifest.Products.Count} ürün");

        await using var pg = new NpgsqlConnection(connStr);
        await pg.OpenAsync();
        Log("Bağlantı açıldı.");

        var siteTypeId = await ScalarGuidAsync(pg, "SELECT \"Id\" FROM core.core_platform_types WHERE \"Code\"='site'")
            ?? throw new Exception("core_platform_types 'site' kaydı yok — demo DB migrate+seed edilmeli.");
        var imageSetId = await ScalarGuidAsync(pg, "SELECT \"Id\" FROM definition.image_sets WHERE \"Code\"='varsayilan' AND \"IsDeleted\"=false")
            ?? throw new Exception("varsayilan image_set yok.");

        // ── Firma + Platform (upsert) ──
        var firmId = await UpsertFirmAsync(pg);
        var platformId = await UpsertFirmPlatformAsync(pg, firmId, siteTypeId);
        Log($"Firma/Platform hazır (Firm={firmId}, Platform={platformId})");

        // ── Görsel servis: yerel media (yükseklik/kalite merdiveni dosyalarda) ──
        await ExecAsync(pg, "UPDATE definition.settings SET \"Value\"='/media/images/products' WHERE \"Key\"='ImageServer.CdnBaseUrl'");

        // ── tlm-* grup haritası ──
        var groupMap = new Dictionary<string, Guid>();
        await using (var cmd = new NpgsqlCommand("SELECT \"Id\",\"Code\" FROM definition.product_groups WHERE \"Code\" LIKE 'tlm_%' AND \"IsDeleted\"=false", pg))
        await using (var r = await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync()) groupMap[r.GetString(1)] = r.GetGuid(0);
        Log($"tlm-* grup sayısı: {groupMap.Count}");

        var missing = manifest.Products.Select(p => p.GroupCode).Distinct().Where(c => c is null || !groupMap.ContainsKey(c)).ToList();
        if (missing.Count > 0)
            throw new Exception($"Eşleşmeyen grup kodu(ları): {string.Join(", ", missing)}");

        // ── Temizle (yeniden koşulabilirlik) ──
        await ClearAsync(pg, platformId);

        // ── Ürünler + Varyantlar + Görseller ──
        var batchId = NewId();
        var products = new List<object?[]>();
        var variants = new List<object?[]>();
        var images = new List<object?[]>();
        var stockRows = new List<object?[]>();
        var productIds = new List<Guid>();
        var ratingSources = new List<object?[]>();

        // Tek depo (tek satıcı) — Depo → Kısım → Raf
        var (warehouseId, sectionId, binId) = await EnsureWarehouseStructureAsync(pg);

        foreach (var p in manifest.Products)
        {
            var groupId = groupMap[p.GroupCode!];
            var productId = NewId();
            var price = p.Price ?? 0m;

            products.Add(new object?[]
            {
                productId, groupId, $"TLM-{p.Id}", I18n(p.Name), price,
                (object?)null, 20, true, (object?)null, (object?)null, Jsonb(new[] { Tag }), Now, (object?)null, false
            });

            if (p.RatingScore is { TotalCount: > 0 } rs)
            {
                ratingSources.Add(new object?[]
                {
                    NewId(), platformId, $"TLM-{p.Id}", "trendyol",
                    p.Id.ToString(), (decimal)Math.Round(rs.AverageRating, 2), rs.TotalCount,
                    p.Url, Now, Now, false
                });
            }

            var variantId = NewId();
            variants.Add(new object?[]
            {
                variantId, productId, $"TLM-{p.Id}", string.IsNullOrWhiteSpace(p.ItemNumber) ? (object?)null : p.ItemNumber,
                price, true, Now, false
            });

            stockRows.Add(new object?[]
            {
                NewId(), variantId, warehouseId, DBNull.Value, sectionId, binId, "physical", 50, 0, Now, false
            });

            int sort = 0;
            foreach (var rel in p.LocalImages)
            {
                images.Add(new object?[]
                {
                    NewId(), productId, variantId, imageSetId, rel, sort,
                    sort == 0, sort == 0, "Active", batchId, Now, false
                });
                sort++;
            }

            productIds.Add(productId);
        }

        await BatchAsync(pg, "catalog.products", ProductCols, ProductCasts, products);
        await BatchAsync(pg, "storefront.product_rating_sources",
            new[] { "Id", "FirmPlatformId", "ProductCode", "Channel", "ExternalProductId", "AverageRating", "ReviewCount", "ExternalUrl", "LastSyncedAt", "CreatedAt", "IsDeleted" },
            new string?[11], ratingSources);
        await BatchAsync(pg, "catalog.product_variants",
            new[] { "Id", "ProductId", "Sku", "Barcode", "BasePrice", "IsActive", "CreatedAt", "IsDeleted" },
            new string?[8], variants);
        await BatchAsync(pg, "catalog.product_images",
            new[] { "Id", "ProductId", "VariantId", "ImageSetId", "FileName", "SortOrder", "IsProductCover", "IsVariantCover", "Status", "BatchId", "CreatedAt", "IsDeleted" },
            new string?[12], images);
        await BatchAsync(pg, "inventory.inv_stocks",
            new[] { "Id", "VariantId", "WarehouseId", "LocationId", "SectionId", "BinId", "StockType", "Quantity", "ReservedQuantity", "CreatedAt", "IsDeleted" },
            new string?[11], stockRows);
        Log($"✓ {products.Count} ürün + varyant + görsel + stok yazıldı.");

        // ── Kanal: satılan gruplar + kategoriler (her grup bir kategori) ──
        int catAdded = 0, groupAdded = 0;
        int sortOrder = 0;
        foreach (var (code, gid) in groupMap.OrderBy(kv => kv.Key))
        {
            // channel_product_groups (katman 2 karar kaydı)
            var existingG = await ScalarGuidAsync(pg, "SELECT \"Id\" FROM storefront.channel_product_groups WHERE \"FirmPlatformId\"=@fp AND \"ProductGroupId\"=@g AND \"IsDeleted\"=false",
                ("fp", platformId), ("g", gid));
            if (existingG is null)
            {
                await ExecAsync(pg, @"INSERT INTO storefront.channel_product_groups
                    (""Id"",""FirmPlatformId"",""ProductGroupId"",""Status"",""CreatedAt"",""IsDeleted"")
                    VALUES (@id,@fp,@g,'active',@now,FALSE)",
                    ("id", NewId()), ("fp", platformId), ("g", gid), ("now", Now));
                groupAdded++;
            }

            var slug = "telemania-" + code.Replace("tlm_", "");
            var filterDef = Jsonb(new Dictionary<string, object> { ["productGroupIds"] = new[] { gid } });
            var existingC = await ScalarGuidAsync(pg, "SELECT \"Id\" FROM storefront.channel_categories WHERE \"FirmPlatformId\"=@fp AND \"Slug\"=@s AND \"IsDeleted\"=false",
                ("fp", platformId), ("s", slug));
            if (existingC is null)
            {
                await ExecAsync(pg, @"INSERT INTO storefront.channel_categories
                    (""Id"",""FirmPlatformId"",""ParentId"",""NameI18n"",""Slug"",""Status"",""FillType"",""FilterDef"",""SortOrder"",""CreatedAt"",""IsDeleted"")
                    VALUES (@id,@fp,NULL,@name::jsonb,@slug,'published','filter',@fdef::jsonb,@sort,@now,FALSE)",
                    ("id", NewId()), ("fp", platformId), ("name", I18n(SlugToName(code))),
                    ("slug", slug), ("fdef", filterDef), ("sort", sortOrder++), ("now", Now));
                catAdded++;
            }
        }
        Log($"✓ {groupAdded} channel_product_group + {catAdded} channel_category eklendi.");

        Log("=== İMPORT TAMAMLANDI ===");
    }

    static string SlugToName(string groupCode)
    {
        // tlm_sac_boyasi -> "Sac Boyasi" (görünen ad; DB'deki NameI18n'den de okunabilir, basitlik için kod)
        var s = groupCode.Replace("tlm_", "").Replace("_", " ");
        return Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(s);
    }

    static async Task ClearAsync(NpgsqlConnection pg, Guid platformId)
    {
        // demo ürünlerinin varyant id'leri (stok temizliği için)
        var demoVariantIds = new List<Guid>();
        await using (var cmd = new NpgsqlCommand(
            "SELECT v.\"Id\" FROM catalog.product_variants v JOIN catalog.products p ON p.\"Id\"=v.\"ProductId\" WHERE p.\"Tags\" @> '[\"demo-telemania\"]'::jsonb", pg))
        await using (var r = await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync()) demoVariantIds.Add(r.GetGuid(0));

        if (demoVariantIds.Count > 0)
        {
            await ExecAsync(pg, "DELETE FROM inventory.inv_stocks WHERE \"VariantId\" = ANY(@ids)", ("ids", demoVariantIds.ToArray()));
            await ExecAsync(pg, "DELETE FROM catalog.product_images WHERE \"ProductId\" IN (SELECT \"Id\" FROM catalog.products WHERE \"Tags\" @> '[\"demo-telemania\"]'::jsonb)");
            await ExecAsync(pg, "DELETE FROM catalog.product_variant_attributes WHERE \"VariantId\" = ANY(@ids)", ("ids", demoVariantIds.ToArray()));
            await ExecAsync(pg, "DELETE FROM catalog.product_variants WHERE \"Id\" = ANY(@ids)", ("ids", demoVariantIds.ToArray()));
            await ExecAsync(pg, "DELETE FROM catalog.product_attributes WHERE \"ProductId\" IN (SELECT \"Id\" FROM catalog.products WHERE \"Tags\" @> '[\"demo-telemania\"]'::jsonb)");
            await ExecAsync(pg, "DELETE FROM catalog.products WHERE \"Tags\" @> '[\"demo-telemania\"]'::jsonb");
        }

        await ExecAsync(pg, "DELETE FROM storefront.product_rating_sources WHERE \"FirmPlatformId\"=@fp", ("fp", platformId));
        await ExecAsync(pg, "DELETE FROM storefront.channel_product_groups WHERE \"FirmPlatformId\"=@fp", ("fp", platformId));
        await ExecAsync(pg, "DELETE FROM storefront.channel_categories WHERE \"FirmPlatformId\"=@fp", ("fp", platformId));
        Log($"Temizlendi: {demoVariantIds.Count} demo varyant + kanal verisi.");
    }

    static async Task<Guid> UpsertFirmAsync(NpgsqlConnection pg)
    {
        var existing = await ScalarGuidAsync(pg, "SELECT \"Id\" FROM core.core_firms WHERE \"Code\"='telemania' AND \"IsDeleted\"=false");
        if (existing is Guid g) return g;
        var id = NewId();
        await ExecAsync(pg, @"INSERT INTO core.core_firms
            (""Id"",""Code"",""NameI18n"",""TaxOffice"",""TaxNumber"",""Address"",""Phone"",""Email"",""IsMain"",""IsActive"",""CreatedAt"",""IsDeleted"")
            VALUES (@id,'telemania',@name::jsonb,'','','','','',FALSE,TRUE,@now,FALSE)",
            ("id", id), ("name", I18n("Telemania")), ("now", Now));
        return id;
    }

    static async Task<Guid> UpsertFirmPlatformAsync(NpgsqlConnection pg, Guid firmId, Guid siteTypeId)
    {
        var settings = Jsonb(new Dictionary<string, object>
        {
            ["domain"] = "telemania.ecspros.com",
            ["theme"] = "misharix",
        });
        var existing = await ScalarGuidAsync(pg, "SELECT \"Id\" FROM core.core_firm_platforms WHERE \"Code\"='telemania' AND \"IsDeleted\"=false");
        if (existing is Guid g)
        {
            await ExecAsync(pg, "UPDATE core.core_firm_platforms SET \"Settings\"=@s::jsonb, \"IsActive\"=TRUE WHERE \"Id\"=@id", ("s", settings), ("id", g));
            return g;
        }
        var id = NewId();
        await ExecAsync(pg, @"INSERT INTO core.core_firm_platforms
            (""Id"",""FirmId"",""PlatformTypeId"",""Code"",""NameI18n"",""Credentials"",""Settings"",""IsActive"",""CreatedAt"",""IsDeleted"")
            VALUES (@id,@fid,@ptid,'telemania',@name::jsonb,'{}'::jsonb,@s::jsonb,TRUE,@now,FALSE)",
            ("id", id), ("fid", firmId), ("ptid", siteTypeId), ("name", I18n("Telemania")), ("s", settings), ("now", Now));
        return id;
    }

    static async Task<(Guid WarehouseId, Guid SectionId, Guid BinId)> EnsureWarehouseStructureAsync(NpgsqlConnection pg)
    {
        // Depo
        Guid warehouseId;
        var wh = await ScalarGuidAsync(pg, "SELECT \"Id\" FROM inventory.inv_warehouses WHERE \"Code\"='telemania' AND \"IsDeleted\"=false");
        if (wh is Guid wg) warehouseId = wg;
        else
        {
            warehouseId = NewId();
            await ExecAsync(pg, @"INSERT INTO inventory.inv_warehouses
                (""Id"",""Code"",""NameI18n"",""WarehouseType"",""IsSellableOnline"",""ReservePriority"",""IsActive"",""SortOrder"",""IsCentral"",""ErpCode"",""CreatedAt"",""IsDeleted"")
                VALUES (@id,'telemania',@name::jsonb,'depo',TRUE,1,TRUE,1,FALSE,NULL,@now,FALSE)",
                ("id", warehouseId), ("name", I18n("Telemania Depo")), ("now", Now));
        }

        // Kısım (satışa açık)
        Guid sectionId;
        var sec = await ScalarGuidAsync(pg, "SELECT \"Id\" FROM inventory.inv_warehouse_sections WHERE \"WarehouseId\"=@wh AND \"Code\"='telemania-main' AND \"IsDeleted\"=false", ("wh", warehouseId));
        if (sec is Guid sg) sectionId = sg;
        else
        {
            sectionId = NewId();
            await ExecAsync(pg, @"INSERT INTO inventory.inv_warehouse_sections
                (""Id"",""WarehouseId"",""Code"",""Name"",""IsSellableOnline"",""PickingOrder"",""IsActive"",""SortOrder"",""CreatedAt"",""IsDeleted"")
                VALUES (@id,@wh,'telemania-main','Ana Kısım',TRUE,1,TRUE,1,@now,FALSE)",
                ("id", sectionId), ("wh", warehouseId), ("now", Now));
        }

        // Raf (bin)
        Guid binId;
        var bin = await ScalarGuidAsync(pg, "SELECT \"Id\" FROM inventory.inv_warehouse_bins WHERE \"SectionId\"=@sec AND \"Code\"='telemania-main-01' AND \"IsDeleted\"=false", ("sec", sectionId));
        if (bin is Guid bg) binId = bg;
        else
        {
            binId = NewId();
            await ExecAsync(pg, @"INSERT INTO inventory.inv_warehouse_bins
                (""Id"",""SectionId"",""Code"",""Barcode"",""Name"",""PickingOrder"",""IsActive"",""SortOrder"",""CreatedAt"",""IsDeleted"")
                VALUES (@id,@sec,'telemania-main-01','telemania-main-01','Ana Raf',1,TRUE,1,@now,FALSE)",
                ("id", binId), ("sec", sectionId), ("now", Now));
        }

        return (warehouseId, sectionId, binId);
    }

    static async Task<Guid?> ScalarGuidAsync(NpgsqlConnection pg, string sql, params (string name, object? value)[] parameters)
    {
        await using var cmd = new NpgsqlCommand(sql, pg) { CommandTimeout = 120 };
        foreach (var (n, v) in parameters) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        var r = await cmd.ExecuteScalarAsync();
        return r is DBNull or null ? null : (Guid)r;
    }

    static async Task ExecAsync(NpgsqlConnection pg, string sql, params (string name, object? value)[] parameters)
    {
        await using var cmd = new NpgsqlCommand(sql, pg) { CommandTimeout = 120 };
        foreach (var (n, v) in parameters) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    static async Task BatchAsync(NpgsqlConnection pg, string table, string[] cols, string?[] casts, List<object?[]> rows)
    {
        if (rows.Count == 0) return;
        var sb = new StringBuilder();
        sb.Append("INSERT INTO ").Append(table).Append(" (\"").Append(string.Join("\",\"", cols)).Append("\") VALUES ");
        await using var cmd = new NpgsqlCommand { Connection = pg, CommandTimeout = 120 };
        int p = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('(');
            var row = rows[i];
            for (int c = 0; c < cols.Length; c++)
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
        await cmd.ExecuteNonQueryAsync();
    }
}
