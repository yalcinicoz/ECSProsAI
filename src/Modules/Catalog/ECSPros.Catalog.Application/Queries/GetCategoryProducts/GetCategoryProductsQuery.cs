using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetCategoryProducts;

public record GetCategoryProductsQuery(
    Guid CategoryId,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<CategoryProductItemDto>>>;

public record CategoryProductItemDto(
    Guid ProductId,
    string Code,
    Dictionary<string, string> NameI18n,
    string? MainImageUrl,
    decimal BasePrice,
    bool IsActive,
    int SortOrder,
    bool IsPinned);

public class GetCategoryProductsQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetCategoryProductsQuery, Result<PagedResult<CategoryProductItemDto>>>
{
    public async Task<Result<PagedResult<CategoryProductItemDto>>> Handle(
        GetCategoryProductsQuery request, CancellationToken ct)
    {
        var q = db.CategoryProducts
            .AsNoTracking()
            .Where(cp => cp.CategoryId == request.CategoryId)
            .OrderBy(cp => cp.SortOrder).ThenBy(cp => cp.CreatedAt);

        var total = await q.CountAsync(ct);

        var rows = await q
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(cp => new
            {
                cp.ProductId,
                cp.SortOrder,
                cp.IsPinned,
                cp.Product.Code,
                cp.Product.NameI18n,
                cp.Product.BasePrice,
                cp.Product.IsActive,
                MainImage = cp.Product.Variants
                    .Where(v => v.IsActive)
                    .SelectMany(v => v.Images)
                    .Where(i => i.IsMain)
                    .OrderBy(i => i.SortOrder)
                    .Select(i => (string?)i.ImageUrl)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new CategoryProductItemDto(
            r.ProductId, r.Code, r.NameI18n, r.MainImage,
            r.BasePrice, r.IsActive, r.SortOrder, r.IsPinned)).ToList();

        return Result.Success(new PagedResult<CategoryProductItemDto>(items, total, request.Page, request.PageSize));
    }
}
