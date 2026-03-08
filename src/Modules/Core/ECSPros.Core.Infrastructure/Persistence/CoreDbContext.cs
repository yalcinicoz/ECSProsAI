using ECSPros.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Infrastructure.Persistence;

public class CoreDbContext : DbContext
{
    public CoreDbContext(DbContextOptions<CoreDbContext> options) : base(options) { }

    public DbSet<Language> Languages => Set<Language>();
    public DbSet<Content> Contents => Set<Content>();
    public DbSet<LookupType> LookupTypes => Set<LookupType>();
    public DbSet<LookupValue> LookupValues => Set<LookupValue>();
    public DbSet<PlatformType> PlatformTypes => Set<PlatformType>();
    public DbSet<Firm> Firms => Set<Firm>();
    public DbSet<FirmPlatform> FirmPlatforms => Set<FirmPlatform>();
    public DbSet<IntegrationService> IntegrationServices => Set<IntegrationService>();
    public DbSet<FirmIntegration> FirmIntegrations => Set<FirmIntegration>();
    public DbSet<ExpenseType> ExpenseTypes => Set<ExpenseType>();
    public DbSet<CargoRule> CargoRules => Set<CargoRule>();
    public DbSet<OrderStatus> OrderStatuses => Set<OrderStatus>();
    public DbSet<OrderItemStatus> OrderItemStatuses => Set<OrderItemStatus>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<ReturnReason> ReturnReasons => Set<ReturnReason>();
    public DbSet<NotificationType> NotificationTypes => Set<NotificationType>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<FirmNotificationSetting> FirmNotificationSettings => Set<FirmNotificationSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("core");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoreDbContext).Assembly);
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
