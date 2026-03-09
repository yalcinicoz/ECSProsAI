using ECSPros.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Services;

public interface ICatalogDbContext
{
    DbSet<AttributeType> AttributeTypes { get; }
    DbSet<AttributeValue> AttributeValues { get; }
    DbSet<ProductGroup> ProductGroups { get; }
    DbSet<ProductGroupAttribute> ProductGroupAttributes { get; }
    DbSet<Category> Categories { get; }
    DbSet<CategoryProduct> CategoryProducts { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductAttribute> ProductAttributes { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<ProductVariantAttribute> ProductVariantAttributes { get; }
    DbSet<ProductVariantImage> ProductVariantImages { get; }
    DbSet<FirmPlatformProduct> FirmPlatformProducts { get; }
    DbSet<FirmPlatformVariant> FirmPlatformVariants { get; }
    DbSet<ProductUnit> ProductUnits { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
