using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetImageSets;

/// <summary>
/// Resim setleri DataGrid şeması (2026-09-09, tur 12).
///
/// ★ AYRI UÇ: mevcut <c>GET /catalog/image-sets</c> TAM liste döner ve üç yerin kaynağıdır
/// (ürün Resimler sekmesi set seçicisi, toplu görsel yükleme, bu ekranın "fallback set" seçicisi);
/// sayfalamak onları kırar. Liste ekranı <c>/catalog/image-sets/grid</c> kullanır.
///
/// <para><c>gorselSayisi</c> ve <c>kullanildi</c>: bir set silinebilir mi sorusunun yanıtı — resmi
/// olan set silinmez. Sayı satırda gösterilir, ekran "silinebilir" süzgecini bu bayraktan kurar.</para>
/// </summary>
public static class ImageSetGrid
{
    public static readonly GridSchema<ImageSet> Schema = new GridSchema<ImageSet>()
        .Text("code", x => x.Code)
        .Text("name", x => x.Name)
        .Text("fallback", x => x.FallbackSet != null ? x.FallbackSet.Name : null)
        .Text("cdnBaseUrl", x => x.CdnBaseUrl)
        .Bool("isActive", x => x.IsActive)
        .Bool("isDefault", x => x.IsDefault)
        .Bool("fallbackVar", x => x.FallbackSetId != null)
        .Bool("kullanildi", x => x.ProductImages.Any())
        .Number("sortPriority", x => x.SortPriority)
        .Number("gorselSayisi", x => x.ProductImages.Count())
        .Date("olusturma", x => x.CreatedAt)
        .Sort("code", x => x.Code)
        .Sort("name", x => x.Name)
        .Sort("fallback", x => x.FallbackSet != null ? x.FallbackSet.Name : null)
        .Sort("isActive", x => x.IsActive)
        .Sort("isDefault", x => x.IsDefault)
        .Sort("sortPriority", x => x.SortPriority)
        .Sort("gorselSayisi", x => x.ProductImages.Count())
        .Sort("olusturma", x => x.CreatedAt)
        // Eski ekranın sırası: öncelik, sonra ad.
        .DefaultSort(x => x.SortPriority, desc: false)
        .TieBreaker(x => x.Name);

    public static IQueryable<ImageSet> ApplyNamed(IQueryable<ImageSet> query, ImageSetFiltreleri f)
    {
        if (f.YalnizAktif) query = query.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(f.Arama))
        {
            var t = f.Arama.Trim().ToLower();
            query = query.Where(x => x.Code.ToLower().Contains(t) || x.Name.ToLower().Contains(t));
        }
        return query;
    }

    public static IQueryable<ImageSet> ApplyAll(IQueryable<ImageSet> query, ImageSetFiltreleri f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);

    public static IQueryable<ImageSetGridRow> Projeksiyon(IQueryable<ImageSet> query)
        => query.Select(x => new ImageSetGridRow(
            x.Id, x.Code, x.Name, x.IsDefault, x.FallbackSetId,
            x.FallbackSet != null ? x.FallbackSet.Name : null,
            x.SortPriority, x.IsActive, x.CdnBaseUrl, x.ProductImages.Count()));
}

/// <summary>Liste satırı = ImageSetDto + silinebilirlik için görsel sayısı ve CDN adresi.</summary>
public record ImageSetGridRow(
    Guid Id, string Code, string Name, bool IsDefault, Guid? FallbackSetId, string? FallbackSetName,
    int SortPriority, bool IsActive, string? CdnBaseUrl, int GorselSayisi);

public record ImageSetFiltreleri(bool YalnizAktif = false, string? Arama = null);

public record GetImageSetsGridQuery(
    ImageSetFiltreleri Filtreler, int Page = 1, int PageSize = 50, GridRequest? Grid = null)
    : IRequest<Result<PagedResult<ImageSetGridRow>>>;

public class GetImageSetsGridQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetImageSetsGridQuery, Result<PagedResult<ImageSetGridRow>>>
{
    public async Task<Result<PagedResult<ImageSetGridRow>>> Handle(GetImageSetsGridQuery r, CancellationToken ct)
    {
        var q = ImageSetGrid.ApplyAll(db.ImageSets.AsNoTracking(), r.Filtreler, r.Grid);
        var toplam = await q.CountAsync(ct);   // sayım SAYFALAMADAN ÖNCE
        var satirlar = await ImageSetGrid.Projeksiyon(ImageSetGrid.Schema.ApplySort(q, r.Grid)
                .Skip((Math.Max(1, r.Page) - 1) * r.PageSize).Take(r.PageSize))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<ImageSetGridRow>(satirlar, toplam, r.Page, r.PageSize));
    }
}

public record ImageSetExportRow(
    string Code, string Name, string? Fallback, int SortPriority, int GorselSayisi,
    string? CdnBaseUrl, bool IsDefault, bool IsActive);

public record ExportImageSetsQuery(ImageSetFiltreleri Filtreler, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<ImageSetExportRow>>>;

public class ExportImageSetsQueryHandler(ICatalogDbContext db)
    : IRequestHandler<ExportImageSetsQuery, Result<GridExportSource<ImageSetExportRow>>>
{
    public async Task<Result<GridExportSource<ImageSetExportRow>>> Handle(ExportImageSetsQuery r, CancellationToken ct)
    {
        var q = ImageSetGrid.ApplyAll(db.ImageSets.AsNoTracking(), r.Filtreler, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<ImageSetExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = ImageSetGrid.Schema.ApplySort(q, r.Grid).Select(x => new ImageSetExportRow(
            x.Code, x.Name, x.FallbackSet != null ? x.FallbackSet.Name : null,
            x.SortPriority, x.ProductImages.Count(), x.CdnBaseUrl, x.IsDefault, x.IsActive));
        return Result.Success(new GridExportSource<ImageSetExportRow>(count, rows));
    }
}
