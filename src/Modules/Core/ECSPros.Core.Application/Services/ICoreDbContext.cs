using ECSPros.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Services;

public interface ICoreDbContext
{
    DbSet<Language> Languages { get; }
    DbSet<LookupType> LookupTypes { get; }
    DbSet<LookupValue> LookupValues { get; }
    DbSet<OrderStatus> OrderStatuses { get; }
    DbSet<OrderItemStatus> OrderItemStatuses { get; }
    DbSet<PaymentMethod> PaymentMethods { get; }
    DbSet<ReturnReason> ReturnReasons { get; }
    DbSet<ExpenseType> ExpenseTypes { get; }
    DbSet<CargoRule> CargoRules { get; }
    DbSet<PlatformType> PlatformTypes { get; }
    DbSet<Firm> Firms { get; }
    DbSet<FirmPlatform> FirmPlatforms { get; }
    DbSet<IntegrationService> IntegrationServices { get; }
    DbSet<FirmIntegration> FirmIntegrations { get; }
    DbSet<NotificationType> NotificationTypes { get; }
    DbSet<NotificationTemplate> NotificationTemplates { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
