using ECSPros.Shared.Kernel.Domain;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Infrastructure.Persistence;

public class StorefrontDbContext : DbContext, IStorefrontDbContext
{
    public StorefrontDbContext(DbContextOptions<StorefrontDbContext> options) : base(options) { }

    public DbSet<NavigationMenu> NavigationMenus => Set<NavigationMenu>();
    public DbSet<NavNode> NavNodes => Set<NavNode>();
    public DbSet<ChannelProduct> ChannelProducts => Set<ChannelProduct>();
    public DbSet<ChannelVariant> ChannelVariants => Set<ChannelVariant>();
    public DbSet<ChannelCategory> ChannelCategories => Set<ChannelCategory>();
    public DbSet<ChannelCategoryGroup> ChannelCategoryGroups => Set<ChannelCategoryGroup>();
    public DbSet<ChannelCategoryProduct> ChannelCategoryProducts => Set<ChannelCategoryProduct>();
    public DbSet<ChannelProductGroup> ChannelProductGroups => Set<ChannelProductGroup>();
    public DbSet<StockAlert> StockAlerts => Set<StockAlert>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CollectionItem> CollectionItems => Set<CollectionItem>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("storefront");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StorefrontDbContext).Assembly);
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
