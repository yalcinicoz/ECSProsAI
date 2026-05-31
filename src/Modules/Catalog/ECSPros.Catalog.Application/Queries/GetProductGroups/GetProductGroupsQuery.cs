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
    bool IsActive,
    int SortOrder,
    bool HasProducts,
    List<ProductGroupAttributeDto> Attributes,
    List<ProductGroupAxisSubAttributeDto> AxisSubAttributes
);

public record ProductGroupAttributeDto(
    Guid Id,
    Guid AttributeTypeId,
    string AttributeTypeCode,
    Dictionary<string, string> AttributeTypeNameI18n,
    bool IsVariant,
    bool IsRequired,
    bool IsPrimaryAxis,
    int SortOrder
);

public record ProductGroupAxisSubAttributeDto(
    Guid Id,
    Guid AxisAttributeTypeId,
    string AxisAttributeTypeCode,
    Dictionary<string, string> AxisAttributeTypeNameI18n,
    Guid SubAttributeTypeId,
    string SubAttributeTypeCode,
    Dictionary<string, string> SubAttributeTypeNameI18n,
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
            .Include(pg => pg.AxisSubAttributes.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.AxisAttributeType)
            .Include(pg => pg.AxisSubAttributes.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.SubAttributeType)
            .AsQueryable();

        if (request.ActiveOnly)
            query = query.Where(pg => pg.IsActive);

        var list = await query.OrderBy(pg => pg.SortOrder).ToListAsync(ct);

        var groupIds = list.Select(pg => pg.Id).ToList();
        var productGroupIds = await _db.Products
            .Where(p => groupIds.Contains(p.ProductGroupId))
            .Select(p => p.ProductGroupId)
            .Distinct()
            .ToListAsync(ct);
        var groupsWithProducts = new HashSet<Guid>(productGroupIds);

        var dto = list.Select(pg => new ProductGroupDto(
            pg.Id, pg.Code, pg.NameI18n, pg.IsActive, pg.SortOrder,
            groupsWithProducts.Contains(pg.Id),
            pg.Attributes.Where(a => !a.IsDeleted)
                         .OrderBy(a => a.SortOrder)
                         .Select(a => new ProductGroupAttributeDto(
                             a.Id, a.AttributeTypeId, a.AttributeType.Code,
                             a.AttributeType.NameI18n, a.IsVariant, a.IsRequired, a.IsPrimaryAxis, a.SortOrder))
                         .ToList(),
            pg.AxisSubAttributes
                .OrderBy(s => s.AxisAttributeTypeId).ThenBy(s => s.SortOrder)
                .Select(s => new ProductGroupAxisSubAttributeDto(
                    s.Id,
                    s.AxisAttributeTypeId, s.AxisAttributeType.Code, s.AxisAttributeType.NameI18n,
                    s.SubAttributeTypeId, s.SubAttributeType.Code, s.SubAttributeType.NameI18n,
                    s.IsRequired, s.SortOrder))
                .ToList()
        )).ToList();

        return Result.Success(dto);
    }
}
