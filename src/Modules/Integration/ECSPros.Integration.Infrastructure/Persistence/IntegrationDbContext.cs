using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Integration.Infrastructure.Persistence;

public class IntegrationDbContext(DbContextOptions<IntegrationDbContext> options)
    : DbContext(options), IIntegrationDbContext
{
    public DbSet<IntegrationLog> IntegrationLogs => Set<IntegrationLog>();
    public DbSet<MarketplaceProduct> MarketplaceProducts => Set<MarketplaceProduct>();
    public DbSet<ErpVariantData> ErpVariantData => Set<ErpVariantData>();
    public DbSet<MarketplaceCategoryMapping> MarketplaceCategoryMappings => Set<MarketplaceCategoryMapping>();
    public DbSet<MarketplaceAttributeMapping> MarketplaceAttributeMappings => Set<MarketplaceAttributeMapping>();
    public DbSet<MarketplaceValueMapping> MarketplaceValueMappings => Set<MarketplaceValueMapping>();
    public DbSet<MarketplaceProductCategoryOverride> MarketplaceProductCategoryOverrides => Set<MarketplaceProductCategoryOverride>();
    public DbSet<MarketplaceProductAttributeValue> MarketplaceProductAttributeValues => Set<MarketplaceProductAttributeValue>();
    public DbSet<MarketplaceProductReadiness> MarketplaceProductReadiness => Set<MarketplaceProductReadiness>();
    public DbSet<MarketplaceBatch> MarketplaceBatches => Set<MarketplaceBatch>();
    public DbSet<MarketplaceBatchItem> MarketplaceBatchItems => Set<MarketplaceBatchItem>();
    public DbSet<MarketplaceErrorPattern> MarketplaceErrorPatterns => Set<MarketplaceErrorPattern>();
    public DbSet<MarketplaceIssue> MarketplaceIssues => Set<MarketplaceIssue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("integration");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IntegrationDbContext).Assembly);
    }
}
