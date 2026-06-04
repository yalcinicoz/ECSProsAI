using ECSPros.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Catalog.Infrastructure.Persistence.Configurations;

public class AttributeTypeConfiguration : IEntityTypeConfiguration<AttributeType>
{
    public void Configure(EntityTypeBuilder<AttributeType> builder)
    {
        builder.ToTable("catalog_attribute_types");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DataType).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasMany(x => x.Values)
            .WithOne(x => x.AttributeType)
            .HasForeignKey(x => x.AttributeTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AttributeValueConfiguration : IEntityTypeConfiguration<AttributeValue>
{
    public void Configure(EntityTypeBuilder<AttributeValue> builder)
    {
        builder.ToTable("catalog_attribute_values");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ExtraData).HasColumnType("jsonb");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ProductGroupConfiguration : IEntityTypeConfiguration<ProductGroup>
{
    public void Configure(EntityTypeBuilder<ProductGroup> builder)
    {
        builder.ToTable("catalog_product_groups");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasMany(x => x.Attributes)
            .WithOne(x => x.ProductGroup)
            .HasForeignKey(x => x.ProductGroupId);
    }
}

public class ProductGroupAttributeConfiguration : IEntityTypeConfiguration<ProductGroupAttribute>
{
    public void Configure(EntityTypeBuilder<ProductGroupAttribute> builder)
    {
        builder.ToTable("catalog_product_group_attributes");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ProductGroupId, x.AttributeTypeId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.AttributeType)
            .WithMany(x => x.ProductGroupAttributes)
            .HasForeignKey(x => x.AttributeTypeId);
    }
}


public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("catalog_products");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ShortDescriptionI18n).HasColumnType("jsonb");
        builder.Property(x => x.DescriptionI18n).HasColumnType("jsonb");
        builder.Property(x => x.BasePrice).HasPrecision(18, 4);
        builder.Property(x => x.BaseCost).HasPrecision(18, 4);
        builder.Property(x => x.SupplierProductCode).HasMaxLength(100);
        builder.Property(x => x.Tags).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");
        builder.Property(x => x.Slug).HasMaxLength(300);
        builder.Property(x => x.MetaTitleI18n).HasColumnType("jsonb");
        builder.Property(x => x.MetaDescriptionI18n).HasColumnType("jsonb");
        builder.Property(x => x.MetaKeywordsI18n).HasColumnType("jsonb");
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.Slug).IsUnique().HasFilter("\"Slug\" IS NOT NULL AND \"Slug\" <> ''");
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.ProductGroup)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.ProductGroupId);

        builder.HasMany(x => x.Variants)
            .WithOne(x => x.Product)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductAttributeConfiguration : IEntityTypeConfiguration<ProductAttribute>
{
    public void Configure(EntityTypeBuilder<ProductAttribute> builder)
    {
        builder.ToTable("catalog_product_attributes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CustomValue).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.ProductId, x.AttributeTypeId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.Product).WithMany(x => x.Attributes).HasForeignKey(x => x.ProductId);
        builder.HasOne(x => x.AttributeType).WithMany().HasForeignKey(x => x.AttributeTypeId);
        builder.HasOne(x => x.AttributeValue).WithMany().HasForeignKey(x => x.AttributeValueId).IsRequired(false);
    }
}

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("catalog_product_variants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Sku).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Barcode).HasMaxLength(50);
        builder.HasIndex(x => x.Barcode).IsUnique().HasFilter("\"Barcode\" IS NOT NULL AND \"Barcode\" <> ''");
        builder.Property(x => x.BasePrice).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.BaseCost).HasPrecision(18, 2);
        builder.HasIndex(x => x.Sku).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasMany(x => x.VariantAttributes).WithOne(x => x.Variant).HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Images).WithOne(x => x.Variant).HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Units).WithOne(x => x.Variant).HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.FirmPlatformVariants).WithOne(x => x.Variant).HasForeignKey(x => x.VariantId);
    }
}

public class ProductVariantAttributeConfiguration : IEntityTypeConfiguration<ProductVariantAttribute>
{
    public void Configure(EntityTypeBuilder<ProductVariantAttribute> builder)
    {
        builder.ToTable("catalog_product_variant_attributes");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.VariantId, x.AttributeTypeId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.AttributeType).WithMany().HasForeignKey(x => x.AttributeTypeId);
        builder.HasOne(x => x.AttributeValue).WithMany().HasForeignKey(x => x.AttributeValueId);
    }
}

public class ProductVariantImageConfiguration : IEntityTypeConfiguration<ProductVariantImage>
{
    public void Configure(EntityTypeBuilder<ProductVariantImage> builder)
    {
        builder.ToTable("catalog_product_variant_images");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ImageUrl).HasMaxLength(500).IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class FirmPlatformProductConfiguration : IEntityTypeConfiguration<FirmPlatformProduct>
{
    public void Configure(EntityTypeBuilder<FirmPlatformProduct> builder)
    {
        builder.ToTable("catalog_firm_platform_products");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NameI18n).HasColumnType("jsonb");
        builder.Property(x => x.ShortDescriptionI18n).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.FirmPlatformId, x.ProductId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.Product).WithMany(x => x.FirmPlatformProducts).HasForeignKey(x => x.ProductId);
    }
}

public class FirmPlatformVariantConfiguration : IEntityTypeConfiguration<FirmPlatformVariant>
{
    public void Configure(EntityTypeBuilder<FirmPlatformVariant> builder)
    {
        builder.ToTable("catalog_firm_platform_variants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PriceType).HasMaxLength(20);
        builder.Property(x => x.PriceMultiplier).HasPrecision(18, 6);
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.CompareAtPrice).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.FirmPlatformId, x.VariantId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ProductUnitConfiguration : IEntityTypeConfiguration<ProductUnit>
{
    public void Configure(EntityTypeBuilder<ProductUnit> builder)
    {
        builder.ToTable("catalog_product_units");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UnitType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.UnitNameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.PriceMultiplier).HasPrecision(18, 6);
        builder.HasIndex(x => new { x.VariantId, x.UnitType }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class VariantPriceHistoryConfiguration : IEntityTypeConfiguration<VariantPriceHistory>
{
    public void Configure(EntityTypeBuilder<VariantPriceHistory> builder)
    {
        builder.ToTable("catalog_variant_price_history");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PriceType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.OldValue).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.NewValue).HasPrecision(18, 2).IsRequired();
        builder.HasIndex(x => x.VariantId);
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId);
    }
}

public class ProductPriceHistoryConfiguration : IEntityTypeConfiguration<ProductPriceHistory>
{
    public void Configure(EntityTypeBuilder<ProductPriceHistory> builder)
    {
        builder.ToTable("catalog_product_price_history");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PriceField).HasMaxLength(20).IsRequired();
        builder.Property(x => x.OldValue).HasPrecision(18, 4);
        builder.Property(x => x.NewValue).HasPrecision(18, 4);
        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.ChangedAt);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductGroupAxisSubAttributeConfiguration : IEntityTypeConfiguration<ProductGroupAxisSubAttribute>
{
    public void Configure(EntityTypeBuilder<ProductGroupAxisSubAttribute> builder)
    {
        builder.ToTable("catalog_product_group_axis_sub_attributes");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ProductGroupId, x.AxisAttributeTypeId, x.SubAttributeTypeId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.ProductGroup)
            .WithMany(x => x.AxisSubAttributes)
            .HasForeignKey(x => x.ProductGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.AxisAttributeType)
            .WithMany(x => x.AxisSubAttributes)
            .HasForeignKey(x => x.AxisAttributeTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SubAttributeType)
            .WithMany(x => x.AsSubAttributeOf)
            .HasForeignKey(x => x.SubAttributeTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AttributeValuePropertyConfiguration : IEntityTypeConfiguration<AttributeValueProperty>
{
    public void Configure(EntityTypeBuilder<AttributeValueProperty> builder)
    {
        builder.ToTable("catalog_attribute_value_properties");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Value).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.AttributeValueId, x.SubAttributeTypeId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.AttributeValue)
            .WithMany(x => x.Properties)
            .HasForeignKey(x => x.AttributeValueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SubAttributeType)
            .WithMany()
            .HasForeignKey(x => x.SubAttributeTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FilterColorConfiguration : IEntityTypeConfiguration<FilterColor>
{
    public void Configure(EntityTypeBuilder<FilterColor> builder)
    {
        builder.ToTable("catalog_filter_colors");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.HexCode).HasMaxLength(20);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class AttributeValueFilterColorConfiguration : IEntityTypeConfiguration<AttributeValueFilterColor>
{
    public void Configure(EntityTypeBuilder<AttributeValueFilterColor> builder)
    {
        builder.ToTable("catalog_attribute_value_filter_colors");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.AttributeValueId, x.FilterColorId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.AttributeValue)
            .WithMany(x => x.FilterColors)
            .HasForeignKey(x => x.AttributeValueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FilterColor)
            .WithMany(x => x.AttributeValueMappings)
            .HasForeignKey(x => x.FilterColorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductAxisSubAttributeValueConfiguration : IEntityTypeConfiguration<ProductAxisSubAttributeValue>
{
    public void Configure(EntityTypeBuilder<ProductAxisSubAttributeValue> builder)
    {
        builder.ToTable("catalog_product_axis_sub_attribute_values");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Value).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.ProductId, x.AttributeValueId, x.SubAttributeTypeId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.AttributeValue)
            .WithMany()
            .HasForeignKey(x => x.AttributeValueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SubAttributeType)
            .WithMany()
            .HasForeignKey(x => x.SubAttributeTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CatalogSettingConfiguration : IEntityTypeConfiguration<CatalogSetting>
{
    public void Configure(EntityTypeBuilder<CatalogSetting> builder)
    {
        builder.ToTable("catalog_settings", "catalog");
        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(500).IsRequired();
    }
}

public class ImageSetConfiguration : IEntityTypeConfiguration<ImageSet>
{
    public void Configure(EntityTypeBuilder<ImageSet> builder)
    {
        builder.ToTable("catalog_image_sets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.FallbackSet)
            .WithMany()
            .HasForeignKey(x => x.FallbackSetId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("catalog_product_images");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.BatchId);
        builder.HasIndex(x => new { x.ProductId, x.ImageSetId, x.Status });
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Variant)
            .WithMany()
            .HasForeignKey(x => x.VariantId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ImageSet)
            .WithMany(x => x.ProductImages)
            .HasForeignKey(x => x.ImageSetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductImageSetMappingConfiguration : IEntityTypeConfiguration<ProductImageSetMapping>
{
    public void Configure(EntityTypeBuilder<ProductImageSetMapping> builder)
    {
        builder.ToTable("catalog_product_image_set_mappings");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ProductId, x.ForSetId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ForSet)
            .WithMany(x => x.MappingsAsFor)
            .HasForeignKey(x => x.ForSetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UseSet)
            .WithMany(x => x.MappingsAsUse)
            .HasForeignKey(x => x.UseSetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductVideoConfiguration : IEntityTypeConfiguration<ProductVideo>
{
    public void Configure(EntityTypeBuilder<ProductVideo> builder)
    {
        builder.ToTable("catalog_product_videos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ThumbnailFileName).HasMaxLength(500);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.BatchId);
        builder.HasIndex(x => new { x.ProductId, x.ImageSetId, x.Status });
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ImageSet)
            .WithMany()
            .HasForeignKey(x => x.ImageSetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
