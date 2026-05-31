using ECSPros.Catalog.Domain.Entities;
using ECSPros.Catalog.Infrastructure.Persistence;
using ECSPros.Core.Domain.Entities;
using ECSPros.Core.Infrastructure.Persistence;
using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using ECSPros.Iam.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Extensions;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        await SeedIamAsync(scope.ServiceProvider);
        await SeedCoreAsync(scope.ServiceProvider);
        await SeedCatalogAsync(scope.ServiceProvider);
        await SeedCategoriesAsync(scope.ServiceProvider);
        await SeedPlatformTypesAsync(services);
    }

    private static async Task SeedIamAsync(IServiceProvider sp)
    {
        await SeedPermissionsAndRolesAsync(sp);
        await SeedAdminUserAsync(sp);
    }

    /// <summary>
    /// İdempotent: eksik dilleri ekler. Her ortamda (Production dahil) çalışır.
    /// </summary>
    public static async Task SeedLanguagesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var existingCodes = (await context.Languages
            .Select(l => l.Code).ToListAsync()).ToHashSet();

        var allLanguages = new[]
        {
            new Language { Code = "tr", NativeName = "Türkçe",  Direction = "ltr", IsDefault = true,  IsActive = true, SortOrder = 1 },
            new Language { Code = "en", NativeName = "English", Direction = "ltr", IsDefault = false, IsActive = true, SortOrder = 2 },
            new Language { Code = "ar", NativeName = "العربية", Direction = "rtl", IsDefault = false, IsActive = true, SortOrder = 3 },
        };

        var toAdd = allLanguages.Where(l => !existingCodes.Contains(l.Code)).ToList();
        if (toAdd.Count > 0)
        {
            context.Languages.AddRange(toAdd);
            await context.SaveChangesAsync();
            Console.WriteLine($"✓ Seed: {toAdd.Count} dil eklendi ({string.Join(", ", toAdd.Select(l => l.Code))}).");
        }
    }

    /// <summary>
    /// İdempotent: permission ve rol kayıtlarını oluşturur/günceller.
    /// Mevcut kayıtlara dokunmaz, eksik olanları ekler.
    /// </summary>
    public static async Task SeedPermissionsAndRolesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var context = sp.GetRequiredService<IamDbContext>();

        // ── Permissions ────────────────────────────────────────────────────────
        var permDefs = new[]
        {
            (Code: Permissions.CatalogPlatformManage,   Name: "Katalog Platform Yönetimi",  Module: "catalog"),
            (Code: Permissions.CatalogProductsManage,   Name: "Ürün Yönetimi",              Module: "catalog"),
            (Code: Permissions.CatalogCategoriesManage, Name: "Kategori Yönetimi",           Module: "catalog"),
            (Code: Permissions.CatalogImagesManage,     Name: "Görsel Yönetimi",             Module: "catalog"),
            (Code: Permissions.CatalogSettingsManage,   Name: "Katalog Ayarları",            Module: "catalog"),
            (Code: Permissions.InventoryManage,         Name: "Envanter Yönetimi",           Module: "inventory"),
        };

        var existingCodes = await context.Permissions.Select(p => p.Code).ToListAsync();

        foreach (var def in permDefs.Where(d => !existingCodes.Contains(d.Code)))
        {
            context.Permissions.Add(new Permission
            {
                Code           = def.Code,
                NameI18n       = new Dictionary<string, string> { { "tr", def.Name } },
                Module         = def.Module,
                PermissionType = "manage",
                IsActive       = true,
            });
        }
        await context.SaveChangesAsync();

        // ── Roles ──────────────────────────────────────────────────────────────
        var roleDefs = new[]
        {
            (Code: "super_admin",    Name: "Süper Admin",     IsSystem: true),
            (Code: "platform_admin", Name: "Platform Admin",  IsSystem: true),
            (Code: "firm_admin",     Name: "Firma Admin",     IsSystem: false),
        };

        var existingRoleCodes = await context.Roles.Select(r => r.Code).ToListAsync();

        foreach (var rd in roleDefs.Where(r => !existingRoleCodes.Contains(r.Code)))
        {
            context.Roles.Add(new Role
            {
                Code     = rd.Code,
                NameI18n = new Dictionary<string, string> { { "tr", rd.Name }, { "en", rd.Name } },
                IsSystem = rd.IsSystem,
                IsActive = true,
            });
        }
        await context.SaveChangesAsync();

        // ── Role → Permission assignments ──────────────────────────────────────
        var allPermissions    = await context.Permissions.ToListAsync();
        var allRoles          = await context.Roles.ToListAsync();
        var existingRolePerms = await context.RolePermissions
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync();

        void AssignPerms(string roleCode, IEnumerable<string> permCodes)
        {
            var role = allRoles.FirstOrDefault(r => r.Code == roleCode);
            if (role is null) return;

            foreach (var permCode in permCodes)
            {
                var perm = allPermissions.FirstOrDefault(p => p.Code == permCode);
                if (perm is null) continue;
                if (existingRolePerms.Any(rp => rp.RoleId == role.Id && rp.PermissionId == perm.Id)) continue;

                context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
            }
        }

        AssignPerms("super_admin",    Permissions.AllPermissions);
        AssignPerms("platform_admin", Permissions.AllPermissions);
        AssignPerms("firm_admin",     Permissions.FirmAdminPermissions);

        await context.SaveChangesAsync();

        Console.WriteLine("✓ Seed: Permission ve roller oluşturuldu/güncellendi.");
    }

    private static async Task SeedAdminUserAsync(IServiceProvider sp)
    {
        var context = sp.GetRequiredService<IamDbContext>();
        var hasher  = sp.GetRequiredService<IPasswordHasher>();

        if (await context.Users.AnyAsync(u => u.Username == "admin"))
            return;

        var superAdminRole = await context.Roles.FirstAsync(r => r.Code == "super_admin");

        var adminUser = new User
        {
            Username          = "admin",
            Email             = "admin@ecspros.com",
            PasswordHash      = hasher.Hash("Admin123!"),
            FirstName         = "Sistem",
            LastName          = "Admin",
            Department        = "IT",
            IsActive          = true,
            MustChangePassword = true,
        };
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        context.UserRoles.Add(new UserRole { UserId = adminUser.Id, RoleId = superAdminRole.Id });
        await context.SaveChangesAsync();

        Console.WriteLine("✓ Seed: Admin kullanıcısı oluşturuldu. (admin / Admin123!)");
    }

    /// <summary>
    /// İdempotent: eksik platform tiplerini ekler.
    /// </summary>
    public static async Task SeedPlatformTypesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var existingCodes = (await context.PlatformTypes
            .Select(p => p.Code).ToListAsync()).ToHashSet();

        var defaults = new[]
        {
            new PlatformType { Code = "site",          NameI18n = new() { { "tr", "Web Sitesi" },   { "en", "Website" } },          IsMarketplace = false, IsActive = true },
            new PlatformType { Code = "trendyol",      NameI18n = new() { { "tr", "Trendyol" },     { "en", "Trendyol" } },         IsMarketplace = true,  IsActive = true },
            new PlatformType { Code = "hepsiburada",   NameI18n = new() { { "tr", "Hepsiburada" },  { "en", "Hepsiburada" } },      IsMarketplace = true,  IsActive = true },
            new PlatformType { Code = "n11",           NameI18n = new() { { "tr", "n11" },          { "en", "n11" } },              IsMarketplace = true,  IsActive = true },
            new PlatformType { Code = "amazon",        NameI18n = new() { { "tr", "Amazon" },       { "en", "Amazon" } },           IsMarketplace = true,  IsActive = true },
            new PlatformType { Code = "ciceksepeti",   NameI18n = new() { { "tr", "Çiçeksepeti" },  { "en", "Ciceksepeti" } },      IsMarketplace = true,  IsActive = true },
            new PlatformType { Code = "pazarama",      NameI18n = new() { { "tr", "Pazarama" },     { "en", "Pazarama" } },         IsMarketplace = true,  IsActive = true },
            new PlatformType { Code = "mobile_app",    NameI18n = new() { { "tr", "Mobil Uygulama" },{ "en", "Mobile App" } },      IsMarketplace = false, IsActive = true },
            new PlatformType { Code = "pos",           NameI18n = new() { { "tr", "Mağaza / POS" }, { "en", "Store / POS" } },      IsMarketplace = false, IsActive = true },
        };

        var toAdd = defaults.Where(p => !existingCodes.Contains(p.Code)).ToList();
        if (toAdd.Count > 0)
        {
            context.PlatformTypes.AddRange(toAdd);
            await context.SaveChangesAsync();
            Console.WriteLine($"✓ Seed: {toAdd.Count} platform tipi eklendi.");
        }
    }

    private static async Task SeedCoreAsync(IServiceProvider sp)
    {
        var context = sp.GetRequiredService<CoreDbContext>();

        // Diğer referans veriler sadece ilk kurulumda eklenir
        if (await context.OrderStatuses.AnyAsync())
            return;

        // Sipariş durumları
        context.OrderStatuses.AddRange(
            new OrderStatus { Code = "pending", NameI18n = new() { { "tr", "Beklemede" }, { "en", "Pending" } }, Color = "#FFA500", SortOrder = 1, IsActive = true },
            new OrderStatus { Code = "confirmed", NameI18n = new() { { "tr", "Onaylandı" }, { "en", "Confirmed" } }, Color = "#2196F3", SortOrder = 2, IsActive = true },
            new OrderStatus { Code = "preparing", NameI18n = new() { { "tr", "Hazırlanıyor" }, { "en", "Preparing" } }, Color = "#9C27B0", SortOrder = 3, IsActive = true },
            new OrderStatus { Code = "shipped", NameI18n = new() { { "tr", "Kargoda" }, { "en", "Shipped" } }, Color = "#00BCD4", SortOrder = 4, IsActive = true },
            new OrderStatus { Code = "delivered", NameI18n = new() { { "tr", "Teslim Edildi" }, { "en", "Delivered" } }, Color = "#4CAF50", SortOrder = 5, IsActive = true },
            new OrderStatus { Code = "cancelled", NameI18n = new() { { "tr", "İptal Edildi" }, { "en", "Cancelled" } }, Color = "#F44336", SortOrder = 6, IsActive = true },
            new OrderStatus { Code = "returned", NameI18n = new() { { "tr", "İade Edildi" }, { "en", "Returned" } }, Color = "#795548", SortOrder = 7, IsActive = true }
        );

        // Ödeme yöntemleri
        context.PaymentMethods.AddRange(
            new PaymentMethod { Code = "credit_card", NameI18n = new() { { "tr", "Kredi Kartı" }, { "en", "Credit Card" } }, IsOnline = true, RequiresConfirmation = false, IsActive = true, SortOrder = 1 },
            new PaymentMethod { Code = "bank_transfer", NameI18n = new() { { "tr", "Havale/EFT" }, { "en", "Bank Transfer" } }, IsOnline = false, RequiresConfirmation = true, IsActive = true, SortOrder = 2 },
            new PaymentMethod { Code = "cash_on_delivery", NameI18n = new() { { "tr", "Kapıda Ödeme" }, { "en", "Cash on Delivery" } }, IsOnline = false, RequiresConfirmation = false, IsActive = true, SortOrder = 3 },
            new PaymentMethod { Code = "pos", NameI18n = new() { { "tr", "POS" }, { "en", "POS Terminal" } }, IsOnline = false, RequiresConfirmation = false, IsActive = true, SortOrder = 4 },
            new PaymentMethod { Code = "wallet", NameI18n = new() { { "tr", "Cüzdan" }, { "en", "Wallet" } }, IsOnline = true, RequiresConfirmation = false, IsActive = true, SortOrder = 5 }
        );

        // Lookup tipleri
        var genderType = new LookupType
        {
            Code = "gender",
            NameI18n = new() { { "tr", "Cinsiyet" }, { "en", "Gender" } },
            IsSystem = true
        };
        context.LookupTypes.Add(genderType);
        await context.SaveChangesAsync();

        context.LookupValues.AddRange(
            new LookupValue { LookupTypeId = genderType.Id, NameI18n = new() { { "tr", "Erkek" }, { "en", "Male" } }, SortOrder = 1, IsActive = true },
            new LookupValue { LookupTypeId = genderType.Id, NameI18n = new() { { "tr", "Kadın" }, { "en", "Female" } }, SortOrder = 2, IsActive = true },
            new LookupValue { LookupTypeId = genderType.Id, NameI18n = new() { { "tr", "Diğer" }, { "en", "Other" } }, SortOrder = 3, IsActive = true }
        );

        await context.SaveChangesAsync();

        Console.WriteLine("✓ Seed: Core referans verileri oluşturuldu.");
    }

    private static async Task SeedCatalogAsync(IServiceProvider sp)
    {
        var context = sp.GetRequiredService<CatalogDbContext>();

        // ImageServer ayarları
        var imageServerKeys = new Dictionary<string, string>
        {
            ["ImageServer.FtpHost"]       = "localhost",
            ["ImageServer.FtpPort"]       = "21",
            ["ImageServer.FtpUser"]       = "anonymous",
            ["ImageServer.FtpPassword"]   = "",
            ["ImageServer.FtpBasePath"]   = "/images/products/",
            ["ImageServer.PublicBaseUrl"] = "",
            ["ImageServer.LocalSavePath"] = "/opt/ECSProsAI/media/images/products/",
            ["VideoServer.LocalSavePath"] = "/opt/ECSProsAI/media/videos/products/",
            ["VideoServer.PublicBaseUrl"] = "/media/videos/products/",
            ["VideoServer.FtpHost"]       = "localhost",
            ["VideoServer.FtpPort"]       = "21",
            ["VideoServer.FtpUser"]       = "anonymous",
            ["VideoServer.FtpPassword"]   = "",
            ["VideoServer.FtpBasePath"]   = "/videos/products/"
        };

        foreach (var (key, defaultValue) in imageServerKeys)
        {
            if (!await context.CatalogSettings.AnyAsync(x => x.Key == key))
            {
                context.CatalogSettings.Add(new CatalogSetting { Key = key, Value = defaultValue });
            }
        }

        // Barkod sequence başlangıç değeri
        if (!await context.CatalogSettings.AnyAsync(x => x.Key == "barcode_sequence"))
        {
            context.CatalogSettings.Add(new CatalogSetting { Key = "barcode_sequence", Value = "1" });
        }

        await context.SaveChangesAsync();

        // Varsayılan görsel seti
        if (!await context.ImageSets.AnyAsync(s => s.Code == "default"))
        {
            context.ImageSets.Add(new ImageSet
            {
                Code         = "default",
                Name         = "Varsayılan Set",
                IsDefault    = true,
                SortPriority = 1,
                IsActive     = true,
            });
            await context.SaveChangesAsync();
        }

        Console.WriteLine("✓ Seed: Catalog ayarları (ImageServer, barcode_sequence, ImageSet) oluşturuldu.");

        // Filtre renkleri
        await SeedFilterColorsAsync(context);
    }

    /// <summary>
    /// Global kategori ağacını seed eder.
    /// "erkek" kodu zaten varsa idempotent olarak atlar.
    /// Yoksa eski demo kategorileri soft-delete edip kapsamlı ağacı oluşturur.
    /// </summary>
    private static async Task SeedCategoriesAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<CatalogDbContext>();

        // Yeni kapsamlı seed zaten uygulandıysa atla
        if (await db.Categories.AnyAsync(c => c.Code == "erkek"))
        {
            Console.WriteLine("✓ Seed: Kategoriler zaten mevcut, atlanıyor.");
            return;
        }

        // Varsa eski demo kategorileri hard-delete et (unique code constraint'i kaldırmak için)
        var oldCount = await db.Categories.IgnoreQueryFilters().CountAsync();
        if (oldCount > 0)
        {
            // category_products bağımlılığı: önce onu temizle
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM catalog.catalog_category_products");
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM catalog.catalog_categories");
            Console.WriteLine($"  → {oldCount} eski demo kategori silindi.");
        }

        var existingCodes = new HashSet<string>();

        static Category Cat(string code, string tr, string en, int sort, Guid? parentId = null) =>
            new()
            {
                Id       = Guid.NewGuid(),
                Code     = code,
                NameI18n = new() { { "tr", tr }, { "en", en } },
                ParentId = parentId,
                FillType = "manual",
                IsActive = true,
                SortOrder = sort,
            };

        var cats = new List<Category>();
        void Add(Category c) { if (!existingCodes.Contains(c.Code)) cats.Add(c); }

        // ── Kök kategoriler ───────────────────────────────────────────────────
        var erkek    = Cat("erkek",     "Erkek",              "Men",                    1);
        var kadin    = Cat("kadin",     "Kadın",              "Women",                  2);
        var cocuk    = Cat("cocuk",     "Çocuk",              "Kids",                   3);
        var ayakkabi = Cat("ayakkabi",  "Ayakkabı",           "Shoes",                  4);
        var canta    = Cat("canta",     "Çanta",              "Bags",                   5);
        var aksesuar = Cat("aksesuar",  "Aksesuar",           "Accessories",            6);
        var spor     = Cat("spor",      "Spor",               "Sports",                 7);
        var ic_giyim = Cat("ic_giyim",  "İç Giyim & Çorap",  "Underwear & Socks",      8);
        var kozmetik = Cat("kozmetik",  "Kozmetik & Bakım",   "Cosmetics & Care",       9);
        var ev       = Cat("ev_yasam",  "Ev & Yaşam",         "Home & Living",         10);
        var kampanya = Cat("kampanya",  "Kampanya",           "Sale",                  11);
        var yeni     = Cat("yeni_sezon","Yeni Sezon",         "New Season",            12);

        Add(erkek); Add(kadin); Add(cocuk); Add(ayakkabi);
        Add(canta); Add(aksesuar); Add(spor); Add(ic_giyim);
        Add(kozmetik); Add(ev); Add(kampanya); Add(yeni);

        db.Categories.AddRange(cats);
        await db.SaveChangesAsync();
        cats.Clear();

        // ── Erkek alt kategoriler ─────────────────────────────────────────────
        var erk_ust  = Cat("erkek_ust_giyim",   "Üst Giyim",            "Tops",           1, erkek.Id);
        var erk_alt  = Cat("erkek_alt_giyim",   "Alt Giyim",            "Bottoms",        2, erkek.Id);
        var erk_dis  = Cat("erkek_dis_giyim",   "Dış Giyim",            "Outerwear",      3, erkek.Id);
        var erk_takim= Cat("erkek_takim",        "Takım Elbise",         "Suits",          4, erkek.Id);
        var erk_esof = Cat("erkek_esofman",      "Eşofman Takımı",       "Tracksuits",     5, erkek.Id);

        Add(erk_ust); Add(erk_alt); Add(erk_dis); Add(erk_takim); Add(erk_esof);
        db.Categories.AddRange(cats); await db.SaveChangesAsync(); cats.Clear();

        Add(Cat("erkek_tisort",     "T-Shirt & Atlet",      "T-Shirts & Vests",   1, erk_ust.Id));
        Add(Cat("erkek_gomlek",     "Gömlek",               "Shirts",             2, erk_ust.Id));
        Add(Cat("erkek_polo",       "Polo Yaka",            "Polo",               3, erk_ust.Id));
        Add(Cat("erkek_sweatshirt", "Sweatshirt & Kazak",   "Sweatshirts",        4, erk_ust.Id));
        Add(Cat("erkek_pantolon",   "Pantolon",             "Trousers",           1, erk_alt.Id));
        Add(Cat("erkek_jean",       "Jean",                 "Jeans",              2, erk_alt.Id));
        Add(Cat("erkek_sort",       "Şort",                 "Shorts",             3, erk_alt.Id));
        Add(Cat("erkek_esofman_alt","Eşofman Altı",         "Sweatpants",         4, erk_alt.Id));
        Add(Cat("erkek_mont",       "Mont & Parka",         "Coats & Parkas",     1, erk_dis.Id));
        Add(Cat("erkek_kaban",      "Kaban & Palto",        "Jackets & Coats",    2, erk_dis.Id));
        Add(Cat("erkek_yelek",      "Yelek",                "Vests",              3, erk_dis.Id));
        Add(Cat("erkek_deri_ceket", "Deri & Suni Deri",     "Leather Jackets",    4, erk_dis.Id));
        db.Categories.AddRange(cats); await db.SaveChangesAsync(); cats.Clear();

        // ── Kadın alt kategoriler ─────────────────────────────────────────────
        var kad_elbise= Cat("kadin_elbise",      "Elbise",               "Dresses",        1, kadin.Id);
        var kad_ust   = Cat("kadin_ust_giyim",   "Üst Giyim",            "Tops",           2, kadin.Id);
        var kad_alt   = Cat("kadin_alt_giyim",   "Alt Giyim",            "Bottoms",        3, kadin.Id);
        var kad_dis   = Cat("kadin_dis_giyim",   "Dış Giyim",            "Outerwear",      4, kadin.Id);
        var kad_takim = Cat("kadin_takim",        "Takım & Tulum",        "Suits & Jumpsuits",5, kadin.Id);

        Add(kad_elbise); Add(kad_ust); Add(kad_alt); Add(kad_dis); Add(kad_takim);
        db.Categories.AddRange(cats); await db.SaveChangesAsync(); cats.Clear();

        Add(Cat("kadin_elbise_mini",   "Mini Elbise",          "Mini Dresses",       1, kad_elbise.Id));
        Add(Cat("kadin_elbise_midi",   "Midi Elbise",          "Midi Dresses",       2, kad_elbise.Id));
        Add(Cat("kadin_elbise_maksi",  "Maksi Elbise",         "Maxi Dresses",       3, kad_elbise.Id));
        Add(Cat("kadin_bluz",          "Bluz & Gömlek",        "Blouses & Shirts",   1, kad_ust.Id));
        Add(Cat("kadin_tisort",        "T-Shirt & Atlet",      "T-Shirts & Vests",   2, kad_ust.Id));
        Add(Cat("kadin_kazak",         "Kazak & Sweatshirt",   "Knitwear & Sweatshirts", 3, kad_ust.Id));
        Add(Cat("kadin_etek",          "Etek",                 "Skirts",             1, kad_alt.Id));
        Add(Cat("kadin_pantolon",      "Pantolon",             "Trousers",           2, kad_alt.Id));
        Add(Cat("kadin_jean",          "Jean",                 "Jeans",              3, kad_alt.Id));
        Add(Cat("kadin_sort",          "Şort",                 "Shorts",             4, kad_alt.Id));
        Add(Cat("kadin_mont",          "Mont & Parka",         "Coats & Parkas",     1, kad_dis.Id));
        Add(Cat("kadin_kaban",         "Kaban & Palto",        "Jackets & Coats",    2, kad_dis.Id));
        Add(Cat("kadin_yelek",         "Yelek",                "Vests",              3, kad_dis.Id));
        db.Categories.AddRange(cats); await db.SaveChangesAsync(); cats.Clear();

        // ── Çocuk alt kategoriler ─────────────────────────────────────────────
        var coc_erkek = Cat("cocuk_erkek", "Erkek Çocuk",       "Boys",               1, cocuk.Id);
        var coc_kiz   = Cat("cocuk_kiz",   "Kız Çocuk",         "Girls",              2, cocuk.Id);
        var coc_bebek = Cat("cocuk_bebek", "Bebek (0-2 Yaş)",   "Baby (0-2 Years)",   3, cocuk.Id);

        Add(coc_erkek); Add(coc_kiz); Add(coc_bebek);
        db.Categories.AddRange(cats); await db.SaveChangesAsync(); cats.Clear();

        Add(Cat("cocuk_erkek_ust",    "Üst Giyim",            "Tops",               1, coc_erkek.Id));
        Add(Cat("cocuk_erkek_alt",    "Alt Giyim",            "Bottoms",            2, coc_erkek.Id));
        Add(Cat("cocuk_erkek_dis",    "Dış Giyim",            "Outerwear",          3, coc_erkek.Id));
        Add(Cat("cocuk_kiz_elbise",   "Elbise & Etek",        "Dresses & Skirts",   1, coc_kiz.Id));
        Add(Cat("cocuk_kiz_ust",      "Üst Giyim",            "Tops",               2, coc_kiz.Id));
        Add(Cat("cocuk_kiz_alt",      "Alt Giyim",            "Bottoms",            3, coc_kiz.Id));
        Add(Cat("cocuk_kiz_dis",      "Dış Giyim",            "Outerwear",          4, coc_kiz.Id));
        db.Categories.AddRange(cats); await db.SaveChangesAsync(); cats.Clear();

        // ── Ayakkabı alt kategoriler ──────────────────────────────────────────
        var ay_erkek = Cat("ayakkabi_erkek", "Erkek Ayakkabı",    "Men's Shoes",        1, ayakkabi.Id);
        var ay_kadin = Cat("ayakkabi_kadin", "Kadın Ayakkabı",    "Women's Shoes",      2, ayakkabi.Id);
        var ay_cocuk = Cat("ayakkabi_cocuk", "Çocuk Ayakkabısı",  "Kids' Shoes",        3, ayakkabi.Id);

        Add(ay_erkek); Add(ay_kadin); Add(ay_cocuk);
        db.Categories.AddRange(cats); await db.SaveChangesAsync(); cats.Clear();

        Add(Cat("erkek_spor_ayakkabi",  "Spor Ayakkabı",        "Sneakers",           1, ay_erkek.Id));
        Add(Cat("erkek_klasik_ayakkabi","Klasik & Oxford",       "Formal Shoes",       2, ay_erkek.Id));
        Add(Cat("erkek_bot",            "Bot & Çizme",           "Boots",              3, ay_erkek.Id));
        Add(Cat("erkek_sandalet",       "Sandalet & Terlik",     "Sandals & Slippers", 4, ay_erkek.Id));
        Add(Cat("kadin_spor_ayakkabi",  "Spor Ayakkabı",        "Sneakers",           1, ay_kadin.Id));
        Add(Cat("kadin_topuklu",        "Topuklu",               "Heels",              2, ay_kadin.Id));
        Add(Cat("kadin_bot",            "Bot & Çizme",           "Boots",              3, ay_kadin.Id));
        Add(Cat("kadin_sandalet",       "Sandalet & Terlik",     "Sandals & Slippers", 4, ay_kadin.Id));
        Add(Cat("kadin_babet",          "Babet & Loafer",        "Flats & Loafers",    5, ay_kadin.Id));
        Add(Cat("cocuk_spor_ayakkabi",  "Spor Ayakkabı",        "Sneakers",           1, ay_cocuk.Id));
        Add(Cat("cocuk_gunluk_ayakkabi","Günlük Ayakkabı",      "Casual Shoes",       2, ay_cocuk.Id));
        Add(Cat("cocuk_bot",            "Bot & Çizme",           "Boots",              3, ay_cocuk.Id));
        db.Categories.AddRange(cats); await db.SaveChangesAsync(); cats.Clear();

        // ── Çanta alt kategoriler ─────────────────────────────────────────────
        Add(Cat("el_cantasi",      "El Çantası",           "Handbags",           1, canta.Id));
        Add(Cat("sirt_cantasi",    "Sırt Çantası",         "Backpacks",          2, canta.Id));
        Add(Cat("omuz_cantasi",    "Omuz Çantası",         "Shoulder Bags",      3, canta.Id));
        Add(Cat("clutch",          "Clutch & El Çantası",  "Clutch Bags",        4, canta.Id));
        Add(Cat("laptop_cantasi",  "Laptop & İş Çantası",  "Laptop Bags",        5, canta.Id));
        Add(Cat("cuzdanlar",       "Cüzdan & Kartlık",     "Wallets & Cardholders", 6, canta.Id));
        Add(Cat("valiz",           "Valiz & Bavul",        "Luggage & Suitcases",7, canta.Id));
        db.Categories.AddRange(cats); await db.SaveChangesAsync(); cats.Clear();

        // ── Aksesuar alt kategoriler ──────────────────────────────────────────
        Add(Cat("kemer",          "Kemer",                "Belts",              1, aksesuar.Id));
        Add(Cat("sapka",          "Şapka & Bere",         "Hats & Beanies",     2, aksesuar.Id));
        Add(Cat("sal_esarp",      "Şal & Eşarp",          "Scarves & Shawls",   3, aksesuar.Id));
        Add(Cat("gunes_gozlugu",  "Güneş Gözlüğü",       "Sunglasses",         4, aksesuar.Id));
        Add(Cat("kravat",         "Kravat & Papyon",      "Ties & Bow Ties",    5, aksesuar.Id));
        Add(Cat("saat",           "Saat",                 "Watches",            6, aksesuar.Id));
        Add(Cat("takilar",        "Takı & Mücevher",      "Jewelry",            7, aksesuar.Id));
        Add(Cat("corap",          "Çorap",                "Socks",              8, aksesuar.Id));
        db.Categories.AddRange(cats); await db.SaveChangesAsync(); cats.Clear();

        // ── Spor alt kategoriler ──────────────────────────────────────────────
        var sp_giyim   = Cat("spor_giyim",    "Spor Giyim",           "Sportswear",         1, spor.Id);
        var sp_ekipman = Cat("spor_ekipman",  "Spor Ekipmanı",        "Sports Equipment",   2, spor.Id);
        var sp_outdoor = Cat("outdoor",       "Outdoor & Kamp",       "Outdoor & Camping",  3, spor.Id);

        Add(sp_giyim); Add(sp_ekipman); Add(sp_outdoor);
        db.Categories.AddRange(cats); await db.SaveChangesAsync(); cats.Clear();

        Add(Cat("spor_giyim_erkek", "Erkek Spor Giyim",   "Men's Sportswear",   1, sp_giyim.Id));
        Add(Cat("spor_giyim_kadin", "Kadın Spor Giyim",   "Women's Sportswear", 2, sp_giyim.Id));
        Add(Cat("spor_giyim_cocuk", "Çocuk Spor Giyim",   "Kids' Sportswear",   3, sp_giyim.Id));
        Add(Cat("fitness_ekipman",  "Fitness & Gym",       "Fitness & Gym",      1, sp_ekipman.Id));
        Add(Cat("top_sporlar",      "Top Sporları",        "Ball Sports",        2, sp_ekipman.Id));
        Add(Cat("yuzme_dalma",      "Yüzme & Dalış",       "Swimming & Diving",  3, sp_ekipman.Id));
        db.Categories.AddRange(cats); await db.SaveChangesAsync(); cats.Clear();

        // ── İç Giyim alt kategoriler ──────────────────────────────────────────
        Add(Cat("erkek_ic_giyim", "Erkek İç Giyim",       "Men's Underwear",    1, ic_giyim.Id));
        Add(Cat("kadin_ic_giyim", "Kadın İç Giyim",       "Women's Underwear",  2, ic_giyim.Id));
        Add(Cat("pijama_gecelik", "Pijama & Gecelik",      "Pyjamas & Nightwear",3, ic_giyim.Id));
        Add(Cat("erkek_corap",    "Erkek Çorap",           "Men's Socks",        4, ic_giyim.Id));
        Add(Cat("kadin_corap",    "Kadın Çorap & Külotlu", "Women's Socks & Tights", 5, ic_giyim.Id));
        db.Categories.AddRange(cats); await db.SaveChangesAsync(); cats.Clear();

        // ── Kozmetik alt kategoriler ──────────────────────────────────────────
        Add(Cat("parfum",         "Parfüm & Deodorant",   "Perfumes & Deodorants", 1, kozmetik.Id));
        Add(Cat("cilt_bakimi",    "Cilt Bakımı",          "Skin Care",            2, kozmetik.Id));
        Add(Cat("sac_bakimi",     "Saç Bakımı",           "Hair Care",            3, kozmetik.Id));
        Add(Cat("makyaj",         "Makyaj",               "Makeup",               4, kozmetik.Id));
        Add(Cat("vucut_bakimi",   "Vücut Bakımı",         "Body Care",            5, kozmetik.Id));
        Add(Cat("tiras_bakim",    "Tıraş & Erkek Bakımı", "Shaving & Men's Grooming", 6, kozmetik.Id));
        db.Categories.AddRange(cats); await db.SaveChangesAsync(); cats.Clear();

        // ── Ev & Yaşam alt kategoriler ────────────────────────────────────────
        Add(Cat("ev_tekstil",    "Ev Tekstili",           "Home Textiles",       1, ev.Id));
        Add(Cat("mutfak",        "Mutfak & Yemek",        "Kitchen & Dining",    2, ev.Id));
        Add(Cat("dekorasyon",    "Dekorasyon",            "Decoration",          3, ev.Id));
        Add(Cat("banyo",         "Banyo",                 "Bathroom",            4, ev.Id));
        db.Categories.AddRange(cats); await db.SaveChangesAsync(); cats.Clear();

        int total = existingCodes.Count == 0
            ? await db.Categories.CountAsync()
            : (await db.Categories.CountAsync()) - existingCodes.Count;
        Console.WriteLine($"✓ Seed: {total} kategori oluşturuldu.");
    }

    private static async Task SeedFilterColorsAsync(CatalogDbContext context)
    {
        var colors = new[]
        {
            // code,              tr,                  en,               hex
            ("siyah",            "Siyah",             "Black",          "#000000"),
            ("beyaz",            "Beyaz",             "White",          "#FFFFFF"),
            ("gri",              "Gri",               "Grey",           "#808080"),
            ("acik_gri",         "Açık Gri",          "Light Grey",     "#D3D3D3"),
            ("koyu_gri",         "Koyu Gri",          "Dark Grey",      "#404040"),
            ("kirmizi",          "Kırmızı",           "Red",            "#E53935"),
            ("pembe",            "Pembe",             "Pink",           "#EC407A"),
            ("turuncu",          "Turuncu",           "Orange",         "#FB8C00"),
            ("sari",             "Sarı",              "Yellow",         "#FDD835"),
            ("bej",              "Bej",               "Beige",          "#F5F0DC"),
            ("krem",             "Krem",              "Cream",          "#FFFDD0"),
            ("yesil",            "Yeşil",             "Green",          "#43A047"),
            ("acik_yesil",       "Açık Yeşil",        "Light Green",    "#A5D6A7"),
            ("koyu_yesil",       "Koyu Yeşil",        "Dark Green",     "#1B5E20"),
            ("haki",             "Haki",              "Khaki",          "#8D7156"),
            ("mavi",             "Mavi",              "Blue",           "#1E88E5"),
            ("acik_mavi",        "Açık Mavi",         "Light Blue",     "#90CAF9"),
            ("koyu_mavi",        "Koyu Mavi",         "Dark Blue",      "#0D47A1"),
            ("lacivert",         "Lacivert",          "Navy",           "#1A237E"),
            ("turkuaz",          "Turkuaz",           "Turquoise",      "#00BCD4"),
            ("mor",              "Mor",               "Purple",         "#8E24AA"),
            ("lila",             "Lila",              "Lilac",          "#CE93D8"),
            ("kahve",            "Kahve",             "Brown",          "#6D4C41"),
            ("altin",            "Altın",             "Gold",           "#FFD600"),
            ("gumus",            "Gümüş",             "Silver",         "#B0BEC5"),
        };

        var existingCodes = (await context.FilterColors.Select(c => c.Code).ToListAsync()).ToHashSet();

        int added = 0;
        foreach (var (code, tr, en, hex) in colors)
        {
            if (existingCodes.Contains(code)) continue;
            context.FilterColors.Add(new FilterColor
            {
                Code      = code,
                NameI18n  = new Dictionary<string, string> { ["tr"] = tr, ["en"] = en },
                HexCode   = hex,
                SortOrder = added * 10,
                IsActive  = true,
            });
            added++;
        }

        if (added > 0)
        {
            await context.SaveChangesAsync();
            Console.WriteLine($"✓ Seed: {added} filtre rengi eklendi.");
        }
    }
}
