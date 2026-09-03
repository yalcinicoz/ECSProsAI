using ECSPros.Integration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ECSPros.Integration.Application.Services;

public interface IIntegrationDbContext
{
    DbSet<IntegrationLog> IntegrationLogs { get; }
    DbSet<MarketplaceProduct> MarketplaceProducts { get; }
    DbSet<ErpVariantData> ErpVariantData { get; }
    DbSet<ErpSyncCheckpoint> ErpSyncCheckpoints { get; }
    DbSet<LegacyImportCheckpoint> LegacyImportCheckpoints { get; }
    DbSet<LegacyOrderOutbox> LegacyOrderOutbox { get; }
    DbSet<TrackingEventOutbox> TrackingEventOutbox { get; }
    DbSet<TrackingOrderContext> TrackingOrderContexts { get; }
    DbSet<TrackingConsentLog> TrackingConsentLogs { get; }
    DbSet<MarketplaceCategoryMapping> MarketplaceCategoryMappings { get; }
    DbSet<MarketplaceAttributeMapping> MarketplaceAttributeMappings { get; }
    DbSet<MarketplaceValueMapping> MarketplaceValueMappings { get; }
    DbSet<MarketplaceProductCategoryOverride> MarketplaceProductCategoryOverrides { get; }
    DbSet<MarketplaceProductAttributeValue> MarketplaceProductAttributeValues { get; }
    DbSet<MarketplaceProductReadiness> MarketplaceProductReadiness { get; }
    DbSet<MarketplaceBatch> MarketplaceBatches { get; }
    DbSet<MarketplaceBatchItem> MarketplaceBatchItems { get; }
    DbSet<MarketplaceErrorPattern> MarketplaceErrorPatterns { get; }
    DbSet<MarketplaceIssue> MarketplaceIssues { get; }
    DbSet<FeedJob> FeedJobs { get; }               // FAZ 10 / A6
    DbSet<FeedRunStatus> FeedStatuses { get; }     // FAZ 10 / A6
    /// <summary>Jsonb payload filtreleri gibi EF'e çevrilemeyen sorgular için (Accounts kalıbı).</summary>
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
