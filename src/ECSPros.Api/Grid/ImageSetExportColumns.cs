using ECSPros.Catalog.Application.Queries.GetImageSets;

namespace ECSPros.Api.Grid;

/// <summary>Resim setleri Excel kolonları (set kodu kilitli — görseller ona bağlı).</summary>
public static class ImageSetExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<ImageSetExportRow>> All = new GridExportColumn<ImageSetExportRow>[]
    {
        new("code", "Kod", r => r.Code, Locked: true),
        new("name", "Ad", r => r.Name),
        new("fallback", "Yedek Set", r => r.Fallback),
        new("sortPriority", "Öncelik", r => r.SortPriority),
        new("gorselSayisi", "Görsel", r => r.GorselSayisi),
        new("cdnBaseUrl", "CDN Adresi", r => r.CdnBaseUrl),
        new("isDefault", "Varsayılan", r => r.IsDefault ? "Evet" : "Hayır"),
        new("isActive", "Aktif", r => r.IsActive ? "Evet" : "Hayır"),
    };
}
