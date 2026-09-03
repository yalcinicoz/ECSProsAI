using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetProducts;

public class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, Result<PagedResult<ProductListDto>>>
{
    private readonly ICatalogDbContext _context;

    public GetProductsQueryHandler(ICatalogDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedResult<ProductListDto>>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Products.AsQueryable();

        if (request.ActiveOnly)
            query = query.Where(x => x.IsSaleOpen);

        if (request.ProductGroupId.HasValue)
            query = query.Where(x => x.ProductGroupId == request.ProductGroupId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim().ToLower();
            query = query.Where(x => x.Code.ToLower().Contains(s)
                || Helpers.PgJsonFunctions.JsonText(x.NameI18n, "tr")!.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var orderedQuery = string.Equals(request.Sort, "newest", StringComparison.OrdinalIgnoreCase)
            ? query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            : query.OrderBy(x => x.Code);

        var items = await orderedQuery
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new ProductListDto(
                x.Id,
                x.Code,
                x.NameI18n,
                x.ProductGroupId,
                x.IsSaleOpen,
                x.Variants.Count))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<ProductListDto>(items, totalCount, request.Page, request.PageSize));
    }
}
