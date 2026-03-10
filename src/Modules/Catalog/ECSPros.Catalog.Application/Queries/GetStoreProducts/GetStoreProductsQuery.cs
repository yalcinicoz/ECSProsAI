using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetStoreProducts;

public record GetStoreProductsQuery(
    Guid FirmPlatformId,
    Guid? CategoryId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 24) : IRequest<Result<PagedResult<StoreProductDto>>>;

public record StoreProductDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? ShortDescriptionI18n,
    string? MainImageUrl,
    decimal MinPrice,
    decimal? CompareAtPrice,
    bool IsActive);

public class GetStoreProductsQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetStoreProductsQuery, Result<PagedResult<StoreProductDto>>>
{
    public async Task<Result<PagedResult<StoreProductDto>>> Handle(GetStoreProductsQuery request, CancellationToken ct)
    {
        var q = db.Products
            .AsNoTracking()
            .Include(p => p.Variants).ThenInclude(v => v.Images)
            .Include(p => p.Variants).ThenInclude(v => v.FirmPlatformVariants)
            .Where(p => p.IsActive);

        if (request.CategoryId.HasValue)
        {
            var productIds = db.CategoryProducts
                .Where(cp => cp.CategoryId == request.CategoryId.Value)
                .Select(cp => cp.ProductId);
            q = q.Where(p => productIds.Contains(p.Id));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            q = q.Where(p => p.Code.ToLower().Contains(search));
        }

        var total = await q.CountAsync(ct);
        var products = await q
            .OrderBy(p => p.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var items = products.Select(p =>
        {
            var activeVariants = p.Variants.Where(v => v.IsActive).ToList();
            var platformPrices = activeVariants
                .SelectMany(v => v.FirmPlatformVariants.Where(fpv => fpv.FirmPlatformId == request.FirmPlatformId && fpv.IsActive))
                .Select(fpv => fpv.Price ?? 0)
                .Where(price => price > 0)
                .ToList();

            var minPrice = platformPrices.Any() ? platformPrices.Min() : activeVariants.MinBy(v => v.BasePrice)?.BasePrice ?? 0;
            var mainImage = activeVariants.SelectMany(v => v.Images).Where(i => i.IsMain).OrderBy(i => i.SortOrder).FirstOrDefault()?.ImageUrl;

            return new StoreProductDto(
                p.Id, p.Code, p.NameI18n, p.ShortDescriptionI18n,
                mainImage, minPrice, null, p.IsActive);
        }).ToList();

        return Result.Success(new PagedResult<StoreProductDto>(items, total, request.Page, request.PageSize));
    }
}
