using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;

namespace ECSPros.Catalog.Application.Queries.GetProducts;

public record GetProductsQuery(
    string? Search = null,
    Guid? ProductGroupId = null,
    bool ActiveOnly = true,
    int Page = 1,
    int PageSize = 20,
    string? Sort = null,
    GridRequest? Grid = null) : IRequest<Result<PagedResult<ProductListDto>>>;
    // Grid (2026-09-08, DataGrid F4): beyaz listeli f.* filtreleri + sort/dir (ProductGrid.Schema); null → eski davranış

public record ProductListDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    Guid ProductGroupId,
    bool IsActive,
    int VariantCount,
    // 2026-09-11 kapsamlı filtre kolonları (mv_product_stats; kayıt yoksa none/0)
    string ImageState = "none",
    int ImageCount = 0,
    int StockQuantity = 0,
    int StockAvailable = 0);

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
