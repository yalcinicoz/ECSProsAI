using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Catalog.Application.Queries.GetProducts;

public record GetProductsQuery(
    string? Search = null,
    Guid? ProductGroupId = null,
    bool ActiveOnly = true,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<ProductListDto>>>;

public record ProductListDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    Guid ProductGroupId,
    bool IsActive,
    int VariantCount);

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
