using ECSPros.Storefront.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Services;

public interface IStorefrontDbContext
{
    DbSet<NavigationMenu> NavigationMenus { get; }
    DbSet<NavNode> NavNodes { get; }
    DbSet<ChannelProduct> ChannelProducts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
