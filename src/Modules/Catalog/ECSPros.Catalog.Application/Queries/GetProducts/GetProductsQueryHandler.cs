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
        // DataGrid F4 (2026-09-08): adlandırılmış filtreler + global arama + beyaz listeli grid filtreleri TEK yerden (ProductGrid).
        var filters = new ProductListFilters(request.Search, request.ProductGroupId, request.ActiveOnly);
        var query = ProductGrid.ApplyAll(_context.Products.AsQueryable(), filters, request.Grid);

        var totalCount = await query.CountAsync(cancellationToken);

        var orderedQuery = ProductGrid.ApplySort(query, request.Grid, request.Sort);

        var items = await orderedQuery
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new ProductListDto(
                x.Id,
                x.Code,
                x.NameI18n,
                x.ProductGroupId,
                x.IsSaleOpen,
                x.Variants.Count,
                x.Stats != null ? x.Stats.ImageState : "none",
                x.Stats != null ? x.Stats.ImageCount : 0,
                x.Stats != null ? x.Stats.StockQuantity : 0,
                x.Stats != null ? x.Stats.StockAvailable : 0,
                x.Stats != null && x.Stats.VideoCount > 0))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<ProductListDto>(items, totalCount, request.Page, request.PageSize));
    }
}
