using ECSPros.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Services;

public interface ICatalogDbContext
{
    DbSet<AttributeType> AttributeTypes { get; }
    DbSet<AttributeValue> AttributeValues { get; }
    DbSet<AttributeValueProperty> AttributeValueProperties { get; }
    DbSet<ProductGroup> ProductGroups { get; }
    DbSet<ProductGroupAttribute> ProductGroupAttributes { get; }
    DbSet<ProductGroupAxisSubAttribute> ProductGroupAxisSubAttributes { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductAttribute> ProductAttributes { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<ProductVariantAttribute> ProductVariantAttributes { get; }
    DbSet<ProductVariantImage> ProductVariantImages { get; }
    DbSet<ImageSet> ImageSets { get; }
    DbSet<ProductImage> ProductImages { get; }
    DbSet<ProductImageSetMapping> ProductImageSetMappings { get; }
    DbSet<ProductVideo> ProductVideos { get; }
    DbSet<FirmPlatformProduct> FirmPlatformProducts { get; }
    DbSet<FirmPlatformVariant> FirmPlatformVariants { get; }
    DbSet<ProductUnit> ProductUnits { get; }
    DbSet<CatalogSetting> CatalogSettings { get; }
    DbSet<ProductPriceHistory> ProductPriceHistories { get; }
    DbSet<ProductAxisSubAttributeValue> ProductAxisSubAttributeValues { get; }
    DbSet<FilterColor> FilterColors { get; }
    DbSet<AttributeValueFilterColor> AttributeValueFilterColors { get; }
    DbSet<VariantPriceHistory> VariantPriceHistories { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
