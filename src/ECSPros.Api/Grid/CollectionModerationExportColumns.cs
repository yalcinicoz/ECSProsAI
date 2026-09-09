using ECSPros.Storefront.Application.Queries.GetCollectionsForModeration;

namespace ECSPros.Api.Grid;

/// <summary>Koleksiyon moderasyonu Excel kolonları (ad kilitli).</summary>
public static class CollectionModerationExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<CollectionModerationExportRow>> All = new GridExportColumn<CollectionModerationExportRow>[]
    {
        new("name", "Ad", r => r.Name, Locked: true),
        new("description", "Açıklama", r => r.Description),
        new("itemCount", "Ürün", r => r.ItemCount),
        new("viewCount", "Görüntülenme", r => r.ViewCount),
        new("isPublic", "Herkese Açık", r => r.IsPublic ? "Evet" : "Hayır"),
        new("isShareable", "Paylaşılabilir", r => r.IsShareable ? "Evet" : "Hayır"),
        new("isQuickSave", "Hızlı Kayıt", r => r.IsQuickSave ? "Evet" : "Hayır"),
        new("status", "Durum", r => CollectionModerationGrid.StatusLabel(r.Status)),
        new("createdAt", "Oluşturma", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
        new("moderatedAt", "Moderasyon", r => r.ModeratedAt.HasValue ? GridExportWriter.ToIstanbul(r.ModeratedAt.Value) : null),
    };
}
