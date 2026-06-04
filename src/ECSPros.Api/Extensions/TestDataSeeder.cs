using ECSPros.Catalog.Domain.Entities;
using ECSPros.Catalog.Infrastructure.Persistence;
using ECSPros.Cms.Infrastructure.Persistence;
using ECSPros.Core.Domain.Entities;
using ECSPros.Core.Infrastructure.Persistence;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Inventory.Infrastructure.Persistence;
using ECSPros.Storefront.Domain.Entities;
using ECSPros.Storefront.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Extensions;

/// <summary>
/// Test / geliştirme ortamı için demo verisi oluşturur.
/// Mevcut iş verilerini temizler; sistem referans verileri korunur.
/// </summary>
public static class TestDataSeeder
{
    public static async Task ResetAndSeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        Console.WriteLine("=== TestDataSeeder: iş verileri temizleniyor... ===");
        await ClearBusinessDataAsync(sp);

        Console.WriteLine("=== TestDataSeeder: demo verileri oluşturuluyor... ===");
        await SeedDemoDataAsync(sp);

        Console.WriteLine("=== TestDataSeeder: tamamlandı ===");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TRUNCATE
    // ─────────────────────────────────────────────────────────────────────────

    private static async Task ClearBusinessDataAsync(IServiceProvider sp)
    {
        var catalogDb   = sp.GetRequiredService<CatalogDbContext>();
        var cmsDb       = sp.GetRequiredService<CmsDbContext>();
        var inventoryDb = sp.GetRequiredService<InventoryDbContext>();
        var coreDb      = sp.GetRequiredService<CoreDbContext>();

        // CMS
        await cmsDb.Database.ExecuteSqlRawAsync(@"
            TRUNCATE TABLE
                cms.cms_page_section_items,
                cms.cms_page_sections,
                cms.cms_product_list_items,
                cms.cms_product_lists,
                cms.cms_pages
            RESTART IDENTITY CASCADE;");
        Console.WriteLine("  ✓ CMS tabloları temizlendi.");

        // Storefront
        var storefrontDb = sp.GetRequiredService<StorefrontDbContext>();
        await storefrontDb.Database.ExecuteSqlRawAsync(@"
            TRUNCATE TABLE
                storefront.channel_products,
                storefront.nav_nodes,
                storefront.nav_menus
            RESTART IDENTITY CASCADE;");
        Console.WriteLine("  ✓ Storefront tabloları temizlendi.");

        // Catalog
        await catalogDb.Database.ExecuteSqlRawAsync(@"
            TRUNCATE TABLE
                catalog.catalog_product_variant_images,
                catalog.catalog_product_variant_attributes,
                catalog.catalog_product_units,
                catalog.catalog_variant_price_history,
                catalog.catalog_firm_platform_variants,
                catalog.catalog_product_image_set_mappings,
                catalog.catalog_product_images,
                catalog.catalog_image_sets,
                catalog.catalog_product_videos,
                catalog.catalog_firm_platform_products,
                catalog.catalog_category_products,
                catalog.catalog_product_attributes,
                catalog.catalog_product_variants,
                catalog.catalog_products,
                catalog.catalog_product_group_axis_sub_attributes,
                catalog.catalog_product_group_attributes,
                catalog.catalog_product_groups,
                catalog.catalog_attribute_value_properties,
                catalog.catalog_attribute_values,
                catalog.catalog_attribute_types
            RESTART IDENTITY CASCADE;");
        Console.WriteLine("  ✓ Catalog tabloları temizlendi.");

        // Inventory
        await inventoryDb.Database.ExecuteSqlRawAsync(@"
            TRUNCATE TABLE
                inventory.inv_stock_reservations,
                inventory.inv_transfer_tracking,
                inventory.inv_transfer_request_items,
                inventory.inv_transfer_requests,
                inventory.inv_stock_movements,
                inventory.inv_stocks,
                inventory.inv_warehouse_locations,
                inventory.inv_warehouses
            RESTART IDENTITY CASCADE;");
        Console.WriteLine("  ✓ Inventory tabloları temizlendi.");

        // Core — firms + platforms (sistem tipleri korunur)
        await coreDb.Database.ExecuteSqlRawAsync(@"
            TRUNCATE TABLE
                core.core_cargo_rules,
                core.core_firm_notification_settings,
                core.core_firm_integrations,
                core.core_firm_platforms,
                core.core_firms
            RESTART IDENTITY CASCADE;");
        Console.WriteLine("  ✓ Core firma tabloları temizlendi.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SEED
    // ─────────────────────────────────────────────────────────────────────────

    private static async Task SeedDemoDataAsync(IServiceProvider sp)
    {
        var coreDb      = sp.GetRequiredService<CoreDbContext>();
        var catalogDb   = sp.GetRequiredService<CatalogDbContext>();
        var inventoryDb = sp.GetRequiredService<InventoryDbContext>();
        var storefrontDb2 = sp.GetRequiredService<StorefrontDbContext>();

        // Sıralı — her adım öncekine bağlı
        var (firm, webPlatform)                = await SeedFirmAndPlatformsAsync(coreDb);
        var atTypes                            = await SeedAttributeTypesAsync(catalogDb);
        var groups                             = await SeedProductGroupsAsync(catalogDb, atTypes);
        var (products, variantMap)             = await SeedProductsAsync(catalogDb, groups, atTypes, webPlatform.Id);
        var warehouse                          = await SeedWarehouseAndStocksAsync(inventoryDb, variantMap);
        await SeedMenusAsync(storefrontDb2, webPlatform.Id);

        Console.WriteLine($"  ✓ Firma + {2} satış kanalı");
        Console.WriteLine($"  ✓ {atTypes.Count} özellik tipi");
        Console.WriteLine($"  ✓ {groups.Count} ürün grubu");
        Console.WriteLine($"  ✓ {products.Count} ürün, {variantMap.Count} varyant");
        Console.WriteLine($"  ✓ Depo: {warehouse.Code}");
        Console.WriteLine($"  ✓ Menüler oluşturuldu.");
    }

    // ── Firma + Satış Kanalları ──────────────────────────────────────────────

    private static async Task<(Firm firm, FirmPlatform webPlatform)> SeedFirmAndPlatformsAsync(CoreDbContext db)
    {
        var platformTypes = await db.PlatformTypes.ToDictionaryAsync(p => p.Code);

        var firm = new Firm
        {
            Code           = "demo",
            NameI18n       = new() { { "tr", "Demo Mağaza" }, { "en", "Demo Store" } },
            TaxOffice      = "Kadıköy",
            TaxNumber      = "1234567890",
            Address        = "Bağdat Caddesi No:123 Kadıköy/İstanbul",
            Phone          = "+90 212 000 0000",
            Email          = "demo@ecspros.com",
            IsMain         = true,
            PriceType      = "manual",
            IsActive       = true,
        };
        db.Firms.Add(firm);
        await db.SaveChangesAsync();

        FirmPlatform MakePlatform(string code, string trName, string enName, string ptCode) =>
            new()
            {
                FirmId         = firm.Id,
                PlatformTypeId = platformTypes[ptCode].Id,
                Code           = code,
                NameI18n       = new() { { "tr", trName }, { "en", enName } },
                Credentials    = new(),
                Settings       = new(),
                IsActive       = true,
            };

        var webPlatform        = MakePlatform("demo-web",         "Web Sitesi",   "Website",    "site");
        var trendyolPlatform   = MakePlatform("demo-trendyol",    "Trendyol",     "Trendyol",   "trendyol");
        var hepsibPlatform     = MakePlatform("demo-hepsiburada", "Hepsiburada",  "Hepsiburada","hepsiburada");
        var mobilePlatform     = MakePlatform("demo-mobile",      "Mobil Uygulama","Mobile App","mobile_app");

        db.FirmPlatforms.AddRange(webPlatform, trendyolPlatform, hepsibPlatform, mobilePlatform);
        await db.SaveChangesAsync();

        return (firm, webPlatform);
    }

    // ── Özellik Tipleri + Değerleri ──────────────────────────────────────────

    private static async Task<Dictionary<string, AttributeType>> SeedAttributeTypesAsync(CatalogDbContext db)
    {
        AttributeType MakeType(string code, string trName, string enName, string dataType, int order) =>
            new()
            {
                Code      = code,
                NameI18n  = new() { { "tr", trName }, { "en", enName } },
                DataType  = dataType,
                IsActive  = true,
                SortOrder = order,
            };

        AttributeValue MakeVal(AttributeType type, string tr, string en, int order) =>
            new()
            {
                AttributeTypeId = type.Id,
                NameI18n        = new() { { "tr", tr }, { "en", en } },
                IsActive        = true,
                SortOrder       = order,
            };

        // ── Renk ─────────────────────────────────────────────────────────────
        var renk = MakeType("renk", "Renk", "Color", "select", 1);
        db.AttributeTypes.Add(renk);
        await db.SaveChangesAsync();

        var renkValues = new[]
        {
            MakeVal(renk, "Beyaz",       "White",      1),
            MakeVal(renk, "Siyah",       "Black",      2),
            MakeVal(renk, "Gri",         "Grey",       3),
            MakeVal(renk, "Lacivert",    "Navy",       4),
            MakeVal(renk, "Mavi",        "Blue",       5),
            MakeVal(renk, "Kırmızı",     "Red",        6),
            MakeVal(renk, "Yeşil",       "Green",      7),
            MakeVal(renk, "Sarı",        "Yellow",     8),
            MakeVal(renk, "Kahverengi",  "Brown",      9),
            MakeVal(renk, "Bej",         "Beige",      10),
            MakeVal(renk, "Pembe",       "Pink",       11),
            MakeVal(renk, "Mor",         "Purple",     12),
        };
        db.AttributeValues.AddRange(renkValues);

        // ── Giyim Bedeni ──────────────────────────────────────────────────────
        var bedenGiyim = MakeType("beden-giyim", "Beden", "Size", "select", 2);
        db.AttributeTypes.Add(bedenGiyim);
        await db.SaveChangesAsync();

        var bedenGiyimValues = new[] { "XS", "S", "M", "L", "XL", "XXL", "3XL" }
            .Select((s, i) => MakeVal(bedenGiyim, s, s, i + 1)).ToArray();
        db.AttributeValues.AddRange(bedenGiyimValues);

        // ── Ayakkabı Bedeni ───────────────────────────────────────────────────
        var bedenAyakkabi = MakeType("beden-ayakkabi", "Ayakkabı Bedeni", "Shoe Size", "select", 3);
        db.AttributeTypes.Add(bedenAyakkabi);
        await db.SaveChangesAsync();

        var bedenAyakkabiValues = new[] { "35", "36", "37", "38", "39", "40", "41", "42", "43", "44", "45" }
            .Select((s, i) => MakeVal(bedenAyakkabi, s, s, i + 1)).ToArray();
        db.AttributeValues.AddRange(bedenAyakkabiValues);

        // ── Malzeme ───────────────────────────────────────────────────────────
        var malzeme = MakeType("malzeme", "Malzeme", "Material", "multi_select", 4);
        db.AttributeTypes.Add(malzeme);
        await db.SaveChangesAsync();

        var malzemeValues = new[]
        {
            MakeVal(malzeme, "%100 Pamuk",      "100% Cotton",    1),
            MakeVal(malzeme, "Pamuk Karışım",   "Cotton Blend",   2),
            MakeVal(malzeme, "Polyester",        "Polyester",      3),
            MakeVal(malzeme, "Viskon",           "Viscose",        4),
            MakeVal(malzeme, "Denim",            "Denim",          5),
            MakeVal(malzeme, "Deri",             "Leather",        6),
            MakeVal(malzeme, "Keten",            "Linen",          7),
            MakeVal(malzeme, "Modal",            "Modal",          8),
        };
        db.AttributeValues.AddRange(malzemeValues);

        // ── Cinsiyet ──────────────────────────────────────────────────────────
        var cinsiyetUrun = MakeType("cinsiyet-urun", "Hedef Kitle", "Target Audience", "select", 5);
        db.AttributeTypes.Add(cinsiyetUrun);
        await db.SaveChangesAsync();

        var cinsiyetValues = new[]
        {
            MakeVal(cinsiyetUrun, "Erkek",  "Men",   1),
            MakeVal(cinsiyetUrun, "Kadın",  "Women", 2),
            MakeVal(cinsiyetUrun, "Unisex", "Unisex",3),
            MakeVal(cinsiyetUrun, "Çocuk",  "Kids",  4),
        };
        db.AttributeValues.AddRange(cinsiyetValues);

        // ── Sezon ─────────────────────────────────────────────────────────────
        var sezon = MakeType("sezon", "Sezon", "Season", "select", 6);
        db.AttributeTypes.Add(sezon);
        await db.SaveChangesAsync();

        var sezonValues = new[]
        {
            MakeVal(sezon, "İlkbahar/Yaz",  "Spring/Summer", 1),
            MakeVal(sezon, "Sonbahar/Kış",  "Autumn/Winter", 2),
            MakeVal(sezon, "Tüm Sezon",     "All Season",    3),
        };
        db.AttributeValues.AddRange(sezonValues);

        await db.SaveChangesAsync();

        return new Dictionary<string, AttributeType>
        {
            ["renk"]          = renk,
            ["beden-giyim"]   = bedenGiyim,
            ["beden-ayakkabi"]= bedenAyakkabi,
            ["malzeme"]       = malzeme,
            ["cinsiyet-urun"] = cinsiyetUrun,
            ["sezon"]         = sezon,
        };
    }

    // ── Ürün Grupları ────────────────────────────────────────────────────────

    private static async Task<Dictionary<string, ProductGroup>> SeedProductGroupsAsync(
        CatalogDbContext db, Dictionary<string, AttributeType> at)
    {
        ProductGroup MakeGroup(string code, string tr, string en, int order) =>
            new()
            {
                Code      = code,
                NameI18n  = new() { { "tr", tr }, { "en", en } },
                IsActive  = true,
                SortOrder = order,
            };

        ProductGroupAttribute MakeAttr(ProductGroup g, AttributeType a, bool isVariant, bool isPrimary = false, int order = 0) =>
            new()
            {
                ProductGroupId  = g.Id,
                AttributeTypeId = a.Id,
                IsVariant       = isVariant,
                IsPrimaryAxis   = isPrimary,
                IsRequired      = isVariant,
                SortOrder       = order,
            };

        var erkekTisort    = MakeGroup("erkek-tisort",   "Erkek T-Shirt",  "Men's T-Shirt",  1);
        var kadinTisort    = MakeGroup("kadin-tisort",   "Kadın T-Shirt",  "Women's T-Shirt",2);
        var erkekGomlek    = MakeGroup("erkek-gomlek",   "Erkek Gömlek",   "Men's Shirt",    3);
        var kadinElbise    = MakeGroup("kadin-elbise",   "Kadın Elbise",   "Women's Dress",  4);
        var sneaker        = MakeGroup("unisex-sneaker", "Sneaker",        "Sneaker",        5);
        var kadinCanta     = MakeGroup("kadin-canta",    "Kadın Çanta",    "Women's Bag",    6);
        var erkekEsofman   = MakeGroup("erkek-esofman",  "Erkek Eşofman",  "Men's Tracksuit",7);

        db.ProductGroups.AddRange(erkekTisort, kadinTisort, erkekGomlek, kadinElbise, sneaker, kadinCanta, erkekEsofman);
        await db.SaveChangesAsync();

        db.ProductGroupAttributes.AddRange(
            // Erkek T-Shirt
            MakeAttr(erkekTisort, at["renk"],          true,  true,  1),
            MakeAttr(erkekTisort, at["beden-giyim"],   true,  false, 2),
            MakeAttr(erkekTisort, at["malzeme"],       false, false, 3),
            MakeAttr(erkekTisort, at["cinsiyet-urun"], false, false, 4),
            MakeAttr(erkekTisort, at["sezon"],         false, false, 5),

            // Kadın T-Shirt
            MakeAttr(kadinTisort, at["renk"],          true,  true,  1),
            MakeAttr(kadinTisort, at["beden-giyim"],   true,  false, 2),
            MakeAttr(kadinTisort, at["malzeme"],       false, false, 3),
            MakeAttr(kadinTisort, at["sezon"],         false, false, 4),

            // Erkek Gömlek
            MakeAttr(erkekGomlek, at["renk"],          true,  true,  1),
            MakeAttr(erkekGomlek, at["beden-giyim"],   true,  false, 2),
            MakeAttr(erkekGomlek, at["malzeme"],       false, false, 3),
            MakeAttr(erkekGomlek, at["cinsiyet-urun"], false, false, 4),

            // Kadın Elbise
            MakeAttr(kadinElbise, at["renk"],          true,  true,  1),
            MakeAttr(kadinElbise, at["beden-giyim"],   true,  false, 2),
            MakeAttr(kadinElbise, at["malzeme"],       false, false, 3),
            MakeAttr(kadinElbise, at["sezon"],         false, false, 4),

            // Sneaker
            MakeAttr(sneaker, at["renk"],              true,  true,  1),
            MakeAttr(sneaker, at["beden-ayakkabi"],    true,  false, 2),
            MakeAttr(sneaker, at["malzeme"],           false, false, 3),

            // Kadın Çanta
            MakeAttr(kadinCanta, at["renk"],           true,  true,  1),
            MakeAttr(kadinCanta, at["malzeme"],        false, false, 2),

            // Erkek Eşofman
            MakeAttr(erkekEsofman, at["renk"],         true,  true,  1),
            MakeAttr(erkekEsofman, at["beden-giyim"],  true,  false, 2),
            MakeAttr(erkekEsofman, at["malzeme"],      false, false, 3)
        );
        await db.SaveChangesAsync();

        return new Dictionary<string, ProductGroup>
        {
            ["erkek-tisort"]   = erkekTisort,
            ["kadin-tisort"]   = kadinTisort,
            ["erkek-gomlek"]   = erkekGomlek,
            ["kadin-elbise"]   = kadinElbise,
            ["unisex-sneaker"] = sneaker,
            ["kadin-canta"]    = kadinCanta,
            ["erkek-esofman"]  = erkekEsofman,
        };
    }

    // ── Ürünler + Varyantlar ─────────────────────────────────────────────────

    private static async Task<(List<Product>, List<ProductVariant>)> SeedProductsAsync(
        CatalogDbContext db,
        Dictionary<string, ProductGroup> groups,
        Dictionary<string, AttributeType> at,
        Guid firmPlatformId)
    {
        var allValues = await db.AttributeValues.ToListAsync();

        // ── Renk değerleri ────────────────────────────────────────────────────
        var vBeyaz      = allValues.First(v => v.AttributeTypeId == at["renk"].Id && v.NameI18n["tr"] == "Beyaz");
        var vSiyah      = allValues.First(v => v.AttributeTypeId == at["renk"].Id && v.NameI18n["tr"] == "Siyah");
        var vGri        = allValues.First(v => v.AttributeTypeId == at["renk"].Id && v.NameI18n["tr"] == "Gri");
        var vLacivert   = allValues.First(v => v.AttributeTypeId == at["renk"].Id && v.NameI18n["tr"] == "Lacivert");
        var vMavi       = allValues.First(v => v.AttributeTypeId == at["renk"].Id && v.NameI18n["tr"] == "Mavi");
        var vKirmizi    = allValues.First(v => v.AttributeTypeId == at["renk"].Id && v.NameI18n["tr"] == "Kırmızı");
        var vKahverengi = allValues.First(v => v.AttributeTypeId == at["renk"].Id && v.NameI18n["tr"] == "Kahverengi");
        var vBej        = allValues.First(v => v.AttributeTypeId == at["renk"].Id && v.NameI18n["tr"] == "Bej");
        var vPembe      = allValues.First(v => v.AttributeTypeId == at["renk"].Id && v.NameI18n["tr"] == "Pembe");

        // ── Giyim beden değerleri ─────────────────────────────────────────────
        var vXS  = allValues.First(v => v.AttributeTypeId == at["beden-giyim"].Id && v.NameI18n["tr"] == "XS");
        var vS   = allValues.First(v => v.AttributeTypeId == at["beden-giyim"].Id && v.NameI18n["tr"] == "S");
        var vM   = allValues.First(v => v.AttributeTypeId == at["beden-giyim"].Id && v.NameI18n["tr"] == "M");
        var vL   = allValues.First(v => v.AttributeTypeId == at["beden-giyim"].Id && v.NameI18n["tr"] == "L");
        var vXL  = allValues.First(v => v.AttributeTypeId == at["beden-giyim"].Id && v.NameI18n["tr"] == "XL");
        var vXXL = allValues.First(v => v.AttributeTypeId == at["beden-giyim"].Id && v.NameI18n["tr"] == "XXL");

        // ── Ayakkabı beden değerleri ──────────────────────────────────────────
        var v38 = allValues.First(v => v.AttributeTypeId == at["beden-ayakkabi"].Id && v.NameI18n["tr"] == "38");
        var v39 = allValues.First(v => v.AttributeTypeId == at["beden-ayakkabi"].Id && v.NameI18n["tr"] == "39");
        var v40 = allValues.First(v => v.AttributeTypeId == at["beden-ayakkabi"].Id && v.NameI18n["tr"] == "40");
        var v41 = allValues.First(v => v.AttributeTypeId == at["beden-ayakkabi"].Id && v.NameI18n["tr"] == "41");
        var v42 = allValues.First(v => v.AttributeTypeId == at["beden-ayakkabi"].Id && v.NameI18n["tr"] == "42");
        var v43 = allValues.First(v => v.AttributeTypeId == at["beden-ayakkabi"].Id && v.NameI18n["tr"] == "43");

        // ── Malzeme değerleri ─────────────────────────────────────────────────
        var vPamuk       = allValues.First(v => v.AttributeTypeId == at["malzeme"].Id && v.NameI18n["tr"] == "%100 Pamuk");
        var vPamukKarisim= allValues.First(v => v.AttributeTypeId == at["malzeme"].Id && v.NameI18n["tr"] == "Pamuk Karışım");
        var vDeri        = allValues.First(v => v.AttributeTypeId == at["malzeme"].Id && v.NameI18n["tr"] == "Deri");
        var vDenim       = allValues.First(v => v.AttributeTypeId == at["malzeme"].Id && v.NameI18n["tr"] == "Denim");

        // ── Cinsiyet değerleri ────────────────────────────────────────────────
        var vErkekTarget  = allValues.First(v => v.AttributeTypeId == at["cinsiyet-urun"].Id && v.NameI18n["tr"] == "Erkek");
        var vKadinTarget  = allValues.First(v => v.AttributeTypeId == at["cinsiyet-urun"].Id && v.NameI18n["tr"] == "Kadın");
        var vUnisexTarget = allValues.First(v => v.AttributeTypeId == at["cinsiyet-urun"].Id && v.NameI18n["tr"] == "Unisex");

        // ── Sezon değerleri ───────────────────────────────────────────────────
        var vTumSezon    = allValues.First(v => v.AttributeTypeId == at["sezon"].Id && v.NameI18n["tr"] == "Tüm Sezon");
        var vIlkYaz      = allValues.First(v => v.AttributeTypeId == at["sezon"].Id && v.NameI18n["tr"] == "İlkbahar/Yaz");
        var vSonKis      = allValues.First(v => v.AttributeTypeId == at["sezon"].Id && v.NameI18n["tr"] == "Sonbahar/Kış");

        // ─────────────────────────────────────────────────────────────────────
        // Yardımcı: Ürün + Varyant + FirmPlatformProduct + FirmPlatformVariant

        var allProducts = new List<Product>();
        var allVariants = new List<ProductVariant>();

        async Task<(Product prod, List<ProductVariant> variants)> AddProduct(
            string code, string trName, string enName,
            string shortDescTr, string shortDescEn,
            string groupCode, decimal basePrice, decimal baseCost, int taxRate,
            (AttributeValue renk, AttributeValue beden)[] variantCombos,
            (AttributeType type, AttributeValue val)[] productAttrs)
        {
            var product = new Product
            {
                Code                = code,
                NameI18n            = new() { { "tr", trName }, { "en", enName } },
                ShortDescriptionI18n= new() { { "tr", shortDescTr }, { "en", shortDescEn } },
                DescriptionI18n     = new() { { "tr", $"{trName} ürün açıklaması. Kaliteli malzeme ve özenli işçilik." }, { "en", $"{enName} product description. Quality material and craftsmanship." } },
                ProductGroupId      = groups[groupCode].Id,
                BasePrice           = basePrice,
                BaseCost            = baseCost,
                TaxRate             = taxRate,
                IsActive            = true,
            };
            db.Products.Add(product);
            await db.SaveChangesAsync();

            // Ürün özellikleri
            foreach (var (type, val) in productAttrs)
            {
                db.ProductAttributes.Add(new ProductAttribute
                {
                    ProductId       = product.Id,
                    AttributeTypeId = type.Id,
                    AttributeValueId= val.Id,
                });
            }

            // Varyantlar
            var variants = new List<ProductVariant>();
            int barcodeSeed = Math.Abs(code.GetHashCode()) % 10000000;
            int barcodeIdx = 0;

            foreach (var (renkVal, bedenVal) in variantCombos)
            {
                var bedenCode = bedenVal.NameI18n["tr"].Replace("/", "").Replace(" ", "").ToUpper()[..Math.Min(3, bedenVal.NameI18n["tr"].Length)];
                var renkCode  = renkVal.NameI18n["tr"].Replace(" ", "").ToUpper()[..Math.Min(3, renkVal.NameI18n["tr"].Length)];
                var sku = $"{code}-{renkCode}-{bedenCode}";

                var variant = new ProductVariant
                {
                    ProductId = product.Id,
                    Sku       = sku,
                    Barcode   = $"868{barcodeSeed:D7}{barcodeIdx++:D2}",
                    BasePrice = basePrice,
                    BaseCost  = baseCost,
                    IsActive  = true,
                };
                db.ProductVariants.Add(variant);
                await db.SaveChangesAsync();

                // Varyant özellikleri: renk + beden
                db.ProductVariantAttributes.AddRange(
                    new ProductVariantAttribute
                    {
                        VariantId       = variant.Id,
                        AttributeTypeId = at["renk"].Id,
                        AttributeValueId= renkVal.Id,
                    },
                    new ProductVariantAttribute
                    {
                        VariantId       = variant.Id,
                        AttributeTypeId = bedenVal.AttributeTypeId,
                        AttributeValueId= bedenVal.Id,
                    }
                );

                // FirmPlatformVariant (web sitesi için)
                db.FirmPlatformVariants.Add(new FirmPlatformVariant
                {
                    FirmPlatformId = firmPlatformId,
                    VariantId      = variant.Id,
                    PriceType      = "manual",
                    Price          = basePrice,
                    IsActive       = true,
                });

                variants.Add(variant);
            }

            // FirmPlatformProduct
            db.FirmPlatformProducts.Add(new FirmPlatformProduct
            {
                FirmPlatformId = firmPlatformId,
                ProductId      = product.Id,
                IsActive       = true,
            });

            await db.SaveChangesAsync();

            allProducts.Add(product);
            allVariants.AddRange(variants);
            return (product, variants);
        }

        // ── Ürün 1: Basic Erkek Tişört ────────────────────────────────────────
        await AddProduct(
            "PROD-001", "Basic Erkek Tişört", "Basic Men's T-Shirt",
            "Rahat ve şık basic tişört", "Comfortable and stylish basic tee",
            "erkek-tisort", 299.90m, 89.90m, 18,
            new[]
            {
                (vSiyah, vS), (vSiyah, vM), (vSiyah, vL), (vSiyah, vXL),
                (vBeyaz, vS), (vBeyaz, vM), (vBeyaz, vL), (vBeyaz, vXL),
                (vGri,   vM), (vGri,   vL),
                (vLacivert, vM), (vLacivert, vL),
            },
            new[] { (at["malzeme"], vPamuk), (at["cinsiyet-urun"], vErkekTarget), (at["sezon"], vTumSezon) }
        );

        // ── Ürün 2: Oxford Erkek Gömlek ──────────────────────────────────────
        await AddProduct(
            "PROD-002", "Oxford Erkek Gömlek", "Oxford Men's Shirt",
            "Klasik Oxford dokuma gömlek", "Classic Oxford weave shirt",
            "erkek-gomlek", 599.90m, 179.90m, 18,
            new[]
            {
                (vBeyaz, vM), (vBeyaz, vL), (vBeyaz, vXL),
                (vMavi,  vM), (vMavi,  vL), (vMavi,  vXL),
                (vLacivert, vM), (vLacivert, vL),
            },
            new[] { (at["malzeme"], vPamukKarisim), (at["cinsiyet-urun"], vErkekTarget), (at["sezon"], vTumSezon) }
        );

        // ── Ürün 3: Yazlık Kadın Elbise ──────────────────────────────────────
        await AddProduct(
            "PROD-003", "Yazlık Kadın Elbise", "Women's Summer Dress",
            "Hafif ve şık yazlık elbise", "Light and stylish summer dress",
            "kadin-elbise", 799.90m, 219.90m, 18,
            new[]
            {
                (vKirmizi, vS), (vKirmizi, vM), (vKirmizi, vL),
                (vLacivert, vS), (vLacivert, vM),
                (vBeyaz, vS), (vBeyaz, vM), (vBeyaz, vL),
                (vBej, vS), (vBej, vM),
            },
            new[] { (at["malzeme"], vPamuk), (at["sezon"], vIlkYaz) }
        );

        // ── Ürün 4: Unisex Spor Sneaker ──────────────────────────────────────
        await AddProduct(
            "PROD-004", "Spor Sneaker", "Sport Sneaker",
            "Hafif ve konforlu spor sneaker", "Lightweight and comfortable sport sneaker",
            "unisex-sneaker", 1299.90m, 389.90m, 18,
            new[]
            {
                (vBeyaz, v38), (vBeyaz, v39), (vBeyaz, v40), (vBeyaz, v41), (vBeyaz, v42),
                (vSiyah, v38), (vSiyah, v39), (vSiyah, v40), (vSiyah, v41), (vSiyah, v42), (vSiyah, v43),
                (vGri,   v39), (vGri,   v40), (vGri,   v41),
            },
            new[] { (at["sezon"], vTumSezon) }
        );

        // ── Ürün 5: Kadın Tişört ─────────────────────────────────────────────
        await AddProduct(
            "PROD-005", "Basic Kadın Tişört", "Basic Women's T-Shirt",
            "Günlük kullanım için rahat basic tişört", "Comfortable basic tee for everyday use",
            "kadin-tisort", 249.90m, 74.90m, 18,
            new[]
            {
                (vBeyaz, vXS), (vBeyaz, vS), (vBeyaz, vM), (vBeyaz, vL),
                (vSiyah, vXS), (vSiyah, vS), (vSiyah, vM), (vSiyah, vL),
                (vGri,   vS),  (vGri,   vM),
                (vPembe, vS),  (vPembe, vM),
                (vKirmizi, vS),(vKirmizi, vM),
            },
            new[] { (at["malzeme"], vPamuk), (at["sezon"], vTumSezon) }
        );

        // ── Ürün 6: Erkek Eşofman Takımı ─────────────────────────────────────
        await AddProduct(
            "PROD-006", "Erkek Eşofman Takımı", "Men's Tracksuit Set",
            "Rahat spor eşofman takımı", "Comfortable sport tracksuit set",
            "erkek-esofman", 899.90m, 269.90m, 18,
            new[]
            {
                (vSiyah, vS),  (vSiyah, vM),  (vSiyah, vL),  (vSiyah, vXL),
                (vGri,   vS),  (vGri,   vM),  (vGri,   vL),  (vGri,   vXL),
                (vLacivert, vM), (vLacivert, vL),
            },
            new[] { (at["malzeme"], vPamukKarisim), (at["cinsiyet-urun"], vErkekTarget), (at["sezon"], vTumSezon) }
        );

        // ── Ürün 7: Deri Kadın Çanta ─────────────────────────────────────────
        // Çanta: sadece renk ekseni (beden yok) → dummy "beden" olarak renk çiftleri
        // Farklı bir yaklaşım: sadece renk varyantı (beden yok)
        // ProductGroupAttribute'da beden-ayakkabi kullanmadık kadin-canta için
        // Çanta varyantları: her renk, "M" bedenini placeholder olarak kullanıyoruz
        // Aslında çanta grubunda sadece renk var, bu yüzden ProductGroupAttribute'da beden ekseni yok
        // Bu durumda sadece renk kombinasyonları olmalı ama method imzası (renk, beden) ikili istiyor
        // Basit çözüm: beden olarak vM kullan, sadece variant attr'da renk ekle

        var canta = new Product
        {
            Code                = "PROD-007",
            NameI18n            = new() { { "tr", "Deri Kadın Çanta" }, { "en", "Leather Women's Bag" } },
            ShortDescriptionI18n= new() { { "tr", "Hakiki deri kadın omuz çantası" }, { "en", "Genuine leather women's shoulder bag" } },
            DescriptionI18n     = new() { { "tr", "El işçiliğiyle üretilmiş hakiki deri omuz çantası." }, { "en", "Handcrafted genuine leather shoulder bag." } },
            ProductGroupId      = groups["kadin-canta"].Id,
            BasePrice           = 1799.90m,
            BaseCost            = 539.90m,
            TaxRate             = 18,
            IsActive            = true,
        };
        db.Products.Add(canta);
        await db.SaveChangesAsync();

        db.ProductAttributes.Add(new ProductAttribute
        {
            ProductId        = canta.Id,
            AttributeTypeId  = at["sezon"].Id,
            AttributeValueId = vTumSezon.Id,
        });

        var cantaRenkler = new[] { vKahverengi, vSiyah, vBej };
        int cantaIdx = 0;
        foreach (var renkVal in cantaRenkler)
        {
            var renkCode = renkVal.NameI18n["tr"].Replace(" ", "").ToUpper()[..Math.Min(3, renkVal.NameI18n["tr"].Length)];
            var sku      = $"PROD-007-{renkCode}";
            var variant  = new ProductVariant
            {
                ProductId = canta.Id,
                Sku       = sku,
                Barcode   = $"86890700{cantaIdx++:D2}",
                BasePrice = 1799.90m,
                BaseCost  = 539.90m,
                IsActive  = true,
            };
            db.ProductVariants.Add(variant);
            await db.SaveChangesAsync();

            db.ProductVariantAttributes.Add(new ProductVariantAttribute
            {
                VariantId        = variant.Id,
                AttributeTypeId  = at["renk"].Id,
                AttributeValueId = renkVal.Id,
            });
            db.FirmPlatformVariants.Add(new FirmPlatformVariant
            {
                FirmPlatformId = firmPlatformId,
                VariantId      = variant.Id,
                PriceType      = "manual",
                Price          = 1799.90m,
                IsActive       = true,
            });
            allVariants.Add(variant);
        }

        db.FirmPlatformProducts.Add(new FirmPlatformProduct
        {
            FirmPlatformId = firmPlatformId,
            ProductId      = canta.Id,
            IsActive       = true,
        });
        await db.SaveChangesAsync();
        allProducts.Add(canta);

        return (allProducts, allVariants);
    }

    // ── Depo + Stoklar ───────────────────────────────────────────────────────

    private static async Task<Warehouse> SeedWarehouseAndStocksAsync(
        InventoryDbContext db, List<ProductVariant> variants)
    {
        var warehouse = new Warehouse
        {
            Code             = "WH-001",
            NameI18n         = new() { { "tr", "Merkez Depo" }, { "en", "Central Warehouse" } },
            WarehouseType    = "main",
            Address          = "Organize Sanayi Bölgesi No:5 Pendik/İstanbul",
            IsSellableOnline = true,
            ReservePriority  = 1,
            IsActive         = true,
            SortOrder        = 1,
        };
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync();

        var stocks = variants.Select(v => new Stock
        {
            VariantId        = v.Id,
            WarehouseId      = warehouse.Id,
            StockType        = "physical",
            Quantity         = 50,
            ReservedQuantity = 0,
        }).ToList();

        db.Stocks.AddRange(stocks);
        await db.SaveChangesAsync();

        return warehouse;
    }

    // ── CMS Menüleri ─────────────────────────────────────────────────────────

    private static async Task SeedMenusAsync(
        StorefrontDbContext db, Guid firmPlatformId)
    {
        // ── Ana Navigasyon Menüsü ─────────────────────────────────────────────
        var mainMenu = new NavigationMenu
        {
            Id             = Guid.NewGuid(),
            FirmPlatformId = firmPlatformId,
            Code           = "main-nav",
            NameI18n       = new() { { "tr", "Ana Navigasyon" }, { "en", "Main Navigation" } },
            MenuType       = "header",
            IsActive       = true,
            SortOrder      = 1,
        };
        db.NavigationMenus.Add(mainMenu);
        await db.SaveChangesAsync();

        NavNode MakeNode(Guid menuId, string tr, string en, string nodeType,
            Guid? parentId = null, string? url = null, int order = 0) =>
            new()
            {
                Id                = Guid.NewGuid(),
                NavigationMenuId  = menuId,
                ParentNavNodeId   = parentId,
                NameOverrideI18n  = new() { { "tr", tr }, { "en", en } },
                NodeType          = nodeType,
                ChannelCategoryId = null,
                CustomUrl         = url,
                IsActive          = true,
                SortOrder         = order,
            };

        // L1 menü öğeleri
        var miErkek    = MakeNode(mainMenu.Id, "Erkek",    "Men",        "label", order: 1);
        var miKadin    = MakeNode(mainMenu.Id, "Kadın",    "Women",      "label", order: 2);
        var miAyakkabi = MakeNode(mainMenu.Id, "Ayakkabı", "Shoes",      "label", order: 3);
        var miSale     = MakeNode(mainMenu.Id, "İndirim",  "Sale",       "link",  url: "/indirim", order: 4);

        db.NavNodes.AddRange(miErkek, miKadin, miAyakkabi, miSale);
        await db.SaveChangesAsync();

        // L2 — Erkek alt öğeleri
        db.NavNodes.AddRange(
            MakeNode(mainMenu.Id, "T-Shirt",  "T-Shirt",   "link", miErkek.Id,    "/erkek/t-shirt",  1),
            MakeNode(mainMenu.Id, "Gömlek",   "Shirts",    "link", miErkek.Id,    "/erkek/gomlek",   2),
            MakeNode(mainMenu.Id, "Eşofman",  "Tracksuits","link", miErkek.Id,    "/erkek/esofman",  3),
            MakeNode(mainMenu.Id, "Pantolon", "Trousers",  "link", miErkek.Id,    "/erkek/pantolon", 4)
        );

        // L2 — Kadın alt öğeleri
        db.NavNodes.AddRange(
            MakeNode(mainMenu.Id, "Elbise",  "Dresses",  "link", miKadin.Id, "/kadin/elbise",  1),
            MakeNode(mainMenu.Id, "T-Shirt", "T-Shirts", "link", miKadin.Id, "/kadin/t-shirt", 2),
            MakeNode(mainMenu.Id, "Çanta",   "Bags",     "link", miKadin.Id, "/kadin/canta",   3),
            MakeNode(mainMenu.Id, "Bluz",    "Blouses",  "link", miKadin.Id, "/kadin/bluz",    4)
        );

        // L2 — Ayakkabı alt öğeleri
        db.NavNodes.AddRange(
            MakeNode(mainMenu.Id, "Spor Ayakkabı", "Sneakers", "link", miAyakkabi.Id, "/ayakkabi/spor",   1),
            MakeNode(mainMenu.Id, "Klasik",        "Classic",  "link", miAyakkabi.Id, "/ayakkabi/klasik", 2)
        );
        await db.SaveChangesAsync();

        // ── Footer Menüsü ─────────────────────────────────────────────────────
        var footerMenu = new NavigationMenu
        {
            Id             = Guid.NewGuid(),
            FirmPlatformId = firmPlatformId,
            Code           = "footer-links",
            NameI18n       = new() { { "tr", "Footer Bağlantıları" }, { "en", "Footer Links" } },
            MenuType       = "footer",
            IsActive       = true,
            SortOrder      = 2,
        };
        db.NavigationMenus.Add(footerMenu);
        await db.SaveChangesAsync();

        db.NavNodes.AddRange(
            MakeNode(footerMenu.Id, "Hakkımızda",         "About Us",       "link", url: "/hakkimizda", order: 1),
            MakeNode(footerMenu.Id, "İletişim",           "Contact",        "link", url: "/iletisim",   order: 2),
            MakeNode(footerMenu.Id, "Kargo ve Teslimat",  "Shipping Info",  "link", url: "/kargo",      order: 3),
            MakeNode(footerMenu.Id, "İade Koşulları",     "Return Policy",  "link", url: "/iade",       order: 4),
            MakeNode(footerMenu.Id, "Gizlilik Politikası","Privacy Policy", "link", url: "/gizlilik",   order: 5),
            MakeNode(footerMenu.Id, "KVKK",               "KVKK",           "link", url: "/kvkk",       order: 6)
        );

        // ── Mobil Bottom Nav ──────────────────────────────────────────────────
        var mobileMenu = new NavigationMenu
        {
            Id             = Guid.NewGuid(),
            FirmPlatformId = firmPlatformId,
            Code           = "mobile-bottom",
            NameI18n       = new() { { "tr", "Mobil Alt Navigasyon" }, { "en", "Mobile Bottom Nav" } },
            MenuType       = "custom",
            IsActive       = true,
            SortOrder      = 3,
        };
        db.NavigationMenus.Add(mobileMenu);
        await db.SaveChangesAsync();

        db.NavNodes.AddRange(
            MakeNode(mobileMenu.Id, "Anasayfa",    "Home",       "link", url: "/",            order: 1),
            MakeNode(mobileMenu.Id, "Kategoriler", "Categories", "link", url: "/kategoriler", order: 2),
            MakeNode(mobileMenu.Id, "Favoriler",   "Favorites",  "link", url: "/favoriler",   order: 3),
            MakeNode(mobileMenu.Id, "Sepet",       "Cart",       "link", url: "/sepet",       order: 4),
            MakeNode(mobileMenu.Id, "Hesabım",     "Account",    "link", url: "/hesabim",     order: 5)
        );

        await db.SaveChangesAsync();
    }
}
