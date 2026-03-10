using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetAttributeTypes;

public record GetAttributeTypesQuery(bool ActiveOnly = true) : IRequest<Result<List<AttributeTypeDto>>>;

public record AttributeTypeDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    string DataType,
    bool IsActive,
    int SortOrder,
    List<AttributeValueDto> Values
);

public record AttributeValueDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    bool IsActive,
    int SortOrder
);

public class GetAttributeTypesQueryHandler : IRequestHandler<GetAttributeTypesQuery, Result<List<AttributeTypeDto>>>
{
    private readonly ICatalogDbContext _db;

    public GetAttributeTypesQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<List<AttributeTypeDto>>> Handle(GetAttributeTypesQuery request, CancellationToken ct)
    {
        var query = _db.AttributeTypes.Include(a => a.Values).AsQueryable();
        if (request.ActiveOnly)
            query = query.Where(a => a.IsActive);

        var list = await query.OrderBy(a => a.SortOrder).ThenBy(a => a.Code).ToListAsync(ct);

        var dto = list.Select(a => new AttributeTypeDto(
            a.Id, a.Code, a.NameI18n, a.DataType, a.IsActive, a.SortOrder,
            a.Values.Where(v => !v.IsDeleted)
                    .OrderBy(v => v.SortOrder)
                    .Select(v => new AttributeValueDto(v.Id, v.Code, v.NameI18n, v.IsActive, v.SortOrder))
                    .ToList()
        )).ToList();

        return Result.Success(dto);
    }
}
