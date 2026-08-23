using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Contracts.Channels;
using ECSPros.Shared.Contracts;
using ECSPros.Storefront.Application.Services.ChannelScoping;
using ECSPros.Storefront.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Storefront.Infrastructure.Services;

/// <summary>B11: IChannelProductFlagService implementasyonu (storefront.channel_products).</summary>
public class StorefrontChannelProductFlagService(StorefrontDbContext db, ICatalogDbContext catDb, IMemoryCache cache, IChannelCapabilityResolver capabilityResolver) : IChannelProductFlagService
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

    /// <summary>
    /// Kanalda GÖRÜNMEYEN ürünler (deny-set). M2/M3: çıkarılan (IsActive=false) VEYA an itibarıyla durdurulmuş.
    /// F1 kapsam: + manuel hariç tutulan (IsExcluded); kanal kapsamı filter|mixed ise + kapsam dışı tüm katalog
    /// (InScope satırı olmayan ürünler). all (ya da kapsam tanımsız) kanalda davranış F1 öncesiyle AYNIDIR.
    /// 60 sn süreç-içi önbellek; kanal kararı/kapsam komutları anahtarı siler (ChannelProductCacheKeys).
    /// </summary>
    public async Task<HashSet<Guid>> GetChannelExcludedProductIdsAsync(Guid firmPlatformId, CancellationToken ct = default)
    {
        var key = ChannelProductCacheKeys.Excluded(firmPlatformId);
        if (cache.TryGetValue(key, out HashSet<Guid>? cached) && cached is not null) return cached;

        var simdi = DateTime.UtcNow;
        var deny = (await db.ChannelProducts.AsNoTracking()
            .Where(cp => cp.FirmPlatformId == firmPlatformId
                      && (!cp.IsActive || cp.IsExcluded
                          || (cp.SaleStoppedFrom != null && cp.SaleStoppedFrom <= simdi
                              && (cp.SaleStoppedUntil == null || cp.SaleStoppedUntil >= simdi))))
            .Select(cp => cp.ProductId)
            .ToListAsync(ct)).ToHashSet();

        // F5 K6: kanalın kapalı olduğu kaynaklar (seller/supply) görünmez — hangi kapsam tipinde olursa olsun.
        var caps = await capabilityResolver.GetAsync(firmPlatformId, ct);
        var allowedSources = ChannelScopeResolver.AllowedSourceTypes(caps);
        if (allowedSources.Count < 3)
        {
            var disallowedIds = await catDb.Products.AsNoTracking()
                .Where(p => p.SourceType != "own" && !allowedSources.Contains(p.SourceType))
                .Select(p => p.Id).ToListAsync(ct);
            foreach (var id in disallowedIds) deny.Add(id);
        }

        var filterBased = await db.ChannelScopes.AsNoTracking()
            .AnyAsync(s => s.FirmPlatformId == firmPlatformId && (s.FillType == "filter" || s.FillType == "mixed"), ct);
        if (filterBased)
        {
            var inScope = (await db.ChannelProducts.AsNoTracking()
                .Where(cp => cp.FirmPlatformId == firmPlatformId && cp.InScope && !cp.IsExcluded)
                .Select(cp => cp.ProductId).ToListAsync(ct)).ToHashSet();
            var allIds = await catDb.Products.AsNoTracking().Select(p => p.Id).ToListAsync(ct);
            foreach (var id in allIds) if (!inScope.Contains(id)) deny.Add(id);
        }

        cache.Set(key, deny, ChannelProductCacheKeys.Ttl);
        return deny;
    }
}
