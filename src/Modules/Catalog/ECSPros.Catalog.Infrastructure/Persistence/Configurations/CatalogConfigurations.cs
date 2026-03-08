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
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ExtraData).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.AttributeTypeId, x.Code }).IsUnique();
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
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .IsRequired(false);

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

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("catalog_categories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.FillType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.FilterRules).HasColumnType("jsonb");
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .IsRequired(false);
    }
}

public class CategoryProductConfiguration : IEntityTypeConfiguration<CategoryProduct>
{
    public void Configure(EntityTypeBuilder<CategoryProduct> builder)
    {
        builder.ToTable("catalog_category_products");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.CategoryId, x.ProductId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.Category)
            .WithMany(x => x.CategoryProducts)
            .HasForeignKey(x => x.CategoryId);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.CategoryProducts)
            .HasForeignKey(x => x.ProductId);
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
        builder.HasIndex(x => x.Code).IsUnique();
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
