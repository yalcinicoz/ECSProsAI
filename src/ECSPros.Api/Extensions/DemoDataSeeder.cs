using ECSPros.Catalog.Domain.Entities;
using ECSPros.Catalog.Infrastructure.Persistence;
using ECSPros.Core.Domain.Entities;
using ECSPros.Core.Infrastructure.Persistence;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Inventory.Infrastructure.Persistence;
using ECSPros.Storefront.Domain.Entities;
using ECSPros.Storefront.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Extensions;

/// <summary>
/// Demo verilerini seed eder. İdempotent — var olan kayıtlara dokunmaz.
/// Sadece Development ortamında otomatik çalışır; Production'da manuel tetiklenir.
/// </summary>
public static class DemoDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        // Sıralı çalışmalı: Core → Catalog → Inventory → CMS
        var firmPlatformId = await SeedFirmAsync(sp);
        if (firmPlatformId == Guid.Empty) return;

        var (attrTypes, attrValues) = await SeedAttributeTypesAndValuesAsync(sp);
        var productGroups = await SeedProductGroupsAsync(sp, attrTypes);
        var categories = await SeedCategoriesAsync(sp);
        var variants = await SeedProductsAsync(sp, productGroups, attrTypes, attrValues, categories);
        await SeedInventoryAsync(sp, variants);
        await SeedCmsMenusAsync(sp, firmPlatformId, categories);
    }

    // ─────────────────────────────────────────────────────────
    // 1. FİRMA + SATIŞ KANALLARI
    // ─────────────────────────────────────────────────────────
    private static async Task<Guid> SeedFirmAsync(IServiceProvider sp)
    {
        var ctx = sp.GetRequiredService<CoreDbContext>();

        if (await ctx.Firms.AnyAsync(f => f.Code == "demo"))
        {
            var existing = await ctx.FirmPlatforms.FirstOrDefaultAsync(fp => fp.Code == "demo_web");
            return existing?.Id ?? Guid.Empty;
        }

        // Platform type'ları al
        var sitePt    = await ctx.PlatformTypes.FirstOrDefaultAsync(p => p.Code == "site");
        var trendyolPt = await ctx.PlatformTypes.FirstOrDefaultAsync(p => p.Code == "trendyol");
        var hbPt      = await ctx.PlatformTypes.FirstOrDefaultAsync(p => p.Code == "hepsiburada");
        var mobilePt  = await ctx.PlatformTypes.FirstOrDefaultAsync(p => p.Code == "mobile_app");

        if (sitePt is null)
        {
            Console.WriteLine("⚠️  Demo seed: PlatformTypes bulunamadı, önce SeedPlatformTypesAsync çalıştırılmalı.");
            return Guid.Empty;
        }

        var firm = new Firm
        {
            Code       = "demo",
            NameI18n   = new() { { "tr", "Demo Mağaza" }, { "en", "Demo Store" } },
            TaxOffice  = "Merkez",
            TaxNumber  = "1234567890",
            Address    = "Demo Caddesi No:1, İstanbul",
            Phone      = "+90 212 000 0000",
            Email      = "info@demo.com",
            IsMain     = true,
            IsActive   = true,
        };
        ctx.Firms.Add(firm);
        await ctx.SaveChangesAsync();

        var platforms = new List<FirmPlatform>();

        void AddPlatform(PlatformType? pt, string code, string nameTr, string nameEn)
        {
            if (pt is null) return;
            platforms.Add(new FirmPlatform
            {
                FirmId         = firm.Id,
                PlatformTypeId = pt.Id,
                Code           = code,
                NameI18n       = new() { { "tr", nameTr }, { "en", nameEn } },
                Credentials    = new(),
                Settings       = new(),
                IsActive       = true,
            });
        }

        AddPlatform(sitePt,     "demo_web",        "Demo Web Sitesi",    "Demo Website");
        AddPlatform(trendyolPt, "demo_trendyol",   "Demo Trendyol",      "Demo Trendyol");
        AddPlatform(hbPt,       "demo_hepsiburada","Demo Hepsiburada",   "Demo Hepsiburada");
        AddPlatform(mobilePt,   "demo_mobile",     "Demo Mobil",         "Demo Mobile");

        ctx.FirmPlatforms.AddRange(platforms);
        await ctx.SaveChangesAsync();

        var webPlatform = platforms.First(p => p.Code == "demo_web");
        Console.WriteLine($"✓ Demo Seed: 1 firma + {platforms.Count} satış kanalı oluşturuldu.");
        return webPlatform.Id;
    }

    // ─────────────────────────────────────────────────────────
    // 2. ÖZELLİK TİPLERİ + DEĞERLERİ
    // ─────────────────────────────────────────────────────────
    private static async Task<(Dictionary<string, AttributeType> types, Dictionary<string, Dictionary<string, AttributeValue>> values)>
        SeedAttributeTypesAndValuesAsync(IServiceProvider sp)
    {
        var ctx = sp.GetRequiredService<CatalogDbContext>();

        var typeCodes = new[] { "color", "size", "shoe_size", "material", "target_audience", "season" };
        var existingTypes = await ctx.AttributeTypes
            .Where(a => typeCodes.Contains(a.Code))
            .ToDictionaryAsync(a => a.Code);

        if (existingTypes.Count == typeCodes.Length)
        {
            // Zaten var, değerleri de yükle
            var existingValues = await ctx.AttributeValues
                .Where(v => typeCodes.Contains(v.AttributeType.Code))
                .Include(v => v.AttributeType)
                .ToListAsync();
            var valDict = new Dictionary<string, Dictionary<string, AttributeValue>>();
            foreach (var v in existingValues)
            {
                var code = v.AttributeType.Code;
                if (!valDict.ContainsKey(code)) valDict[code] = new();
                var key = v.NameI18n.GetValueOrDefault("tr") ?? v.Id.ToString();
                valDict[code][key] = v;
            }
            return (existingTypes, valDict);
        }

        var typesList = new List<AttributeType>();

        AttributeType MakeType(string code, string nameTr, string nameEn, string dataType = "select", int sort = 0)
        {
            if (existingTypes.TryGetValue(code, out var existing)) return existing;
            var t = new AttributeType
            {
                Code      = code,
                NameI18n  = new() { { "tr", nameTr }, { "en", nameEn } },
                DataType  = dataType,
                IsActive  = true,
                SortOrder = sort,
            };
            typesList.Add(t);
            return t;
        }

        var tColor    = MakeType("color",           "Renk",           "Color",           "select", 1);
        var tSize     = MakeType("size",            "Beden",          "Size",            "select", 2);
        var tShoeSize = MakeType("shoe_size",       "Ayakkabı Bedeni","Shoe Size",       "select", 3);
        var tMaterial = MakeType("material",        "Malzeme",        "Material",        "multi_select", 4);
        var tTarget   = MakeType("target_audience", "Hedef Kitle",    "Target Audience", "select", 5);
        var tSeason   = MakeType("season",          "Sezon",          "Season",          "multi_select", 6);

        ctx.AttributeTypes.AddRange(typesList);
        await ctx.SaveChangesAsync();

        // Değerler
        var valuesMap = new Dictionary<string, Dictionary<string, AttributeValue>>();

        async Task AddValues(AttributeType attrType, (string tr, string en, Dictionary<string, object>? extra)[] defs)
        {
            var existing = await ctx.AttributeValues
                .Where(v => v.AttributeTypeId == attrType.Id)
                .Select(v => v.NameI18n)
                .ToListAsync();

            if (existing.Count >= defs.Length)
            {
                var all = await ctx.AttributeValues.Where(v => v.AttributeTypeId == attrType.Id).ToListAsync();
                valuesMap[attrType.Code] = all.ToDictionary(v => v.NameI18n.GetValueOrDefault("tr") ?? v.Id.ToString());
                return;
            }

            var added = new List<AttributeValue>();
            for (int i = 0; i < defs.Length; i++)
            {
                var (tr, en, extra) = defs[i];
                var av = new AttributeValue
                {
                    AttributeTypeId = attrType.Id,
                    NameI18n  = new() { { "tr", tr }, { "en", en } },
                    ExtraData = extra,
                    IsActive  = true,
                    SortOrder = i + 1,
                };
                added.Add(av);
                ctx.AttributeValues.Add(av);
            }
            await ctx.SaveChangesAsync();
            valuesMap[attrType.Code] = added.ToDictionary(v => v.NameI18n["tr"]);
        }

        Dictionary<string, object> Hex(string hex) => new() { { "color_hex", hex } };

        await AddValues(tColor, new[]
        {
            ("Beyaz",   "White",   Hex("#FFFFFF")),
            ("Siyah",   "Black",   Hex("#000000")),
            ("Lacivert","Navy",    Hex("#001F5B")),
            ("Kırmızı", "Red",     Hex("#E53935")),
            ("Mavi",    "Blue",    Hex("#1565C0")),
            ("Yeşil",   "Green",   Hex("#2E7D32")),
            ("Gri",     "Gray",    Hex("#9E9E9E")),
            ("Bej",     "Beige",   Hex("#F5F0E1")),
            ("Pembe",   "Pink",    Hex("#EC407A")),
            ("Turuncu", "Orange",  Hex("#F57C00")),
            ("Mor",     "Purple",  Hex("#7B1FA2")),
            ("Kahverengi","Brown", Hex("#5D4037")),
        });

        Dictionary<string, object>? noExtra = null;
        await AddValues(tSize, new (string tr, string en, Dictionary<string, object>? extra)[]
        {
            ("XS", "XS", noExtra),
            ("S",  "S",  noExtra),
            ("M",  "M",  noExtra),
            ("L",  "L",  noExtra),
            ("XL", "XL", noExtra),
            ("XXL","XXL",noExtra),
            ("3XL","3XL",noExtra),
        });

        await AddValues(tShoeSize, new (string tr, string en, Dictionary<string, object>? extra)[]
        {
            ("36","36",noExtra),("37","37",noExtra),("38","38",noExtra),("39","39",noExtra),
            ("40","40",noExtra),("41","41",noExtra),("42","42",noExtra),("43","43",noExtra),
            ("44","44",noExtra),("45","45",noExtra),("46","46",noExtra),
        });

        await AddValues(tMaterial, new (string tr, string en, Dictionary<string, object>? extra)[]
        {
            ("Pamuk",     "Cotton",    noExtra),
            ("Polyester", "Polyester", noExtra),
            ("Keten",     "Linen",     noExtra),
            ("Viskon",    "Viscose",   noExtra),
            ("Deri",      "Leather",   noExtra),
            ("Süet",      "Suede",     noExtra),
            ("Naylon",    "Nylon",     noExtra),
        });

        await AddValues(tTarget, new (string tr, string en, Dictionary<string, object>? extra)[]
        {
            ("Erkek",    "Men",    noExtra),
            ("Kadın",    "Women",  noExtra),
            ("Unisex",   "Unisex", noExtra),
            ("Çocuk",    "Kids",   noExtra),
        });

        await AddValues(tSeason, new (string tr, string en, Dictionary<string, object>? extra)[]
        {
            ("İlkbahar/Yaz", "Spring/Summer", noExtra),
            ("Sonbahar/Kış", "Autumn/Winter", noExtra),
            ("Tüm Sezonlar", "All Seasons",   noExtra),
        });

        // Tipler sözlüğünü güncelle
        var allTypes = new Dictionary<string, AttributeType>
        {
            ["color"]           = tColor,
            ["size"]            = tSize,
            ["shoe_size"]       = tShoeSize,
            ["material"]        = tMaterial,
            ["target_audience"] = tTarget,
            ["season"]          = tSeason,
        };

        Console.WriteLine("✓ Demo Seed: 6 özellik tipi + ~55 özellik değeri oluşturuldu.");
        return (allTypes, valuesMap);
    }

    // ─────────────────────────────────────────────────────────
    // 3. ÜRÜN GRUPLARI
    // ─────────────────────────────────────────────────────────
    private static async Task<Dictionary<string, ProductGroup>> SeedProductGroupsAsync(
        IServiceProvider sp,
        Dictionary<string, AttributeType> types)
    {
        var ctx = sp.GetRequiredService<CatalogDbContext>();

        var codes = new[] { "tshirt", "shirt", "dress", "sneaker", "tracksuit", "bag", "jacket" };
        var existing = await ctx.ProductGroups
            .Where(g => codes.Contains(g.Code))
            .ToDictionaryAsync(g => g.Code);

        if (existing.Count == codes.Length)
        {
            Console.WriteLine("✓ Demo Seed: Ürün grupları zaten mevcut.");
            return existing;
        }

        var groups = new List<ProductGroup>();

        ProductGroup MakeGroup(string code, string nameTr, string nameEn, int sort)
        {
            if (existing.TryGetValue(code, out var ex)) return ex;
            var g = new ProductGroup
            {
                Code      = code,
                NameI18n  = new() { { "tr", nameTr }, { "en", nameEn } },
                IsActive  = true,
                SortOrder = sort,
            };
            groups.Add(g);
            return g;
        }

        var grpTshirt   = MakeGroup("tshirt",    "T-Shirt",        "T-Shirt",     1);
        var grpShirt    = MakeGroup("shirt",     "Gömlek",         "Shirt",       2);
        var grpDress    = MakeGroup("dress",     "Elbise",         "Dress",       3);
        var grpSneaker  = MakeGroup("sneaker",   "Spor Ayakkabı",  "Sneaker",     4);
        var grpTrack    = MakeGroup("tracksuit", "Eşofman",        "Tracksuit",   5);
        var grpBag      = MakeGroup("bag",       "Çanta",          "Bag",         6);
        var grpJacket   = MakeGroup("jacket",    "Ceket/Mont",     "Jacket",      7);

        ctx.ProductGroups.AddRange(groups);
        await ctx.SaveChangesAsync();

        // Özellik atamaları
        void AddAttr(ProductGroup g, AttributeType t, bool isVariant = false, bool isPrimary = false, int sort = 0)
        {
            ctx.ProductGroupAttributes.Add(new ProductGroupAttribute
            {
                ProductGroupId  = g.Id,
                AttributeTypeId = t.Id,
                IsVariant       = isVariant,
                IsPrimaryAxis   = isPrimary,
                IsRequired      = isVariant,
                SortOrder       = sort,
            });
        }

        // T-Shirt: Renk (varyant, birincil), Beden (varyant), Malzeme, Hedef Kitle, Sezon
        AddAttr(grpTshirt, types["color"],           isVariant: true,  isPrimary: true, sort: 1);
        AddAttr(grpTshirt, types["size"],            isVariant: true,  sort: 2);
        AddAttr(grpTshirt, types["material"],        sort: 3);
        AddAttr(grpTshirt, types["target_audience"], sort: 4);
        AddAttr(grpTshirt, types["season"],          sort: 5);

        // Gömlek: Renk (varyant, birincil), Beden (varyant), Malzeme, Hedef Kitle
        AddAttr(grpShirt, types["color"],           isVariant: true, isPrimary: true, sort: 1);
        AddAttr(grpShirt, types["size"],            isVariant: true, sort: 2);
        AddAttr(grpShirt, types["material"],        sort: 3);
        AddAttr(grpShirt, types["target_audience"], sort: 4);

        // Elbise: Renk (varyant, birincil), Beden (varyant), Malzeme, Hedef Kitle, Sezon
        AddAttr(grpDress, types["color"],           isVariant: true, isPrimary: true, sort: 1);
        AddAttr(grpDress, types["size"],            isVariant: true, sort: 2);
        AddAttr(grpDress, types["material"],        sort: 3);
        AddAttr(grpDress, types["target_audience"], sort: 4);

        // Spor Ayakkabı: Renk (varyant, birincil), Ayakkabı Bedeni (varyant), Malzeme, Hedef Kitle
        AddAttr(grpSneaker, types["color"],           isVariant: true, isPrimary: true, sort: 1);
        AddAttr(grpSneaker, types["shoe_size"],       isVariant: true, sort: 2);
        AddAttr(grpSneaker, types["material"],        sort: 3);
        AddAttr(grpSneaker, types["target_audience"], sort: 4);

        // Eşofman: Renk (varyant, birincil), Beden (varyant), Hedef Kitle, Sezon
        AddAttr(grpTrack, types["color"],           isVariant: true, isPrimary: true, sort: 1);
        AddAttr(grpTrack, types["size"],            isVariant: true, sort: 2);
        AddAttr(grpTrack, types["target_audience"], sort: 3);

        // Çanta: Renk (varyant, birincil), Malzeme
        AddAttr(grpBag, types["color"],    isVariant: true, isPrimary: true, sort: 1);
        AddAttr(grpBag, types["material"], sort: 2);

        // Ceket/Mont: Renk (varyant, birincil), Beden (varyant), Malzeme, Hedef Kitle, Sezon
        AddAttr(grpJacket, types["color"],           isVariant: true, isPrimary: true, sort: 1);
        AddAttr(grpJacket, types["size"],            isVariant: true, sort: 2);
        AddAttr(grpJacket, types["material"],        sort: 3);
        AddAttr(grpJacket, types["target_audience"], sort: 4);
        AddAttr(grpJacket, types["season"],          sort: 5);

        await ctx.SaveChangesAsync();

        var result = existing;
        foreach (var g in groups) result[g.Code] = g;
        Console.WriteLine($"✓ Demo Seed: {groups.Count} ürün grubu + özellik atamaları oluşturuldu.");
        return result;
    }

    // ─────────────────────────────────────────────────────────
    // 4. KATEGORİLER
    // ─────────────────────────────────────────────────────────
    private static async Task<Dictionary<string, Category>> SeedCategoriesAsync(IServiceProvider sp)
    {
        // Kategoriler artık DatabaseSeeder tarafından oluşturuluyor.
        // DemoDataSeeder sadece mevcut kategorileri yükler.
        var ctx = sp.GetRequiredService<CatalogDbContext>();
        var all = await ctx.Categories.ToDictionaryAsync(c => c.Code);
        Console.WriteLine($"✓ Demo Seed: {all.Count} kategori DatabaseSeeder'dan alındı.");
        return all;
    }

    // ─────────────────────────────────────────────────────────
    // 5. ÜRÜNLER + VARYANTLAR
    // ─────────────────────────────────────────────────────────
    private static async Task<List<Guid>> SeedProductsAsync(
        IServiceProvider sp,
        Dictionary<string, ProductGroup> groups,
        Dictionary<string, AttributeType> types,
        Dictionary<string, Dictionary<string, AttributeValue>> values,
        Dictionary<string, Category> categories)
    {
        var ctx = sp.GetRequiredService<CatalogDbContext>();

        if (await ctx.Products.AnyAsync(p => p.Code == "TSHIRT-001"))
        {
            Console.WriteLine("✓ Demo Seed: Ürünler zaten mevcut.");
            var existing = await ctx.Products.SelectMany(p => p.Variants).Select(v => v.Id).ToListAsync();
            return existing;
        }

        var allVariantIds = new List<Guid>();

        async Task<List<Guid>> AddProduct(
            string code, string nameTr, string nameEn,
            string groupCode, decimal basePrice, decimal baseCost, int taxRate,
            string[] colorNames, string[] sizeNames,
            string attrSizeKey, // "size" veya "shoe_size"
            string[] catCodes)
        {
            if (!groups.TryGetValue(groupCode, out var group))
                return new();

            var product = new Product
            {
                ProductGroupId   = group.Id,
                Code             = code,
                NameI18n         = new() { { "tr", nameTr }, { "en", nameEn } },
                BasePrice        = basePrice,
                BaseCost         = baseCost,
                TaxRate          = taxRate,
                IsActive         = true,
            };
            ctx.Products.Add(product);
            await ctx.SaveChangesAsync();

            // Kategorilere ekle
            foreach (var catCode in catCodes)
            {
                if (categories.TryGetValue(catCode, out var cat))
                    ctx.CategoryProducts.Add(new CategoryProduct { CategoryId = cat.Id, ProductId = product.Id, SortOrder = 0 });
            }

            // Varyantlar
            var colorVals = values.GetValueOrDefault("color") ?? new();
            var sizeVals  = values.GetValueOrDefault(attrSizeKey) ?? new();
            var variantIds = new List<Guid>();

            int varIdx = 0;
            foreach (var colorName in colorNames)
            {
                if (!colorVals.TryGetValue(colorName, out var colorVal)) continue;
                foreach (var sizeName in sizeNames)
                {
                    if (!sizeVals.TryGetValue(sizeName, out var sizeVal)) continue;

                    var sku = $"{code}-{colorName.ToUpper().Replace(" ", "")}-{sizeName}";
                    var variant = new ProductVariant
                    {
                        ProductId = product.Id,
                        Sku       = sku,
                        Barcode   = GenerateBarcode(code, varIdx++),
                        BasePrice = basePrice,
                        BaseCost  = baseCost,
                        IsActive  = true,
                    };
                    ctx.ProductVariants.Add(variant);
                    await ctx.SaveChangesAsync();

                    // Renk özelliği
                    ctx.ProductVariantAttributes.Add(new ProductVariantAttribute
                    {
                        VariantId       = variant.Id,
                        AttributeTypeId = types["color"].Id,
                        AttributeValueId = colorVal.Id,
                    });

                    // Beden özelliği
                    ctx.ProductVariantAttributes.Add(new ProductVariantAttribute
                    {
                        VariantId        = variant.Id,
                        AttributeTypeId  = types[attrSizeKey].Id,
                        AttributeValueId = sizeVal.Id,
                    });

                    variantIds.Add(variant.Id);
                }
            }

            await ctx.SaveChangesAsync();
            return variantIds;
        }

        // ── Ürünler ──────────────────────────────────────────────

        allVariantIds.AddRange(await AddProduct(
            "TSHIRT-001", "Slim Fit Bisiklet Yaka T-Shirt", "Slim Fit Crew Neck T-Shirt",
            "tshirt", 299, 89, 18,
            colorNames: new[] { "Beyaz", "Siyah", "Lacivert", "Kırmızı", "Gri" },
            sizeNames:  new[] { "S", "M", "L", "XL", "XXL" },
            attrSizeKey: "size",
            catCodes: new[] { "erkek_tshirt", "giyim" }));

        allVariantIds.AddRange(await AddProduct(
            "SHIRT-001", "Oxford Uzun Kollu Gömlek", "Oxford Long Sleeve Shirt",
            "shirt", 599, 189, 18,
            colorNames: new[] { "Beyaz", "Mavi", "Lacivert" },
            sizeNames:  new[] { "S", "M", "L", "XL" },
            attrSizeKey: "size",
            catCodes: new[] { "erkek_gomlek", "giyim" }));

        allVariantIds.AddRange(await AddProduct(
            "DRESS-001", "Yazlık Midi Elbise", "Summer Midi Dress",
            "dress", 849, 249, 18,
            colorNames: new[] { "Beyaz", "Pembe", "Lacivert" },
            sizeNames:  new[] { "XS", "S", "M", "L" },
            attrSizeKey: "size",
            catCodes: new[] { "kadin_elbise", "giyim" }));

        allVariantIds.AddRange(await AddProduct(
            "SNEAKER-001", "Air Cushion Spor Ayakkabı", "Air Cushion Sneaker",
            "sneaker", 1299, 399, 18,
            colorNames: new[] { "Beyaz", "Siyah", "Kırmızı" },
            sizeNames:  new[] { "38", "39", "40", "41", "42", "43", "44" },
            attrSizeKey: "shoe_size",
            catCodes: new[] { "spor_ayakkabi", "erkek_ayakkabi" }));

        allVariantIds.AddRange(await AddProduct(
            "TRACK-001", "Slim Fit Eşofman Takımı", "Slim Fit Tracksuit Set",
            "tracksuit", 999, 299, 18,
            colorNames: new[] { "Siyah", "Lacivert", "Gri" },
            sizeNames:  new[] { "S", "M", "L", "XL", "XXL" },
            attrSizeKey: "size",
            catCodes: new[] { "erkek_esofman", "spor_giyim" }));

        allVariantIds.AddRange(await AddProduct(
            "BAG-001", "Deri Omuz Çantası", "Leather Shoulder Bag",
            "bag", 1799, 549, 18,
            colorNames: new[] { "Siyah", "Kahverengi", "Bej" },
            sizeNames:  new[] { "S", "M", "L" },
            attrSizeKey: "size",
            catCodes: new[] { "kadin_canta", "canta" }));

        allVariantIds.AddRange(await AddProduct(
            "JACKET-001", "Oversize Blazer Ceket", "Oversize Blazer Jacket",
            "jacket", 2199, 699, 18,
            colorNames: new[] { "Siyah", "Beyaz", "Bej" },
            sizeNames:  new[] { "XS", "S", "M", "L", "XL" },
            attrSizeKey: "size",
            catCodes: new[] { "erkek_ceket", "kadin_ceket", "giyim" }));

        Console.WriteLine($"✓ Demo Seed: 7 ürün + {allVariantIds.Count} varyant oluşturuldu.");
        return allVariantIds;
    }

    private static string GenerateBarcode(string productCode, int index)
    {
        var prefix = productCode.Replace("-", "").Substring(0, Math.Min(4, productCode.Length));
        return $"8690{prefix.PadRight(4, '0')}{index:D5}";
    }

    // ─────────────────────────────────────────────────────────
    // 6. DEPO + STOK
    // ─────────────────────────────────────────────────────────
    private static async Task SeedInventoryAsync(IServiceProvider sp, List<Guid> variantIds)
    {
        var ctx = sp.GetRequiredService<InventoryDbContext>();

        Warehouse warehouse;
        if (await ctx.Warehouses.AnyAsync(w => w.Code == "merkez"))
        {
            warehouse = await ctx.Warehouses.FirstAsync(w => w.Code == "merkez");
        }
        else
        {
            warehouse = new Warehouse
            {
                Code             = "merkez",
                NameI18n         = new() { { "tr", "Merkez Depo" }, { "en", "Main Warehouse" } },
                WarehouseType    = "main",
                Address          = "Sanayi Mah. Depo Cad. No:1, İstanbul",
                IsSellableOnline = true,
                ReservePriority  = 1,
                IsActive         = true,
                SortOrder        = 1,
            };
            ctx.Warehouses.Add(warehouse);
            await ctx.SaveChangesAsync();
            Console.WriteLine("✓ Demo Seed: Merkez Depo oluşturuldu.");
        }

        // Mevcut stok kayıtlarını kontrol et
        var existingStocks = (await ctx.Stocks
            .Where(s => s.WarehouseId == warehouse.Id)
            .Select(s => s.VariantId)
            .ToListAsync()).ToHashSet();

        var toAdd = variantIds.Where(id => !existingStocks.Contains(id)).ToList();
        if (toAdd.Count == 0)
        {
            Console.WriteLine("✓ Demo Seed: Stoklar zaten mevcut.");
            return;
        }

        foreach (var varId in toAdd)
        {
            ctx.Stocks.Add(new Stock
            {
                VariantId        = varId,
                WarehouseId      = warehouse.Id,
                StockType        = "physical",
                Quantity         = 50,
                ReservedQuantity = 0,
            });
        }
        await ctx.SaveChangesAsync();
        Console.WriteLine($"✓ Demo Seed: {toAdd.Count} stok kaydı oluşturuldu (50 adet/varyant).");
    }

    // ─────────────────────────────────────────────────────────
    // 7. CMS MENÜLER
    // ─────────────────────────────────────────────────────────
    private static async Task SeedCmsMenusAsync(
        IServiceProvider sp,
        Guid firmPlatformId,
        Dictionary<string, Category> categories)
    {
        var ctx = sp.GetRequiredService<StorefrontDbContext>();

        if (await ctx.NavigationMenus.AnyAsync(m => m.Code == "main_nav"))
        {
            Console.WriteLine("✓ Demo Seed: Navigasyon menüleri zaten mevcut.");
            return;
        }

        var mainNav = new NavigationMenu
        {
            Id             = Guid.NewGuid(),
            FirmPlatformId = firmPlatformId,
            Code           = "main_nav",
            NameI18n       = new() { { "tr", "Ana Navigasyon" }, { "en", "Main Navigation" } },
            MenuType       = "header",
            IsActive       = true,
            SortOrder      = 1,
        };

        var footer = new NavigationMenu
        {
            Id             = Guid.NewGuid(),
            FirmPlatformId = firmPlatformId,
            Code           = "footer",
            NameI18n       = new() { { "tr", "Footer Menü" }, { "en", "Footer Menu" } },
            MenuType       = "footer",
            IsActive       = true,
            SortOrder      = 2,
        };

        var mobileNav = new NavigationMenu
        {
            Id             = Guid.NewGuid(),
            FirmPlatformId = firmPlatformId,
            Code           = "mobile_bottom",
            NameI18n       = new() { { "tr", "Mobil Alt Navigasyon" }, { "en", "Mobile Bottom Navigation" } },
            MenuType       = "custom",
            IsActive       = true,
            SortOrder      = 3,
        };

        ctx.NavigationMenus.AddRange(mainNav, footer, mobileNav);
        await ctx.SaveChangesAsync();

        NavNode MakeNode(Guid menuId, string nameTr, string nameEn, string nodeType,
            Guid? parentId = null, string? url = null, Guid? categoryId = null, int sort = 0) =>
            new()
            {
                Id               = Guid.NewGuid(),
                NavigationMenuId = menuId,
                ParentNavNodeId  = parentId,
                NameOverrideI18n = new() { { "tr", nameTr }, { "en", nameEn } },
                NodeType         = nodeType,
                CategoryId       = categoryId,
                CustomUrl        = url,
                IsActive         = true,
                SortOrder        = sort,
            };

        var nodes = new List<NavNode>();

        var navGiyim    = MakeNode(mainNav.Id, "Giyim",       "Clothing",  "category",
            categoryId: categories.GetValueOrDefault("giyim")?.Id, sort: 1);
        var navAyakkabi = MakeNode(mainNav.Id, "Ayakkabı",    "Shoes",     "category",
            categoryId: categories.GetValueOrDefault("ayakkabi")?.Id, sort: 2);
        var navCanta    = MakeNode(mainNav.Id, "Çanta",       "Bags",      "category",
            categoryId: categories.GetValueOrDefault("canta")?.Id, sort: 3);
        var navSpor     = MakeNode(mainNav.Id, "Spor",        "Sports",    "category",
            categoryId: categories.GetValueOrDefault("spor")?.Id, sort: 4);
        var navCampaign = MakeNode(mainNav.Id, "Kampanyalar", "Campaigns", "link",
            url: "/kampanyalar", sort: 5);

        nodes.AddRange(new[] { navGiyim, navAyakkabi, navCanta, navSpor, navCampaign });
        ctx.NavNodes.AddRange(nodes);
        await ctx.SaveChangesAsync();
        nodes.Clear();

        nodes.Add(MakeNode(mainNav.Id, "Erkek Giyim", "Men's Clothing",   "category",
            parentId: navGiyim.Id, categoryId: categories.GetValueOrDefault("erkek_giyim")?.Id, sort: 1));
        nodes.Add(MakeNode(mainNav.Id, "Kadın Giyim", "Women's Clothing", "category",
            parentId: navGiyim.Id, categoryId: categories.GetValueOrDefault("kadin_giyim")?.Id, sort: 2));
        nodes.Add(MakeNode(mainNav.Id, "Çocuk Giyim", "Kids Clothing",    "category",
            parentId: navGiyim.Id, categoryId: categories.GetValueOrDefault("cocuk_giyim")?.Id, sort: 3));

        nodes.Add(MakeNode(mainNav.Id, "Erkek Ayakkabı", "Men's Shoes",   "category",
            parentId: navAyakkabi.Id, categoryId: categories.GetValueOrDefault("erkek_ayakkabi")?.Id, sort: 1));
        nodes.Add(MakeNode(mainNav.Id, "Kadın Ayakkabı", "Women's Shoes", "category",
            parentId: navAyakkabi.Id, categoryId: categories.GetValueOrDefault("kadin_ayakkabi")?.Id, sort: 2));

        nodes.Add(MakeNode(footer.Id, "Hakkımızda",    "About Us",     "link", url: "/hakkimizda",    sort: 1));
        nodes.Add(MakeNode(footer.Id, "İletişim",      "Contact",      "link", url: "/iletisim",      sort: 2));
        nodes.Add(MakeNode(footer.Id, "Gizlilik",      "Privacy",      "link", url: "/gizlilik",      sort: 3));
        nodes.Add(MakeNode(footer.Id, "İade Koşulları","Return Policy","link", url: "/iade-kosullari", sort: 4));
        nodes.Add(MakeNode(footer.Id, "Kampanyalar",   "Campaigns",    "link", url: "/kampanyalar",   sort: 5));

        nodes.Add(MakeNode(mobileNav.Id, "Anasayfa",    "Home",       "link", url: "/",           sort: 1));
        nodes.Add(MakeNode(mobileNav.Id, "Kategoriler", "Categories", "link", url: "/kategoriler", sort: 2));
        nodes.Add(MakeNode(mobileNav.Id, "Sepet",       "Cart",       "link", url: "/sepet",       sort: 3));
        nodes.Add(MakeNode(mobileNav.Id, "Favoriler",   "Favorites",  "link", url: "/favoriler",   sort: 4));
        nodes.Add(MakeNode(mobileNav.Id, "Hesabım",     "Account",    "link", url: "/hesabim",     sort: 5));

        ctx.NavNodes.AddRange(nodes);
        await ctx.SaveChangesAsync();

        Console.WriteLine($"✓ Demo Seed: 3 navigasyon menüsü + {nodes.Count + 5} node oluşturuldu.");
    }
}
