using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetChannelCategories;

/// <summary>
/// Kanal kategorileri DataGrid şeması (2026-09-09).
///
/// ★ AYRI UÇ: mevcut <c>GET /navigation/channel-categories</c> TAM liste döner ve iki ekranın daha
/// kaynağıdır (Menü Yerleşimi, Ürün Kartı); sayfalamak onları kırar. Liste ekranı sayfalı
/// <c>/navigation/channel-categories/grid</c> kullanır.
///
/// <para>Y3 (K2): liste tek kanala kilitli olsa bile şema <c>.Kanal(FirmPlatformId)</c> BİLDİRİR ve
/// kapsam ayrıca uygulanır — "kanal kolonu olan her şema kapsam bildirir" değişmezi (KanalKapsamiTests)
/// bunu şart koşuyor. Tek parametreye güvenmek yetmez: kapsam denetimi tek yerde toplanır ki yeni bir
/// uç aynı şemayı kanal kısıtı olmadan kullanamasın.</para>
/// <para><c>tanimsiz</c>: yayında olup hiçbir ürün grubundan sorumlu olmayan kategori — ekranın
/// uyarı çubuğu bu durumu sayar, filtreyle doğrudan süzülebilir.</para>
/// </summary>
public static class ChannelCategoryGrid
{
    public static readonly string[] Statuses = { "published", "draft", "archived" };
    public static readonly string[] FillTypes = { "manual", "filter", "mixed" };

    public static readonly GridSchema<ChannelCategory> Schema = new GridSchema<ChannelCategory>()
        .Kanal(c => c.FirmPlatformId)   // Y3 (K2)
        .Text("name", c => GridJson.Text(c.NameI18n, "tr"))
        .Text("slug", c => c.Slug)
        .Text("badgeLabel", c => c.BadgeLabel)
        .Enum("status", c => c.Status, Statuses)
        .Enum("fillType", c => c.FillType, FillTypes)
        .Enum("listingMode", c => c.ListingMode)
        .Bool("hasImage", c => c.DisplayImageUrl != null && c.DisplayImageUrl != "")
        .Bool("isRoot", c => c.ParentId == null)
        .Bool("tanimsiz", c => c.Status == "published" && !c.CategoryGroups.Any())
        .Number("sortOrder", c => c.SortOrder)
        .Number("productGroupCount", c => c.CategoryGroups.Count)
        .Number("productCount", c => c.CategoryProducts.Count)
        .Date("createdAt", c => c.CreatedAt)
        .Guid("parentId", c => c.ParentId)
        .Sort("name", c => GridJson.Text(c.NameI18n, "tr"))
        .Sort("slug", c => c.Slug)
        .Sort("status", c => c.Status)
        .Sort("fillType", c => c.FillType)
        .Sort("sortOrder", c => c.SortOrder)
        .Sort("productGroupCount", c => c.CategoryGroups.Count)
        .Sort("productCount", c => c.CategoryProducts.Count)
        .Sort("createdAt", c => c.CreatedAt)
        .DefaultSort(c => c.SortOrder, desc: false)
        .TieBreaker(c => c.Id);

    public static IQueryable<ChannelCategory> ApplyNamed(IQueryable<ChannelCategory> query, ChannelCategoryFilters f)
    {
        query = query.Where(c => c.FirmPlatformId == f.FirmPlatformId);
        if (f.ActiveOnly) query = query.Where(c => c.Status == "published");
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var t = f.Search.Trim().ToLower();
            query = query.Where(c => c.Slug.ToLower().Contains(t)
                || GridJson.Text(c.NameI18n, "tr")!.ToLower().Contains(t));
        }
        return query;
    }

    /// <param name="kanalKisiti">Y3 kapsam; verilmezse <c>grid.KanalKisiti</c>.</param>
    public static IQueryable<ChannelCategory> ApplyAll(
        IQueryable<ChannelCategory> query, ChannelCategoryFilters f, GridRequest? grid,
        IReadOnlyCollection<Guid>? kanalKisiti = null)
        => Schema.ApplyKanalKapsami(
            Schema.ApplyFilters(ApplyNamed(query, f), grid),
            kanalKisiti ?? grid?.KanalKisiti);

    public static string StatusLabel(string s) => s switch
    {
        "published" => "Yayında", "draft" => "Taslak", "archived" => "Arşiv", _ => s,
    };

    public static string FillLabel(string s) => s switch
    {
        "manual" => "Manuel", "filter" => "Filtre", "mixed" => "Karma", _ => s,
    };
}

public record ChannelCategoryFilters(Guid FirmPlatformId, bool ActiveOnly = false, string? Search = null);

public record ChannelCategoryGridRow(
    Guid Id, Guid? ParentId, Dictionary<string, string> NameI18n, string Slug, string Status,
    string FillType, int SortOrder, string? DisplayImageUrl, string? BadgeLabel,
    int ProductGroupCount, int ProductCount, DateTime CreatedAt);

public record GetChannelCategoriesGridQuery(
    ChannelCategoryFilters Filters, int Page = 1, int PageSize = 30, GridRequest? Grid = null,
    IReadOnlyCollection<Guid>? KanalKisiti = null)
    : IRequest<Result<PagedResult<ChannelCategoryGridRow>>>;

public class GetChannelCategoriesGridQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetChannelCategoriesGridQuery, Result<PagedResult<ChannelCategoryGridRow>>>
{
    public async Task<Result<PagedResult<ChannelCategoryGridRow>>> Handle(GetChannelCategoriesGridQuery r, CancellationToken ct)
    {
        var q = ChannelCategoryGrid.ApplyAll(db.ChannelCategories.AsNoTracking(), r.Filters, r.Grid,
            r.KanalKisiti ?? r.Grid?.KanalKisiti);
        var total = await q.CountAsync(ct);
        var items = await ChannelCategoryGrid.Schema.ApplySort(q, r.Grid)
            .Skip((Math.Max(1, r.Page) - 1) * r.PageSize).Take(r.PageSize)
            .Select(c => new ChannelCategoryGridRow(
                c.Id, c.ParentId, c.NameI18n, c.Slug, c.Status, c.FillType, c.SortOrder,
                c.DisplayImageUrl, c.BadgeLabel, c.CategoryGroups.Count, c.CategoryProducts.Count, c.CreatedAt))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<ChannelCategoryGridRow>(items, total, r.Page, r.PageSize));
    }
}

public record ChannelCategoryExportRow(
    string Name, string Slug, string Status, string FillType, int SortOrder,
    string? BadgeLabel, int ProductGroupCount, int ProductCount, DateTime CreatedAt);

public record ExportChannelCategoriesQuery(ChannelCategoryFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<ChannelCategoryExportRow>>>;

public class ExportChannelCategoriesQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<ExportChannelCategoriesQuery, Result<GridExportSource<ChannelCategoryExportRow>>>
{
    public async Task<Result<GridExportSource<ChannelCategoryExportRow>>> Handle(ExportChannelCategoriesQuery r, CancellationToken ct)
    {
        var q = ChannelCategoryGrid.ApplyAll(db.ChannelCategories.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<ChannelCategoryExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = ChannelCategoryGrid.Schema.ApplySort(q, r.Grid).Select(c => new ChannelCategoryExportRow(
            GridJson.Text(c.NameI18n, "tr") ?? c.Slug, c.Slug, c.Status, c.FillType, c.SortOrder,
            c.BadgeLabel, c.CategoryGroups.Count, c.CategoryProducts.Count, c.CreatedAt));
        return Result.Success(new GridExportSource<ChannelCategoryExportRow>(count, rows));
    }
}
