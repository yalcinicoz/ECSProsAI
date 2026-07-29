using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Infrastructure.Persistence;

public class CoreDbContext : DbContext, ICoreDbContext
{
    private readonly IDataProtectionProvider? _dataProtectionProvider;

    public CoreDbContext(DbContextOptions<CoreDbContext> options,
        IDataProtectionProvider? dataProtectionProvider = null) : base(options)
    {
        _dataProtectionProvider = dataProtectionProvider;
    }

    public DbSet<Language> Languages => Set<Language>();
    public DbSet<Content> Contents => Set<Content>();
    public DbSet<LookupType> LookupTypes => Set<LookupType>();
    public DbSet<LookupValue> LookupValues => Set<LookupValue>();
    public DbSet<PlatformType> PlatformTypes => Set<PlatformType>();
    public DbSet<Firm> Firms => Set<Firm>();
    public DbSet<FirmPlatform> FirmPlatforms => Set<FirmPlatform>();
    public DbSet<IntegrationService> IntegrationServices => Set<IntegrationService>();
    public DbSet<FirmPlatformIntegration> FirmPlatformIntegrations => Set<FirmPlatformIntegration>();
    public DbSet<ExpenseType> ExpenseTypes => Set<ExpenseType>();
    public DbSet<CargoRule> CargoRules => Set<CargoRule>();
    public DbSet<CargoBarcodeRange> CargoBarcodeRanges => Set<CargoBarcodeRange>();
    public DbSet<OrderStatus> OrderStatuses => Set<OrderStatus>();
    public DbSet<OrderItemStatus> OrderItemStatuses => Set<OrderItemStatus>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<ReturnReason> ReturnReasons => Set<ReturnReason>();
    public DbSet<NotificationType> NotificationTypes => Set<NotificationType>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<FirmNotificationSetting> FirmNotificationSettings => Set<FirmNotificationSetting>();
    public DbSet<UiTranslation> UiTranslations => Set<UiTranslation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("core");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoreDbContext).Assembly);

        // Credentials at-rest şifreli (Data Protection) — protector DI'dan geldiği için
        // burada bağlanır; kolonun geri kalan eşlemesi FirmPlatformIntegrationConfiguration'da.
        var protector = _dataProtectionProvider?.CreateProtector(
            "ECSPros.Core.FirmPlatformIntegration.Credentials");
        modelBuilder.Entity<FirmPlatformIntegration>()
            .Property(x => x.Credentials)
            .HasConversion(EncryptedCredentials.CreateConverter(protector), EncryptedCredentials.Comparer)
            .HasColumnType("text")
            .IsRequired();

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<ECSPros.Shared.Kernel.Domain.BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
