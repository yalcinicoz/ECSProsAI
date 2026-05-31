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
    bool RequiresFilterColor,
    List<AttributeValueDto> Values,
    List<AxisSubAttributeSchemaDto> AxisSubAttributeSchema
);

public record AttributeValueDto(
    Guid Id,
    Dictionary<string, string> NameI18n,
    bool IsActive,
    int SortOrder,
    List<ValuePropertyDto> Properties,
    List<FilterColorMappingDto> FilterColors,
    int UsedInProductCount = 0
);

public record FilterColorMappingDto(
    Guid FilterColorId,
    string Code,
    Dictionary<string, string> NameI18n,
    string? HexCode
);

public record ValuePropertyDto(
    Guid SubAttributeTypeId,
    string SubAttributeTypeCode,
    Dictionary<string, string> SubAttributeTypeNameI18n,
    string Value
);

public record AxisSubAttributeSchemaDto(
    Guid SubAttributeTypeId,
    string SubAttributeTypeCode,
    Dictionary<string, string> SubAttributeTypeNameI18n,
    bool IsRequired
);

public class GetAttributeTypesQueryHandler : IRequestHandler<GetAttributeTypesQuery, Result<List<AttributeTypeDto>>>
{
    private readonly ICatalogDbContext _db;

    public GetAttributeTypesQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<List<AttributeTypeDto>>> Handle(GetAttributeTypesQuery request, CancellationToken ct)
    {
        var query = _db.AttributeTypes
            .Include(a => a.Values.Where(v => !v.IsDeleted))
                .ThenInclude(v => v.Properties.Where(p => !p.IsDeleted))
                    .ThenInclude(p => p.SubAttributeType)
            .Include(a => a.Values.Where(v => !v.IsDeleted))
                .ThenInclude(v => v.FilterColors.Where(fc => !fc.IsDeleted))
                    .ThenInclude(fc => fc.FilterColor)
            .Include(a => a.AxisSubAttributes.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.SubAttributeType)
            .AsQueryable();

        if (request.ActiveOnly)
            query = query.Where(a => a.IsActive);

        var list = await query.OrderBy(a => a.SortOrder).ThenBy(a => a.Code).ToListAsync(ct);

        // Her attribute value için kaç farklı üründe kullanıldığını say (tek sorgu)
        var allValueIds = list.SelectMany(a => a.Values).Select(v => v.Id).ToList();

        var directCounts = await _db.ProductAttributes
            .Where(pa => pa.AttributeValueId.HasValue && allValueIds.Contains(pa.AttributeValueId.Value))
            .GroupBy(pa => pa.AttributeValueId!.Value)
            .Select(g => new { ValueId = g.Key, ProductCount = g.Select(pa => pa.ProductId).Distinct().Count() })
            .ToListAsync(ct);

        var variantCounts = await _db.ProductVariantAttributes
            .Where(pva => allValueIds.Contains(pva.AttributeValueId))
            .GroupBy(pva => pva.AttributeValueId)
            .Select(g => new { ValueId = g.Key, ProductCount = g.Select(pva => pva.Variant.ProductId).Distinct().Count() })
            .ToListAsync(ct);

        var countMap = new Dictionary<Guid, int>();
        foreach (var row in directCounts)
            countMap[row.ValueId] = row.ProductCount;
        foreach (var row in variantCounts)
            countMap[row.ValueId] = (countMap.GetValueOrDefault(row.ValueId)) + row.ProductCount;

        // Eksen alt özellik şeması: aynı SubAttributeTypeId birden fazla gruptan gelebilir, distinct al
        var dto = list.Select(a => new AttributeTypeDto(
            a.Id, a.Code, a.NameI18n, a.DataType, a.IsActive, a.SortOrder, a.RequiresFilterColor,
            a.Values
                .OrderBy(v => v.SortOrder)
                .Select(v => new AttributeValueDto(
                    v.Id, v.NameI18n, v.IsActive, v.SortOrder,
                    v.Properties
                        .OrderBy(p => p.SubAttributeType.SortOrder)
                        .Select(p => new ValuePropertyDto(
                            p.SubAttributeTypeId,
                            p.SubAttributeType.Code,
                            p.SubAttributeType.NameI18n,
                            p.Value))
                        .ToList(),
                    v.FilterColors
                        .OrderBy(fc => fc.FilterColor.SortOrder)
                        .Select(fc => new FilterColorMappingDto(
                            fc.FilterColorId,
                            fc.FilterColor.Code,
                            fc.FilterColor.NameI18n,
                            fc.FilterColor.HexCode))
                        .ToList(),
                    countMap.GetValueOrDefault(v.Id, 0)))
                .ToList(),
            a.AxisSubAttributes
                .GroupBy(s => s.SubAttributeTypeId)
                .Select(g => g.First())
                .OrderBy(s => s.SortOrder)
                .Select(s => new AxisSubAttributeSchemaDto(
                    s.SubAttributeTypeId,
                    s.SubAttributeType.Code,
                    s.SubAttributeType.NameI18n,
                    s.IsRequired))
                .ToList()
        )).ToList();

        return Result.Success(dto);
    }
}
