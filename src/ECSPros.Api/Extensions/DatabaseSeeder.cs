using ECSPros.Core.Domain.Entities;
using ECSPros.Core.Infrastructure.Persistence;
using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using ECSPros.Iam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Extensions;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        await SeedIamAsync(scope.ServiceProvider);
        await SeedCoreAsync(scope.ServiceProvider);
    }

    private static async Task SeedIamAsync(IServiceProvider sp)
    {
        var context = sp.GetRequiredService<IamDbContext>();
        var hasher = sp.GetRequiredService<IPasswordHasher>();

        if (await context.Users.AnyAsync(u => u.Username == "admin"))
            return;

        var adminRole = new Role
        {
            Code = "super_admin",
            NameI18n = new Dictionary<string, string> { { "tr", "Süper Admin" }, { "en", "Super Admin" } },
            IsSystem = true,
            IsActive = true
        };
        context.Roles.Add(adminRole);

        var adminUser = new User
        {
            Username = "admin",
            Email = "admin@ecspros.com",
            PasswordHash = hasher.Hash("Admin123!"),
            FirstName = "Sistem",
            LastName = "Admin",
            Department = "IT",
            IsActive = true,
            MustChangePassword = true
        };
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        context.UserRoles.Add(new UserRole { UserId = adminUser.Id, RoleId = adminRole.Id });
        await context.SaveChangesAsync();

        Console.WriteLine("✓ Seed: Admin kullanıcısı oluşturuldu. (admin / Admin123!)");
    }

    private static async Task SeedCoreAsync(IServiceProvider sp)
    {
        var context = sp.GetRequiredService<CoreDbContext>();

        if (await context.Languages.AnyAsync())
            return;

        // Diller
        context.Languages.AddRange(
            new Language { Code = "tr", NativeName = "Türkçe", Direction = "ltr", IsDefault = true, IsActive = true, SortOrder = 1 },
            new Language { Code = "en", NativeName = "English", Direction = "ltr", IsDefault = false, IsActive = true, SortOrder = 2 }
        );

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
            new LookupValue { LookupTypeId = genderType.Id, Code = "male", NameI18n = new() { { "tr", "Erkek" }, { "en", "Male" } }, SortOrder = 1, IsActive = true },
            new LookupValue { LookupTypeId = genderType.Id, Code = "female", NameI18n = new() { { "tr", "Kadın" }, { "en", "Female" } }, SortOrder = 2, IsActive = true },
            new LookupValue { LookupTypeId = genderType.Id, Code = "other", NameI18n = new() { { "tr", "Diğer" }, { "en", "Other" } }, SortOrder = 3, IsActive = true }
        );

        await context.SaveChangesAsync();

        Console.WriteLine("✓ Seed: Core referans verileri oluşturuldu.");
    }
}
