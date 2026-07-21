using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetPartnerGroupSchema;

/// <summary>Partner keşif: bir grubun varyant eksenleri + ürün-seviyesi özellikleri + her biri için
/// izin verilen DEĞER HAVUZU. Entegratör bunu çekip yalnız bu değerleri gönderir (Kapı 1 buna göre
/// doğrular). Değerler havuzdan ADA göre (AttributeValue.NameI18n) tanımlıdır — kod yoktur.</summary>
public record GetPartnerGroupSchemaQuery(string GroupCode) : IRequest<Result<PartnerGroupSchemaDto>>;

public record PartnerGroupSchemaDto(
    string Code,
    Dictionary<string, string> Name,
    List<PartnerSchemaAttributeDto> VariantAxes,
    List<PartnerSchemaAttributeDto> Attributes);

public record PartnerSchemaAttributeDto(
    string Code,
    Dictionary<string, string> Name,
    bool Required,
    bool PrimaryAxis,
    List<PartnerSchemaValueDto> AllowedValues);

public record PartnerSchemaValueDto(string Value, Dictionary<string, string> Name, string? HexCode);

public class GetPartnerGroupSchemaQueryHandler : IRequestHandler<GetPartnerGroupSchemaQuery, Result<PartnerGroupSchemaDto>>
{
    private readonly ICatalogDbContext _db;

    public GetPartnerGroupSchemaQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<PartnerGroupSchemaDto>> Handle(GetPartnerGroupSchemaQuery request, CancellationToken ct)
    {
        var group = await _db.ProductGroups
            .Include(g => g.Attributes.Where(a => !a.IsDeleted)).ThenInclude(a => a.AttributeType)
            .FirstOrDefaultAsync(g => g.Code == request.GroupCode && g.IsActive, ct);

        if (group is null)
            return Result.Failure<PartnerGroupSchemaDto>($"Grup bulunamadı: {request.GroupCode}");

        var typeIds = group.Attributes.Select(a => a.AttributeTypeId).Distinct().ToList();

        // Havuz: her attribute type için aktif tüm değerler (grup değer kısıtlaması yok — global havuz).
        var values = await _db.AttributeValues
            .Where(v => typeIds.Contains(v.AttributeTypeId) && v.IsActive)
            .OrderBy(v => v.SortOrder)
            .ToListAsync(ct);

        var valuesByType = values
            .GroupBy(v => v.AttributeTypeId)
            .ToDictionary(
                grp => grp.Key,
                grp => grp.Select(v => new PartnerSchemaValueDto(
                    v.NameI18n.TryGetValue("tr", out var tr) ? tr : v.NameI18n.Values.FirstOrDefault() ?? "",
                    v.NameI18n, v.HexCode)).ToList());

        PartnerSchemaAttributeDto Map(Domain.Entities.ProductGroupAttribute a) => new(
            a.AttributeType.Code,
            a.AttributeType.NameI18n,
            a.IsRequired,
            a.IsPrimaryAxis,
            valuesByType.TryGetValue(a.AttributeTypeId, out var vs) ? vs : new List<PartnerSchemaValueDto>());

        var dto = new PartnerGroupSchemaDto(
            group.Code,
            group.NameI18n,
            group.Attributes.Where(a => a.IsVariant).OrderBy(a => a.SortOrder).Select(Map).ToList(),
            group.Attributes.Where(a => !a.IsVariant).OrderBy(a => a.SortOrder).Select(Map).ToList());

        return Result.Success(dto);
    }
}
