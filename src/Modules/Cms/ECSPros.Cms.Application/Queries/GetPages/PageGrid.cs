using ECSPros.Cms.Application.Services;
using ECSPros.Cms.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Cms.Application.Queries.GetPages;

/// <summary>
/// İçerik sayfaları DataGrid şeması (2026-09-09).
///
/// ★ AYRI UÇ: mevcut <c>GET /cms/pages</c> TAM liste döner ve sayfa seçicilerini besler
/// (CMS sayfa detayı, sipariş detayındaki sözleşme bağlantısı); sayfalamak onları kırar.
/// Liste ekranı sayfalı <c>/cms/pages/grid</c> kullanır.
///
/// <para>Y3 (K2): <c>.Kanal(FirmPlatformId)</c> — sayfa bir kanala aittir, kapsam dışı kanalın
/// içeriği listede/Excel'de görünmez.</para>
/// <para><c>lastContentUpdatedAt</c> (sözleşme sürüm tarihi) hesaplanmış bir alandır:
/// en son bölüm/sayfa değişikliği — kuralı <c>GetStoreLegalPages</c> ile aynı tutulmalı.</para>
/// </summary>
public static class PageGrid
{
    public static readonly GridSchema<Page> Schema = new GridSchema<Page>()
        .Kanal(p => p.FirmPlatformId)   // Y3 (K2)
        .Text("code", p => p.Code)
        .Text("name", p => GridJson.Text(p.NameI18n, "tr"))
        .Text("slug", p => GridJson.Text(p.SlugI18n, "tr"))
        .Enum("pageType", p => p.PageType)
        .Bool("isActive", p => p.IsActive)
        .Bool("scheduled", p => p.PublishAt != null || p.UnpublishAt != null)
        .Number("sectionCount", p => p.Sections.Count(s => !s.IsDeleted))
        .Date("publishAt", p => p.PublishAt)
        .Date("unpublishAt", p => p.UnpublishAt)
        .Date("createdAt", p => p.CreatedAt)
        .Date("lastContentUpdatedAt", p => p.Sections.Where(s => !s.IsDeleted).Select(s => s.UpdatedAt)
            .Concat(new DateTime?[] { p.UpdatedAt ?? p.CreatedAt }).Max())
        .Guid("firmPlatformId", p => p.FirmPlatformId)
        .Guid("templateId", p => p.TemplateId)
        .Sort("code", p => p.Code)
        .Sort("name", p => GridJson.Text(p.NameI18n, "tr"))
        .Sort("pageType", p => p.PageType)
        .Sort("isActive", p => p.IsActive)
        .Sort("sectionCount", p => p.Sections.Count(s => !s.IsDeleted))
        .Sort("publishAt", p => p.PublishAt)
        .Sort("createdAt", p => p.CreatedAt)
        .Sort("lastContentUpdatedAt", p => p.Sections.Where(s => !s.IsDeleted).Select(s => s.UpdatedAt)
            .Concat(new DateTime?[] { p.UpdatedAt ?? p.CreatedAt }).Max())
        .DefaultSort(p => p.Code, desc: false)
        .TieBreaker(p => p.Id);

    public static IQueryable<Page> ApplyNamed(IQueryable<Page> query, PageListFilters f)
    {
        if (f.FirmPlatformId.HasValue) query = query.Where(p => p.FirmPlatformId == f.FirmPlatformId);
        if (f.ActiveOnly) query = query.Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(f.PageType)) query = query.Where(p => p.PageType == f.PageType);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var t = f.Search.Trim().ToLower();
            query = query.Where(p => p.Code.ToLower().Contains(t)
                || GridJson.Text(p.NameI18n, "tr").ToLower().Contains(t));
        }
        return query;
    }

    public static IQueryable<Page> ApplyAll(
        IQueryable<Page> query, PageListFilters f, GridRequest? grid,
        IReadOnlyCollection<Guid>? kanalKisiti = null)
        => Schema.ApplyKanalKapsami(
            Schema.ApplyFilters(ApplyNamed(query, f), grid),
            kanalKisiti ?? grid?.KanalKisiti);
}

public record PageListFilters(
    Guid? FirmPlatformId = null, bool ActiveOnly = false, string? PageType = null, string? Search = null);

public record PageGridRow(
    Guid Id, string Code, Dictionary<string, string> NameI18n, Dictionary<string, string> SlugI18n,
    string PageType, bool IsActive, DateTime? PublishAt, DateTime? UnpublishAt,
    Guid FirmPlatformId, int SectionCount, DateTime? LastContentUpdatedAt);

public record GetPagesGridQuery(
    PageListFilters Filters, int Page = 1, int PageSize = 20,
    IReadOnlyCollection<Guid>? KanalKisiti = null, GridRequest? Grid = null)
    : IRequest<Result<PagedResult<PageGridRow>>>;

public class GetPagesGridQueryHandler(ICmsDbContext db)
    : IRequestHandler<GetPagesGridQuery, Result<PagedResult<PageGridRow>>>
{
    public async Task<Result<PagedResult<PageGridRow>>> Handle(GetPagesGridQuery r, CancellationToken ct)
    {
        var q = PageGrid.ApplyAll(db.Pages.AsNoTracking(), r.Filters, r.Grid, r.KanalKisiti ?? r.Grid?.KanalKisiti);
        var total = await q.CountAsync(ct);
        var items = await PageGrid.Schema.ApplySort(q, r.Grid)
            .Skip((Math.Max(1, r.Page) - 1) * r.PageSize).Take(r.PageSize)
            .Select(p => new PageGridRow(
                p.Id, p.Code, p.NameI18n, p.SlugI18n, p.PageType, p.IsActive, p.PublishAt, p.UnpublishAt,
                p.FirmPlatformId, p.Sections.Count(s => !s.IsDeleted),
                p.Sections.Where(s => !s.IsDeleted).Select(s => s.UpdatedAt)
                    .Concat(new DateTime?[] { p.UpdatedAt ?? p.CreatedAt }).Max()))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<PageGridRow>(items, total, r.Page, r.PageSize));
    }
}

public record PageExportRow(
    string Code, string Name, string Slug, string PageType, bool IsActive,
    DateTime? PublishAt, DateTime? UnpublishAt, int SectionCount, DateTime? LastContentUpdatedAt);

public record ExportPagesQuery(PageListFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<PageExportRow>>>;

public class ExportPagesQueryHandler(ICmsDbContext db)
    : IRequestHandler<ExportPagesQuery, Result<GridExportSource<PageExportRow>>>
{
    public async Task<Result<GridExportSource<PageExportRow>>> Handle(ExportPagesQuery r, CancellationToken ct)
    {
        var q = PageGrid.ApplyAll(db.Pages.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<PageExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = PageGrid.Schema.ApplySort(q, r.Grid).Select(p => new PageExportRow(
            p.Code, GridJson.Text(p.NameI18n, "tr"), GridJson.Text(p.SlugI18n, "tr"), p.PageType, p.IsActive,
            p.PublishAt, p.UnpublishAt, p.Sections.Count(s => !s.IsDeleted),
            p.Sections.Where(s => !s.IsDeleted).Select(s => s.UpdatedAt)
                .Concat(new DateTime?[] { p.UpdatedAt ?? p.CreatedAt }).Max()));
        return Result.Success(new GridExportSource<PageExportRow>(count, rows));
    }
}
