using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Infrastructure.Persistence;

public class CatalogDbContext : DbContext, ICatalogDbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<AttributeType> AttributeTypes => Set<AttributeType>();
    public DbSet<AttributeValue> AttributeValues => Set<AttributeValue>();
    public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();
    public DbSet<ProductGroupAttribute> ProductGroupAttributes => Set<ProductGroupAttribute>();
    public DbSet<ProductGroupAxisSubAttribute> ProductGroupAxisSubAttributes => Set<ProductGroupAxisSubAttribute>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductSubmission> ProductSubmissions => Set<ProductSubmission>();
    public DbSet<ProductAttribute> ProductAttributes => Set<ProductAttribute>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductVariantAttribute> ProductVariantAttributes => Set<ProductVariantAttribute>();
    public DbSet<ProductVariantImage> ProductVariantImages => Set<ProductVariantImage>();
    public DbSet<ImageSet> ImageSets => Set<ImageSet>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductImageSetMapping> ProductImageSetMappings => Set<ProductImageSetMapping>();
    public DbSet<ProductVideo> ProductVideos => Set<ProductVideo>();
    public DbSet<ProductUnit> ProductUnits => Set<ProductUnit>();
    public DbSet<VariantPriceHistory> VariantPriceHistories => Set<VariantPriceHistory>();
    public DbSet<CatalogSetting> CatalogSettings => Set<CatalogSetting>();
    public DbSet<ProductPriceHistory> ProductPriceHistories => Set<ProductPriceHistory>();
    public DbSet<ProductAxisSubAttributeValue> ProductAxisSubAttributeValues => Set<ProductAxisSubAttributeValue>();
    public DbSet<Mannequin> Mannequins => Set<Mannequin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("catalog");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);

        // JSONB sözlük kolonlarında anahtar bazlı arama (NameI18n->>'tr' benzeri) —
        // Dictionary indexer'ı dinamik JSON eşlemesinde çevrilmediği için PG'nin yerleşik
        // fonksiyonuna bağlanır (bkz. PgJsonFunctions.JsonText kullanan store arama sorguları).
        modelBuilder
            .HasDbFunction(typeof(Application.Helpers.PgJsonFunctions)
                .GetMethod(nameof(Application.Helpers.PgJsonFunctions.JsonText))!)
            .HasName("jsonb_extract_path_text")
            .IsBuiltIn();

        // H10: etiket bayrağı — jsonb dizide herhangi bir anahtar var mı (?| operatörü)
        modelBuilder
            .HasDbFunction(typeof(Application.Helpers.PgJsonFunctions)
                .GetMethod(nameof(Application.Helpers.PgJsonFunctions.JsonExistsAny))!)
            .HasName("jsonb_exists_any")
            .IsBuiltIn();

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
