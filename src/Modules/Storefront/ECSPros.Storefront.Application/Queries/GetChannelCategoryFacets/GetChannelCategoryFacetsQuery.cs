using ECSPros.Catalog.Application.Queries.GetStoreFacets;
using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetChannelCategoryFacets;

public record GetChannelCategoryFacetsQuery(
    Guid ChannelCategoryId) : IRequest<Result<StoreFacetsDto>>;

public class GetChannelCategoryFacetsQueryHandler(
    IStorefrontDbContext sfDb,
    ICatalogDbContext catDb,
    ICacheService cache)
    : IRequestHandler<GetChannelCategoryFacetsQuery, Result<StoreFacetsDto>>
{
    public async Task<Result<StoreFacetsDto>> Handle(
        GetChannelCategoryFacetsQuery request, CancellationToken ct)
    {
        var cacheKey = $"channelcat:facets:{request.ChannelCategoryId}";
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

        var result = await GetStoreFacetsQueryHandler.BuildFacets(catDb, productIds, ct);
        if (result.IsSuccess)
        {
            try { await cache.SetAsync(cacheKey, result.Value, TimeSpan.FromMinutes(10), ct); } catch { /* best-effort */ }
        }
        return result;
    }
}
