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
        b.Property(x => x.LastSentPayloadHash).HasMaxLength(100);
        b.Property(x => x.LastErrorCode).HasMaxLength(50);
        b.Property(x => x.SuggestedCategoryExternalId).HasMaxLength(100);
        b.HasQueryFilter(x => !x.IsDeleted);
        b.HasIndex(x => new { x.FirmIntegrationId, x.VariantId }).IsUnique();
        b.HasIndex(x => new { x.FirmPlatformId, x.SyncStatus });
        b.HasIndex(x => x.SyncStatus);
    }
}

public class MarketplaceBatchConfiguration : IEntityTypeConfiguration<MarketplaceBatch>
{
    public void Configure(EntityTypeBuilder<MarketplaceBatch> b)
    {
        b.ToTable("marketplace_batches");
        b.HasKey(x => x.Id);
        b.Property(x => x.Marketplace).HasMaxLength(50).IsRequired();
        b.Property(x => x.ExternalBatchId).HasMaxLength(200);
        b.Property(x => x.BatchType).HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasMaxLength(30).IsRequired();
        b.Property(x => x.Error).HasMaxLength(1000);
        b.HasQueryFilter(x => !x.IsDeleted);
        // Worker taraması: sırası gelen açık paketler
        b.HasIndex(x => new { x.Status, x.NextPollAt });
        b.HasIndex(x => new { x.FirmPlatformId, x.SubmittedAt });
    }
}

public class MarketplaceBatchItemConfiguration : IEntityTypeConfiguration<MarketplaceBatchItem>
{
    public void Configure(EntityTypeBuilder<MarketplaceBatchItem> b)
    {
        b.ToTable("marketplace_batch_items");
        b.HasKey(x => x.Id);
        b.Property(x => x.Barcode).HasMaxLength(100).IsRequired();
        b.Property(x => x.PayloadHash).HasMaxLength(100).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.ErrorRaw).HasMaxLength(2000);
        b.Property(x => x.ErrorCode).HasMaxLength(50);
        b.Property(x => x.SuggestedCategoryExternalId).HasMaxLength(100);
        b.Property(x => x.SentPrice).HasPrecision(18, 2);
        b.HasQueryFilter(x => !x.IsDeleted);
        b.HasIndex(x => new { x.BatchId, x.Status });
        b.HasIndex(x => new { x.BatchId, x.Barcode });
    }
}

public class MarketplaceIssueConfiguration : IEntityTypeConfiguration<MarketplaceIssue>
{
    public void Configure(EntityTypeBuilder<MarketplaceIssue> b)
    {
        b.ToTable("marketplace_issues");
        b.HasKey(x => x.Id);
        b.Property(x => x.Marketplace).HasMaxLength(50).IsRequired();
        b.Property(x => x.IssueType).HasMaxLength(40).IsRequired();
        b.Property(x => x.ConditionKey).HasMaxLength(200).IsRequired();
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Detail).HasMaxLength(1000);
        b.Property(x => x.SuggestedAction).HasMaxLength(500);
        b.Property(x => x.ReferenceType).HasMaxLength(30);
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.HasQueryFilter(x => !x.IsDeleted);
        // Aynı koşul için tek AÇIK kayıt (duplicate önleme); kapananlar geçmiş olarak kalır
        b.HasIndex(x => new { x.FirmPlatformId, x.ConditionKey })
            .IsUnique().HasFilter("\"Status\" = 'open' AND \"IsDeleted\" = false");
        b.HasIndex(x => new { x.FirmPlatformId, x.Status });
    }
}

public class MarketplaceErrorPatternConfiguration : IEntityTypeConfiguration<MarketplaceErrorPattern>
{
    public void Configure(EntityTypeBuilder<MarketplaceErrorPattern> b)
    {
        b.ToTable("marketplace_error_patterns");
        b.HasKey(x => x.Id);
        b.Property(x => x.Marketplace).HasMaxLength(50).IsRequired();
        b.Property(x => x.Pattern).HasMaxLength(1000).IsRequired();
        b.Property(x => x.ErrorCode).HasMaxLength(50).IsRequired();
        b.HasQueryFilter(x => !x.IsDeleted);
        b.HasIndex(x => new { x.Marketplace, x.IsActive });
    }
}

public class LegacyOrderOutboxConfiguration : IEntityTypeConfiguration<LegacyOrderOutbox>
{
    public void Configure(EntityTypeBuilder<LegacyOrderOutbox> b)
    {
        b.ToTable("legacy_order_outbox");
        b.HasKey(x => x.Id);
        b.Property(x => x.JobType).HasMaxLength(20);
        b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.LastError).HasMaxLength(2000);
        // Aynı siparişe aynı iş tipinden TEK kayıt (idempotent kuyruk)
        b.HasIndex(x => new { x.OrderId, x.JobType }).IsUnique();
        b.HasIndex(x => x.Status);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ErpVariantDataConfiguration : IEntityTypeConfiguration<ErpVariantData>
{
    public void Configure(EntityTypeBuilder<ErpVariantData> b)
    {
        b.ToTable("erp_variant_data");
        b.HasKey(x => x.Id);
        b.Property(x => x.Payload).HasColumnType("jsonb");
        b.HasQueryFilter(x => !x.IsDeleted);
        b.HasIndex(x => new { x.VariantId, x.FirmIntegrationId }).IsUnique();
    }
}

public class MarketplaceCategoryMappingConfiguration : IEntityTypeConfiguration<MarketplaceCategoryMapping>
{
    public void Configure(EntityTypeBuilder<MarketplaceCategoryMapping> b)
    {
        b.ToTable("marketplace_category_mappings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Marketplace).HasMaxLength(50).IsRequired();
        b.Property(x => x.MappingKind).HasMaxLength(20).IsRequired();
        b.Property(x => x.TargetExternalId).HasMaxLength(100);
        b.Property(x => x.TargetName).HasMaxLength(300);
        b.Property(x => x.TargetPath).HasMaxLength(1000);
        b.Property(x => x.RulesJson).HasColumnType("jsonb");
        b.Property(x => x.PoolJson).HasColumnType("jsonb");
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.StatusNote).HasMaxLength(500);
        b.HasQueryFilter(x => !x.IsDeleted);
        // Grup başına pazaryerinde tek eşleme (soft-delete edilenler hariç — filtered index)
        b.HasIndex(x => new { x.Marketplace, x.ProductGroupId, x.FirmPlatformId })
            .IsUnique().HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => new { x.Marketplace, x.Status });
    }
}

public class MarketplaceAttributeMappingConfiguration : IEntityTypeConfiguration<MarketplaceAttributeMapping>
{
    public void Configure(EntityTypeBuilder<MarketplaceAttributeMapping> b)
    {
        b.ToTable("marketplace_attribute_mappings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Marketplace).HasMaxLength(50).IsRequired();
        b.Property(x => x.MpCategoryExternalId).HasMaxLength(100).IsRequired();
        b.Property(x => x.MpAttributeExternalId).HasMaxLength(100).IsRequired();
        b.Property(x => x.MpAttributeName).HasMaxLength(300).IsRequired();
        b.Property(x => x.Strategy).HasMaxLength(20).IsRequired();
        b.Property(x => x.FixedValue).HasMaxLength(500);
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.StatusNote).HasMaxLength(500);
        b.HasQueryFilter(x => !x.IsDeleted);
        b.HasIndex(x => new { x.Marketplace, x.MpCategoryExternalId, x.MpAttributeExternalId, x.FirmPlatformId })
            .IsUnique().HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => new { x.Marketplace, x.Status });
    }
}

public class MarketplaceProductCategoryOverrideConfiguration : IEntityTypeConfiguration<MarketplaceProductCategoryOverride>
{
    public void Configure(EntityTypeBuilder<MarketplaceProductCategoryOverride> b)
    {
        b.ToTable("marketplace_product_category_overrides");
        b.HasKey(x => x.Id);
        b.Property(x => x.Marketplace).HasMaxLength(50).IsRequired();
        b.Property(x => x.CategoryExternalId).HasMaxLength(100).IsRequired();
        b.Property(x => x.CategoryName).HasMaxLength(300).IsRequired();
        b.Property(x => x.CategoryPath).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Source).HasMaxLength(20).IsRequired();
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasQueryFilter(x => !x.IsDeleted);
        b.HasIndex(x => new { x.Marketplace, x.ProductId, x.FirmPlatformId })
            .IsUnique().HasFilter("\"IsDeleted\" = false");
    }
}

public class MarketplaceProductAttributeValueConfiguration : IEntityTypeConfiguration<MarketplaceProductAttributeValue>
{
    public void Configure(EntityTypeBuilder<MarketplaceProductAttributeValue> b)
    {
        b.ToTable("marketplace_product_attribute_values");
        b.HasKey(x => x.Id);
        b.Property(x => x.Marketplace).HasMaxLength(50).IsRequired();
        b.Property(x => x.MpCategoryExternalId).HasMaxLength(100).IsRequired();
        b.Property(x => x.MpAttributeExternalId).HasMaxLength(100).IsRequired();
        b.Property(x => x.MpAttributeName).HasMaxLength(300).IsRequired();
        b.Property(x => x.ValueExternalId).HasMaxLength(100);
        b.Property(x => x.ValueCode).HasMaxLength(100);
        b.Property(x => x.ValueText).HasMaxLength(1000);
        b.HasQueryFilter(x => !x.IsDeleted);
        b.HasIndex(x => new { x.Marketplace, x.ProductId, x.MpCategoryExternalId, x.MpAttributeExternalId, x.FirmPlatformId })
            .IsUnique().HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => new { x.Marketplace, x.ProductId });
    }
}

public class MarketplaceProductReadinessConfiguration : IEntityTypeConfiguration<MarketplaceProductReadiness>
{
    public void Configure(EntityTypeBuilder<MarketplaceProductReadiness> b)
    {
        b.ToTable("marketplace_product_readiness");
        b.HasKey(x => x.Id);
        b.Property(x => x.Marketplace).HasMaxLength(50).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.ReasonsJson).HasColumnType("jsonb");
        b.Property(x => x.ResolvedCategoryExternalId).HasMaxLength(100);
        b.Property(x => x.ResolvedCategoryPath).HasMaxLength(1000);
        b.HasQueryFilter(x => !x.IsDeleted);
        b.HasIndex(x => new { x.Marketplace, x.ProductId, x.FirmPlatformId })
            .IsUnique().HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => new { x.Marketplace, x.Status });
    }
}

public class MarketplaceValueMappingConfiguration : IEntityTypeConfiguration<MarketplaceValueMapping>
{
    public void Configure(EntityTypeBuilder<MarketplaceValueMapping> b)
    {
        b.ToTable("marketplace_value_mappings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Marketplace).HasMaxLength(50).IsRequired();
        b.Property(x => x.MpCategoryExternalId).HasMaxLength(100).IsRequired();
        b.Property(x => x.MpAttributeExternalId).HasMaxLength(100).IsRequired();
        b.Property(x => x.TargetExternalId).HasMaxLength(100);
        b.Property(x => x.TargetCode).HasMaxLength(100);
        b.Property(x => x.TargetValue).HasMaxLength(500).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.StatusNote).HasMaxLength(500);
        b.HasQueryFilter(x => !x.IsDeleted);
        b.HasIndex(x => new { x.Marketplace, x.MpCategoryExternalId, x.MpAttributeExternalId, x.AttributeValueId, x.FirmPlatformId })
            .IsUnique().HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => new { x.Marketplace, x.MpCategoryExternalId });
    }
}
