using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetProductGroups;

/// <summary>
/// Ürün grupları DataGrid şeması (2026-09-09).
///
/// ★ NEDEN AYRI UÇ: mevcut <c>GET /catalog/product-groups</c> TÜM grupları özellikleriyle döner ve
/// dört ekranın dropdown kaynağıdır (ürün listesi grup süzgeci, kanal kategori detayı, komisyon,
/// FilterBuilder). Onu sayfalamak o ekranları kırar — bu yüzden liste sayfası için ayrı, sayfalı ve
/// yalın bir uç (<c>/catalog/product-groups/grid</c>) eklendi; eski uç dokunulmadan kaldı.
/// </summary>
public static class ProductGroupGrid
{
    public static readonly GridSchema<ProductGroup> Schema = new GridSchema<ProductGroup>()
        .Text("code", g => g.Code)
        .Text("name", g => GridJson.Text(g.NameI18n, "tr"))
        .Bool("isActive", g => g.IsActive)
        .Bool("hasProducts", g => g.Products.Any(p => !p.IsDeleted))
        .Number("sortOrder", g => g.SortOrder)
        .Number("attributeCount", g => g.Attributes.Count(a => !a.IsDeleted))
        .Number("variantCount", g => g.Attributes.Count(a => !a.IsDeleted && a.IsVariant))
        .Number("productCount", g => g.Products.Count(p => !p.IsDeleted))
        .Date("createdAt", g => g.CreatedAt)
        .Sort("code", g => g.Code)
        .Sort("name", g => GridJson.Text(g.NameI18n, "tr"))
        .Sort("isActive", g => g.IsActive)
        .Sort("sortOrder", g => g.SortOrder)
        .Sort("attributeCount", g => g.Attributes.Count(a => !a.IsDeleted))
        .Sort("variantCount", g => g.Attributes.Count(a => !a.IsDeleted && a.IsVariant))
        .Sort("productCount", g => g.Products.Count(p => !p.IsDeleted))
        .Sort("createdAt", g => g.CreatedAt)
        .DefaultSort(g => g.SortOrder)
        .TieBreaker(g => g.Id);

    public static IQueryable<ProductGroup> ApplyNamed(IQueryable<ProductGroup> query, ProductGroupListFilters f)
    {
        if (f.ActiveOnly) query = query.Where(g => g.IsActive);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToLower();
            query = query.Where(g => g.Code.ToLower().Contains(term)
                || GridJson.Text(g.NameI18n, "tr").ToLower().Contains(term));
        }
        return query;
    }

    public static IQueryable<ProductGroup> ApplyAll(IQueryable<ProductGroup> query, ProductGroupListFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);
}

public record ProductGroupListFilters(bool ActiveOnly = false, string? Search = null);

/// <summary>Liste satırı — özellik/eksen ayrıntısı YOK (o bilgi grup detay ekranında).</summary>
public record ProductGroupGridRow(
    Guid Id, string Code, Dictionary<string, string> NameI18n, bool IsActive, int SortOrder,
    int AttributeCount, int VariantCount, int ProductCount, bool HasProducts, DateTime CreatedAt);

public record GetProductGroupsGridQuery(
    ProductGroupListFilters Filters, int Page = 1, int PageSize = 20, GridRequest? Grid = null)
    : IRequest<Result<PagedResult<ProductGroupGridRow>>>;

public class GetProductGroupsGridQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetProductGroupsGridQuery, Result<PagedResult<ProductGroupGridRow>>>
{
    public async Task<Result<PagedResult<ProductGroupGridRow>>> Handle(GetProductGroupsGridQuery r, CancellationToken ct)
    {
        var q = ProductGroupGrid.ApplyAll(db.ProductGroups.AsNoTracking(), r.Filters, r.Grid);
        var total = await q.CountAsync(ct);
        var items = await ProductGroupGrid.Schema.ApplySort(q, r.Grid)
            .Skip((Math.Max(1, r.Page) - 1) * r.PageSize)
            .Take(r.PageSize)
            .Select(g => new ProductGroupGridRow(
                g.Id, g.Code, g.NameI18n, g.IsActive, g.SortOrder,
                g.Attributes.Count(a => !a.IsDeleted),
                g.Attributes.Count(a => !a.IsDeleted && a.IsVariant),
                g.Products.Count(p => !p.IsDeleted),
                g.Products.Any(p => !p.IsDeleted),
                g.CreatedAt))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<ProductGroupGridRow>(items, total, r.Page, r.PageSize));
    }
}

public record ExportProductGroupsQuery(ProductGroupListFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<ProductGroupExportRow>>>;

public record ProductGroupExportRow(
    string Code, string Name, bool IsActive, int SortOrder,
    int AttributeCount, int VariantCount, int ProductCount, DateTime CreatedAt);

public class ExportProductGroupsQueryHandler(ICatalogDbContext db)
    : IRequestHandler<ExportProductGroupsQuery, Result<GridExportSource<ProductGroupExportRow>>>
{
    public async Task<Result<GridExportSource<ProductGroupExportRow>>> Handle(ExportProductGroupsQuery r, CancellationToken ct)
    {
        var q = ProductGroupGrid.ApplyAll(db.ProductGroups.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<ProductGroupExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");

        var rows = ProductGroupGrid.Schema.ApplySort(q, r.Grid).Select(g => new ProductGroupExportRow(
            g.Code, GridJson.Text(g.NameI18n, "tr"), g.IsActive, g.SortOrder,
            g.Attributes.Count(a => !a.IsDeleted),
            g.Attributes.Count(a => !a.IsDeleted && a.IsVariant),
            g.Products.Count(p => !p.IsDeleted), g.CreatedAt));
        return Result.Success(new GridExportSource<ProductGroupExportRow>(count, rows));
    }
}
