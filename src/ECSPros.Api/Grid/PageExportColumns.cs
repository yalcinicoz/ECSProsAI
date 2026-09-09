using ECSPros.Cms.Application.Queries.GetPages;

namespace ECSPros.Api.Grid;

/// <summary>İçerik sayfaları Excel kolonları (kod kilitli).</summary>
public static class PageExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<PageExportRow>> All = new GridExportColumn<PageExportRow>[]
    {
        new("code", "Kod", r => r.Code, Locked: true),
        new("name", "Ad", r => r.Name),
        new("slug", "Slug", r => r.Slug),
        new("pageType", "Tür", r => r.PageType),
        new("sectionCount", "Bölüm", r => r.SectionCount),
        new("isActive", "Aktif", r => r.IsActive ? "Evet" : "Hayır"),
        new("publishAt", "Yayın", r => r.PublishAt.HasValue ? GridExportWriter.ToIstanbul(r.PublishAt.Value) : null),
        new("unpublishAt", "Yayın Sonu", r => r.UnpublishAt.HasValue ? GridExportWriter.ToIstanbul(r.UnpublishAt.Value) : null),
        new("lastContentUpdatedAt", "Son İçerik", r => r.LastContentUpdatedAt.HasValue ? GridExportWriter.ToIstanbul(r.LastContentUpdatedAt.Value) : null),
    };
}
