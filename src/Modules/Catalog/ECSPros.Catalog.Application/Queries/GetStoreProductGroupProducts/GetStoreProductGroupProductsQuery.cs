using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetStoreProductGroupProducts;

public record GetStoreProductGroupProductsQuery(
    Guid ProductGroupId,
    Guid FirmPlatformId,
    int Page = 1,
    int PageSize = 24) : IRequest<Result<StoreProductGroupProductsDto>>;

public record StoreProductGroupProductsDto(
    Guid ProductGroupId,
    Dictionary<string, string> GroupNameI18n,
    PagedResult<StoreGroupProductItemDto> Products);

public record StoreGroupProductItemDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? ShortDescriptionI18n,
    string? MainImageUrl,
    decimal MinPrice,
    decimal? CompareAtPrice,
    bool IsActive);

public class GetStoreProductGroupProductsQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetStoreProductGroupProductsQuery, Result<StoreProductGroupProductsDto>>
{
    public async Task<Result<StoreProductGroupProductsDto>> Handle(
        GetStoreProductGroupProductsQuery request, CancellationToken ct)
    {
        var group = await db.ProductGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == request.ProductGroupId && g.IsActive, ct);

        if (group is null)
            return Result.Failure<StoreProductGroupProductsDto>("Ürün grubu bulunamadı.");

        // Alt gruplar dahil tüm grup ID'lerini topla
        var allGroupIds = await CollectGroupIds(db, request.ProductGroupId, ct);

        var q = db.Products
            .AsNoTracking()
            .Where(p => allGroupIds.Contains(p.ProductGroupId) && p.IsActive);

        var total = await q.CountAsync(ct);

        var products = await q
            .Include(p => p.Variants).ThenInclude(v => v.Images)
            .Include(p => p.Variants).ThenInclude(v => v.FirmPlatformVariants)
            .OrderBy(p => p.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var items = products.Select(p =>
        {
            var activeVariants = p.Variants.Where(v => v.IsActive).ToList();
            var platformPrices = activeVariants
                .SelectMany(v => v.FirmPlatformVariants
                    .Where(fpv => fpv.FirmPlatformId == request.FirmPlatformId && fpv.IsActive))
                .Select(fpv => fpv.Price ?? 0)
                .Where(price => price > 0)
                .ToList();

            var minPrice = platformPrices.Count > 0
                ? platformPrices.Min()
                : activeVariants.MinBy(v => v.BasePrice)?.BasePrice ?? 0;

            var mainImage = activeVariants
                .SelectMany(v => v.Images)
                .Where(i => i.IsMain)
                .OrderBy(i => i.SortOrder)
                .FirstOrDefault()?.ImageUrl;

            return new StoreGroupProductItemDto(
                p.Id, p.Code, p.NameI18n, p.ShortDescriptionI18n,
                mainImage, minPrice, null, p.IsActive);
        }).ToList();

        var paged = new PagedResult<StoreGroupProductItemDto>(items, total, request.Page, request.PageSize);
        return Result.Success(new StoreProductGroupProductsDto(group.Id, group.NameI18n, paged));
    }

    private static Task<List<Guid>> CollectGroupIds(
        ICatalogDbContext db, Guid rootId, CancellationToken ct)
        => Task.FromResult(new List<Guid> { rootId });
}
