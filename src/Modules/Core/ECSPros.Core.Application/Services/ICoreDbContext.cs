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
    DbSet<CargoBarcodeRange> CargoBarcodeRanges { get; }
    DbSet<PlatformType> PlatformTypes { get; }
    DbSet<LabelTemplate> LabelTemplates { get; }
    DbSet<Firm> Firms { get; }
    DbSet<FirmPlatform> FirmPlatforms { get; }
    DbSet<IntegrationService> IntegrationServices { get; }
    DbSet<FirmPlatformIntegration> FirmPlatformIntegrations { get; }
    DbSet<NotificationType> NotificationTypes { get; }
    DbSet<NotificationTemplate> NotificationTemplates { get; }
    DbSet<UiTranslation> UiTranslations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
