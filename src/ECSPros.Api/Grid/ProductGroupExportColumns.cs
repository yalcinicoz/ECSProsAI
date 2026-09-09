using ECSPros.Catalog.Application.Queries.GetProductGroups;

namespace ECSPros.Api.Grid;

/// <summary>Ürün grupları Excel kolonları (kod kilitli).</summary>
public static class ProductGroupExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<ProductGroupExportRow>> All = new GridExportColumn<ProductGroupExportRow>[]
    {
        new("code", "Kod", r => r.Code, Locked: true),
        new("name", "Ad", r => r.Name),
        new("attributeCount", "Özellik", r => r.AttributeCount),
        new("variantCount", "Varyant Ekseni", r => r.VariantCount),
        new("productCount", "Ürün", r => r.ProductCount),
        new("sortOrder", "Sıra", r => r.SortOrder),
        new("isActive", "Aktif", r => r.IsActive ? "Evet" : "Hayır"),
        new("createdAt", "Oluşturma", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}
