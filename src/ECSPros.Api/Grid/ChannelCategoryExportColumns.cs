using ECSPros.Storefront.Application.Queries.GetChannelCategories;

namespace ECSPros.Api.Grid;

/// <summary>Kanal kategorileri Excel kolonları (ad kilitli).</summary>
public static class ChannelCategoryExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<ChannelCategoryExportRow>> All = new GridExportColumn<ChannelCategoryExportRow>[]
    {
        new("name", "Kategori", r => r.Name, Locked: true),
        new("slug", "Slug", r => r.Slug),
        new("fillType", "Dolum", r => ChannelCategoryGrid.FillLabel(r.FillType)),
        new("productGroupCount", "Ürün Grubu", r => r.ProductGroupCount),
        new("productCount", "Ürün", r => r.ProductCount),
        new("badgeLabel", "Rozet", r => r.BadgeLabel),
        new("sortOrder", "Sıra", r => r.SortOrder),
        new("status", "Durum", r => ChannelCategoryGrid.StatusLabel(r.Status)),
        new("createdAt", "Oluşturma", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}
