using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetProductGroups;

public record GetProductGroupsQuery(bool ActiveOnly = true) : IRequest<Result<List<ProductGroupDto>>>;

public record ProductGroupDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    Guid? ParentId,
    bool IsActive,
    int SortOrder,
    List<ProductGroupAttributeDto> Attributes
);

public record ProductGroupAttributeDto(
    Guid Id,
    Guid AttributeTypeId,
    string AttributeTypeCode,
    Dictionary<string, string> AttributeTypeNameI18n,
    bool IsVariant,
    bool IsRequired,
    int SortOrder
);

public class GetProductGroupsQueryHandler : IRequestHandler<GetProductGroupsQuery, Result<List<ProductGroupDto>>>
{
    private readonly ICatalogDbContext _db;

    public GetProductGroupsQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<List<ProductGroupDto>>> Handle(GetProductGroupsQuery request, CancellationToken ct)
    {
        var query = _db.ProductGroups
            .Include(pg => pg.Attributes).ThenInclude(a => a.AttributeType)
            .AsQueryable();

        if (request.ActiveOnly)
            query = query.Where(pg => pg.IsActive);

        var list = await query.OrderBy(pg => pg.SortOrder).ThenBy(pg => pg.Code).ToListAsync(ct);

        var dto = list.Select(pg => new ProductGroupDto(
            pg.Id, pg.Code, pg.NameI18n, pg.ParentId, pg.IsActive, pg.SortOrder,
            pg.Attributes.Where(a => !a.IsDeleted)
                         .OrderBy(a => a.SortOrder)
                         .Select(a => new ProductGroupAttributeDto(
                             a.Id, a.AttributeTypeId, a.AttributeType.Code,
                             a.AttributeType.NameI18n, a.IsVariant, a.IsRequired, a.SortOrder))
                         .ToList()
        )).ToList();

        return Result.Success(dto);
    }
}
