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
