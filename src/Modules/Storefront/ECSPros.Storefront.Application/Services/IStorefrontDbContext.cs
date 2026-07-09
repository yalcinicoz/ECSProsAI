using ECSPros.Storefront.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Services;

public interface IStorefrontDbContext
{
    DbSet<NavigationMenu> NavigationMenus { get; }
    DbSet<NavNode> NavNodes { get; }
    DbSet<ChannelProduct> ChannelProducts { get; }
    DbSet<ChannelVariant> ChannelVariants { get; }
    DbSet<ChannelCategory> ChannelCategories { get; }
    DbSet<ChannelCategoryGroup> ChannelCategoryGroups { get; }
    DbSet<ChannelCategoryProduct> ChannelCategoryProducts { get; }
    DbSet<ChannelProductGroup> ChannelProductGroups { get; }
    DbSet<StockAlert> StockAlerts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
