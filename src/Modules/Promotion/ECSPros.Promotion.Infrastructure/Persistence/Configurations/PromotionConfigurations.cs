using ECSPros.Promotion.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Promotion.Infrastructure.Persistence.Configurations;

public class CampaignTypeConfiguration : IEntityTypeConfiguration<CampaignType>
{
    public void Configure(EntityTypeBuilder<CampaignType> builder)
    {
        builder.ToTable("prm_campaign_types");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DescriptionI18n).HasColumnType("jsonb");
        builder.Property(x => x.HandlerClass).HasMaxLength(500).IsRequired();
        builder.Property(x => x.SettingsSchema).HasColumnType("jsonb");
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Campaigns).WithOne(x => x.CampaignType).HasForeignKey(x => x.CampaignTypeId);
    }
}

public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("prm_campaigns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DescriptionI18n).HasColumnType("jsonb");
        builder.Property(x => x.Settings).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ProductSelectionType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ProductFilter).HasColumnType("jsonb");
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Products).WithOne(x => x.Campaign).HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Exclusions).WithOne(x => x.Campaign).HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Platforms).WithOne(x => x.Campaign).HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CampaignProductConfiguration : IEntityTypeConfiguration<CampaignProduct>
{
    public void Configure(EntityTypeBuilder<CampaignProduct> builder)
    {
        builder.ToTable("prm_campaign_products");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AddedType).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.CampaignId, x.ProductId, x.VariantId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class CampaignExclusionConfiguration : IEntityTypeConfiguration<CampaignExclusion>
{
    public void Configure(EntityTypeBuilder<CampaignExclusion> builder)
    {
        builder.ToTable("prm_campaign_exclusions");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.CampaignId, x.ProductId, x.VariantId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class CampaignPlatformConfiguration : IEntityTypeConfiguration<CampaignPlatform>
{
    public void Configure(EntityTypeBuilder<CampaignPlatform> builder)
    {
        builder.ToTable("prm_campaign_platforms");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.CampaignId, x.FirmPlatformId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("prm_coupons");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CouponType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.DiscountValue).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.MinimumCartTotal).HasPrecision(18, 2);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Usages).WithOne(x => x.Coupon).HasForeignKey(x => x.CouponId);
    }
}

public class CouponUsageConfiguration : IEntityTypeConfiguration<CouponUsage>
{
    public void Configure(EntityTypeBuilder<CouponUsage> builder)
    {
        builder.ToTable("prm_coupon_usages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2).IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
