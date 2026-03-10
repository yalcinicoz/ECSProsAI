using ECSPros.Integration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Integration.Infrastructure.Persistence.Configurations;

public class IntegrationLogConfiguration : IEntityTypeConfiguration<IntegrationLog>
{
    public void Configure(EntityTypeBuilder<IntegrationLog> b)
    {
        b.ToTable("integration_logs");
        b.HasKey(x => x.Id);
        b.Property(x => x.ServiceType).HasMaxLength(50).IsRequired();
        b.Property(x => x.OperationType).HasMaxLength(50).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.ReferenceType).HasMaxLength(50);
        b.HasQueryFilter(x => !x.IsDeleted);
        b.HasIndex(x => new { x.FirmIntegrationId, x.CreatedAt });
        b.HasIndex(x => x.Status);
    }
}

public class MarketplaceProductConfiguration : IEntityTypeConfiguration<MarketplaceProduct>
{
    public void Configure(EntityTypeBuilder<MarketplaceProduct> b)
    {
        b.ToTable("marketplace_products");
        b.HasKey(x => x.Id);
        b.Property(x => x.ExternalId).HasMaxLength(200).IsRequired();
        b.Property(x => x.ExternalBarcode).HasMaxLength(100);
        b.Property(x => x.SyncStatus).HasMaxLength(20).IsRequired();
        b.Property(x => x.LastSyncError).HasMaxLength(500);
        b.HasQueryFilter(x => !x.IsDeleted);
        b.HasIndex(x => new { x.FirmIntegrationId, x.VariantId }).IsUnique();
        b.HasIndex(x => x.SyncStatus);
    }
}
