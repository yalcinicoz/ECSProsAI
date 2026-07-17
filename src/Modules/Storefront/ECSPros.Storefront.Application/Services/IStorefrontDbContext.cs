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
    DbSet<Favorite> Favorites { get; }
    DbSet<Collection> Collections { get; }
    DbSet<CollectionItem> CollectionItems { get; }
    DbSet<ProductReview> ProductReviews { get; }
    DbSet<ProductReviewPhoto> ProductReviewPhotos { get; }
    DbSet<SavedSearch> SavedSearches { get; }
    DbSet<ViewedProduct> ViewedProducts { get; }
    DbSet<CartRemovedItem> CartRemovedItems { get; }
    DbSet<ContactMessage> ContactMessages { get; }
    DbSet<NewsletterSubscription> NewsletterSubscriptions { get; }
    DbSet<PageBlock> PageBlocks { get; }
    DbSet<PageBlockItem> PageBlockItems { get; }
    DbSet<PublishedSnapshot> PublishedSnapshots { get; }
    DbSet<PublishLog> PublishLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
