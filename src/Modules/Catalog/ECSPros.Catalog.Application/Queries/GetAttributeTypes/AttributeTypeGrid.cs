using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetAttributeTypes;

/// <summary>
/// Özellik tipleri DataGrid şeması (2026-09-09).
///
/// ★ AYRI UÇ: mevcut <c>GET /catalog/attribute-types</c> TÜM tipleri DEĞERLERİYLE döner ve dört
/// ekranın kaynağıdır (ürün grubu detayı, özellik tipi detayı, pazaryeri eşleme, FilterBuilder);
/// sayfalamak onları kırar. Liste ekranı yalın+sayfalı <c>/attribute-types/grid</c> kullanır.
/// </summary>
public static class AttributeTypeGrid
{
    public static readonly string[] DataTypes = { "select", "multi_select", "text", "number", "boolean", "json" };

    public static readonly GridSchema<AttributeType> Schema = new GridSchema<AttributeType>()
        .Text("code", a => a.Code)
        .Text("name", a => GridJson.Text(a.NameI18n, "tr"))
        .Enum("dataType", a => a.DataType, DataTypes)
        .Bool("isActive", a => a.IsActive)
        .Bool("useInFilter", a => a.UseInFilter)
        .Bool("hasValues", a => a.Values.Any(v => !v.IsDeleted))
        .Number("sortOrder", a => a.SortOrder)
        .Number("valueCount", a => a.Values.Count(v => !v.IsDeleted))
        .Number("groupCount", a => a.ProductGroupAttributes.Count(g => !g.IsDeleted))
        .Date("createdAt", a => a.CreatedAt)
        .Sort("code", a => a.Code)
        .Sort("name", a => GridJson.Text(a.NameI18n, "tr"))
        .Sort("dataType", a => a.DataType)
        .Sort("isActive", a => a.IsActive)
        .Sort("useInFilter", a => a.UseInFilter)
        .Sort("sortOrder", a => a.SortOrder)
        .Sort("valueCount", a => a.Values.Count(v => !v.IsDeleted))
        .Sort("groupCount", a => a.ProductGroupAttributes.Count(g => !g.IsDeleted))
        .Sort("createdAt", a => a.CreatedAt)
        .DefaultSort(a => a.SortOrder)
        .TieBreaker(a => a.Id);

    public static IQueryable<AttributeType> ApplyNamed(IQueryable<AttributeType> query, AttributeTypeFilters f)
    {
        if (f.ActiveOnly) query = query.Where(a => a.IsActive);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var t = f.Search.Trim().ToLower();
            query = query.Where(a => a.Code.ToLower().Contains(t)
                || GridJson.Text(a.NameI18n, "tr").ToLower().Contains(t));
        }
        return query;
    }

    public static IQueryable<AttributeType> ApplyAll(IQueryable<AttributeType> query, AttributeTypeFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);

    public static string DataTypeLabel(string s) => s switch
    {
        "select" => "Tek seçim", "multi_select" => "Çoklu seçim", "text" => "Metin",
        "number" => "Sayı", "boolean" => "Evet/Hayır", "json" => "JSON", _ => s,
    };
}

public record AttributeTypeFilters(bool ActiveOnly = false, string? Search = null);

public record AttributeTypeGridRow(
    Guid Id, string Code, Dictionary<string, string> NameI18n, string DataType, bool IsActive,
    bool UseInFilter, int SortOrder, int ValueCount, int GroupCount, DateTime CreatedAt);

public record GetAttributeTypesGridQuery(AttributeTypeFilters Filters, int Page = 1, int PageSize = 20, GridRequest? Grid = null)
    : IRequest<Result<PagedResult<AttributeTypeGridRow>>>;

public class GetAttributeTypesGridQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetAttributeTypesGridQuery, Result<PagedResult<AttributeTypeGridRow>>>
{
    public async Task<Result<PagedResult<AttributeTypeGridRow>>> Handle(GetAttributeTypesGridQuery r, CancellationToken ct)
    {
        var q = AttributeTypeGrid.ApplyAll(db.AttributeTypes.AsNoTracking(), r.Filters, r.Grid);
        var total = await q.CountAsync(ct);
        var items = await AttributeTypeGrid.Schema.ApplySort(q, r.Grid)
            .Skip((Math.Max(1, r.Page) - 1) * r.PageSize).Take(r.PageSize)
            .Select(a => new AttributeTypeGridRow(
                a.Id, a.Code, a.NameI18n, a.DataType, a.IsActive, a.UseInFilter, a.SortOrder,
                a.Values.Count(v => !v.IsDeleted),
                a.ProductGroupAttributes.Count(g => !g.IsDeleted),
                a.CreatedAt))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<AttributeTypeGridRow>(items, total, r.Page, r.PageSize));
    }
}

public record AttributeTypeExportRow(
    string Code, string Name, string DataType, bool IsActive, bool UseInFilter,
    int SortOrder, int ValueCount, int GroupCount, DateTime CreatedAt);

public record ExportAttributeTypesQuery(AttributeTypeFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<AttributeTypeExportRow>>>;

public class ExportAttributeTypesQueryHandler(ICatalogDbContext db)
    : IRequestHandler<ExportAttributeTypesQuery, Result<GridExportSource<AttributeTypeExportRow>>>
{
    public async Task<Result<GridExportSource<AttributeTypeExportRow>>> Handle(ExportAttributeTypesQuery r, CancellationToken ct)
    {
        var q = AttributeTypeGrid.ApplyAll(db.AttributeTypes.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<AttributeTypeExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = AttributeTypeGrid.Schema.ApplySort(q, r.Grid).Select(a => new AttributeTypeExportRow(
            a.Code, GridJson.Text(a.NameI18n, "tr"), a.DataType, a.IsActive, a.UseInFilter, a.SortOrder,
            a.Values.Count(v => !v.IsDeleted), a.ProductGroupAttributes.Count(g => !g.IsDeleted), a.CreatedAt));
        return Result.Success(new GridExportSource<AttributeTypeExportRow>(count, rows));
    }
}
