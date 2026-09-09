using ECSPros.Catalog.Application.Queries.GetAttributeTypes;

namespace ECSPros.Api.Grid;

/// <summary>Özellik tipleri Excel kolonları (kod kilitli).</summary>
public static class AttributeTypeExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<AttributeTypeExportRow>> All = new GridExportColumn<AttributeTypeExportRow>[]
    {
        new("code", "Kod", r => r.Code, Locked: true),
        new("name", "Ad", r => r.Name),
        new("dataType", "Veri Tipi", r => AttributeTypeGrid.DataTypeLabel(r.DataType)),
        new("valueCount", "Değer", r => r.ValueCount),
        new("groupCount", "Kullanan Grup", r => r.GroupCount),
        new("useInFilter", "Filtrede", r => r.UseInFilter ? "Evet" : "Hayır"),
        new("sortOrder", "Sıra", r => r.SortOrder),
        new("isActive", "Aktif", r => r.IsActive ? "Evet" : "Hayır"),
        new("createdAt", "Oluşturma", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}
