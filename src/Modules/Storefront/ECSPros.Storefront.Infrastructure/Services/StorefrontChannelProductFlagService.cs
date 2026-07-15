using ECSPros.Shared.Contracts;
using ECSPros.Storefront.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Infrastructure.Services;

/// <summary>B11: IChannelProductFlagService implementasyonu (storefront.channel_products).</summary>
public class StorefrontChannelProductFlagService(StorefrontDbContext db) : IChannelProductFlagService
{
    public async Task<HashSet<Guid>> GetFeaturedProductIdsAsync(Guid firmPlatformId, CancellationToken ct = default)
    {
        var simdi = DateTime.UtcNow;
        var ids = await db.ChannelProducts.AsNoTracking()
            .Where(cp => cp.FirmPlatformId == firmPlatformId
                      && cp.FeaturedFrom != null && cp.FeaturedFrom <= simdi
                      && (cp.FeaturedUntil == null || cp.FeaturedUntil >= simdi))
            .Select(cp => cp.ProductId)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    /// <summary>M2/M3: kanaldan çıkarılan (IsActive=false) VEYA an itibarıyla durdurulmuş ürünler.</summary>
    public async Task<HashSet<Guid>> GetChannelExcludedProductIdsAsync(Guid firmPlatformId, CancellationToken ct = default)
    {
        var simdi = DateTime.UtcNow;
        var ids = await db.ChannelProducts.AsNoTracking()
            .Where(cp => cp.FirmPlatformId == firmPlatformId
                      && (!cp.IsActive
                          || (cp.SaleStoppedFrom != null && cp.SaleStoppedFrom <= simdi
                              && (cp.SaleStoppedUntil == null || cp.SaleStoppedUntil >= simdi))))
            .Select(cp => cp.ProductId)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }
}
