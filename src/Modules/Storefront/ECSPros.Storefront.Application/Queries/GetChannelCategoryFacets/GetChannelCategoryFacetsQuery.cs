using ECSPros.Catalog.Application.Queries.GetStoreFacets;
using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetChannelCategoryFacets;

public record GetChannelCategoryFacetsQuery(
    Guid ChannelCategoryId,
    // Stok görünürlüğü — kategori listesiyle facet sayıları tutarlı (2026-07-14).
    bool ShowOutOfStock = false,
    DateTime? OutOfStockSince = null) : IRequest<Result<StoreFacetsDto>>;

public class GetChannelCategoryFacetsQueryHandler(
    IStorefrontDbContext sfDb,
    ICatalogDbContext catDb,
    IInStockProductProvider inStock,
    ICacheService cache)
    : IRequestHandler<GetChannelCategoryFacetsQuery, Result<StoreFacetsDto>>
{
    public async Task<Result<StoreFacetsDto>> Handle(
        GetChannelCategoryFacetsQuery request, CancellationToken ct)
    {
        // v2 + stok paramları: stok filtresi eklendiğinden eski/ayar-farklı girdiler ayrışsın.
        // v3: kanal seçimi/durdurma (M2/M3) geçidi eklendi.
        var cacheKey = $"channelcat:facets:v3:{request.ChannelCategoryId}:{request.ShowOutOfStock}:{request.OutOfStockSince:yyyyMMdd}";
        StoreFacetsDto? cached = null;
        try { cached = await cache.GetAsync<StoreFacetsDto>(cacheKey, ct); } catch { /* Redis erişilemezse taze hesapla */ }
        if (cached is not null)
            return Result.Success(cached);

        var cat = await sfDb.ChannelCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChannelCategoryId, ct);

        if (cat is null)
            return Result.Failure<StoreFacetsDto>("Kanal kategorisi bulunamadı.");

        List<Guid> productIds;

        if (cat.ListingMode == "model")
        {
            // Model modunda: vitrin ürünleri + fallback ilk ürünler
            var groups = await sfDb.ChannelCategoryGroups
                .AsNoTracking()
                .Where(g => g.ChannelCategoryId == request.ChannelCategoryId)
                .Select(g => new { g.ShowcaseProductId, g.ProductGroupId })
                .ToListAsync(ct);

            var showcaseIds = groups
                .Where(g => g.ShowcaseProductId.HasValue)
                .Select(g => g.ShowcaseProductId!.Value)
                .ToList();

            var groupsNeedingFallback = groups
                .Where(g => !g.ShowcaseProductId.HasValue)
                .Select(g => g.ProductGroupId)
                .ToList();

            var fallbackIds = groupsNeedingFallback.Count > 0
                ? (await catDb.Products
                    .AsNoTracking()
                    .Where(p => groupsNeedingFallback.Contains(p.ProductGroupId) && p.IsSaleOpen)
                    .OrderBy(p => p.ProductGroupId).ThenBy(p => p.Id)
                    .Select(p => new { p.Id, p.ProductGroupId })
                    .ToListAsync(ct))
                    .GroupBy(p => p.ProductGroupId)
                    .Select(g => g.First().Id)
                    .ToList()
                : new List<Guid>();

            productIds = showcaseIds.Concat(fallbackIds).Distinct().ToList();
        }
        else if (cat.FillType == "manual")
        {
            productIds = await sfDb.ChannelCategoryProducts
                .Where(p => p.ChannelCategoryId == request.ChannelCategoryId && !p.IsExcluded)
                .Select(p => p.ProductId)
                .ToListAsync(ct);
        }
        else
        {
            // filter / mixed — tüm satışa açık ürünler (yeterli)
            productIds = await catDb.Products
                .AsNoTracking()
                .Where(p => p.IsSaleOpen && catDb.ProductImages.Any(img => img.ProductId == p.Id))
                .Select(p => p.Id)
                .Take(2000)
                .ToListAsync(ct);
        }

        // Stok görünürlüğü: stoğu biteni (kanal açık VE CreatedAt>=eşik değilse) facet'ten çıkar.
        if (productIds.Count > 0)
        {
            var inStockIds = await inStock.GetInStockProductIdsAsync(ct);
            var showOos = request.ShowOutOfStock;
            var oosSince = request.OutOfStockSince;
            var gorunur = (await catDb.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id)
                            && (inStockIds.Contains(p.Id) || (showOos && (oosSince == null || p.CreatedAt >= oosSince))))
                .Select(p => p.Id).ToListAsync(ct)).ToHashSet();
            productIds = productIds.Where(gorunur.Contains).ToList();
        }

        // Kanal seçimi/durdurma (M2/M3): kanaldan çıkarılan/durdurulan ürünü facet'ten de çıkar
        // (liste ile tutarlı sayım). Opt-out: satır yok/seçili → dahil.
        if (productIds.Count > 0)
        {
            var simdi = DateTime.UtcNow;
            var kanalDisi = (await sfDb.ChannelProducts.AsNoTracking()
                .Where(cp => cp.FirmPlatformId == cat.FirmPlatformId && productIds.Contains(cp.ProductId)
                          && (!cp.IsActive
                              || (cp.SaleStoppedFrom != null && cp.SaleStoppedFrom <= simdi
                                  && (cp.SaleStoppedUntil == null || cp.SaleStoppedUntil >= simdi))))
                .Select(cp => cp.ProductId).ToListAsync(ct)).ToHashSet();
            if (kanalDisi.Count > 0)
                productIds = productIds.Where(id => !kanalDisi.Contains(id)).ToList();
        }

        var result = await GetStoreFacetsQueryHandler.BuildFacets(catDb, productIds, ct);
        if (result.IsSuccess)
        {
            try { await cache.SetAsync(cacheKey, result.Value, TimeSpan.FromMinutes(10), ct); } catch { /* best-effort */ }
        }
        return result;
    }
}
