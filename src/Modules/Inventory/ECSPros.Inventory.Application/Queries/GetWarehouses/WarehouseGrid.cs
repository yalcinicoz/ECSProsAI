using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Queries.GetWarehouses;

/// <summary>
/// Depolar DataGrid şeması (2026-09-09).
///
/// ★ NEDEN AYRI UÇ: mevcut <c>GET /inventory/warehouses</c> TÜM depoları döner ve yedi ekranın dropdown
/// kaynağıdır (stok, transfer, paketleme istasyonu, toplama görevi, iade detayı, satın alma…). Onu
/// sayfalamak o ekranları kırar; liste ekranı için sayfalı <c>/inventory/warehouses/grid</c> eklendi.
/// </summary>
public static class WarehouseGrid
{
    public static readonly GridSchema<Warehouse> Schema = new GridSchema<Warehouse>()
        .Text("code", w => w.Code)
        .Text("name", w => GridJson.Text(w.NameI18n, "tr"))
        .Text("address", w => w.Address)
        .Text("erpCode", w => w.ErpCode)
        .Enum("warehouseType", w => w.WarehouseType)
        .Bool("isSellableOnline", w => w.IsSellableOnline)
        .Bool("isActive", w => w.IsActive)
        .Bool("isCentral", w => w.IsCentral)
        .Number("sortOrder", w => w.SortOrder)
        .Number("reservePriority", w => w.ReservePriority)
        .Number("sectionCount", w => w.Sections.Count(x => !x.IsDeleted))
        .Date("createdAt", w => w.CreatedAt)
        .Sort("code", w => w.Code)
        .Sort("name", w => GridJson.Text(w.NameI18n, "tr"))
        .Sort("address", w => w.Address)
        .Sort("warehouseType", w => w.WarehouseType)
        .Sort("isSellableOnline", w => w.IsSellableOnline)
        .Sort("isActive", w => w.IsActive)
        .Sort("sortOrder", w => w.SortOrder)
        .Sort("reservePriority", w => w.ReservePriority)
        .Sort("sectionCount", w => w.Sections.Count(x => !x.IsDeleted))
        .Sort("createdAt", w => w.CreatedAt)
        .DefaultSort(w => w.SortOrder)
        .TieBreaker(w => w.Id);

    public static IQueryable<Warehouse> ApplyNamed(IQueryable<Warehouse> query, WarehouseListFilters f)
    {
        if (f.ActiveOnly) query = query.Where(w => w.IsActive);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToLower();
            query = query.Where(w => w.Code.ToLower().Contains(term)
                || GridJson.Text(w.NameI18n, "tr").ToLower().Contains(term));
        }
        return query;
    }

    public static IQueryable<Warehouse> ApplyAll(IQueryable<Warehouse> query, WarehouseListFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);
}

public record WarehouseListFilters(bool ActiveOnly = false, string? Search = null);

public record WarehouseGridRow(
    Guid Id, string Code, Dictionary<string, string> NameI18n, string WarehouseType, string? Address,
    bool IsSellableOnline, bool IsActive, int SortOrder, int ReservePriority, bool IsCentral,
    string? ErpCode, int SectionCount, DateTime CreatedAt);

public record GetWarehousesGridQuery(
    WarehouseListFilters Filters, int Page = 1, int PageSize = 20, GridRequest? Grid = null)
    : IRequest<Result<PagedResult<WarehouseGridRow>>>;

public class GetWarehousesGridQueryHandler(IInventoryDbContext db)
    : IRequestHandler<GetWarehousesGridQuery, Result<PagedResult<WarehouseGridRow>>>
{
    public async Task<Result<PagedResult<WarehouseGridRow>>> Handle(GetWarehousesGridQuery r, CancellationToken ct)
    {
        var q = WarehouseGrid.ApplyAll(db.Warehouses.AsNoTracking(), r.Filters, r.Grid);
        var total = await q.CountAsync(ct);
        var items = await WarehouseGrid.Schema.ApplySort(q, r.Grid)
            .Skip((Math.Max(1, r.Page) - 1) * r.PageSize)
            .Take(r.PageSize)
            .Select(w => new WarehouseGridRow(
                w.Id, w.Code, w.NameI18n, w.WarehouseType, w.Address, w.IsSellableOnline, w.IsActive,
                w.SortOrder, w.ReservePriority, w.IsCentral, w.ErpCode,
                w.Sections.Count(x => !x.IsDeleted), w.CreatedAt))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<WarehouseGridRow>(items, total, r.Page, r.PageSize));
    }
}

public record WarehouseExportRow(
    string Code, string Name, string WarehouseType, string? Address, bool IsSellableOnline,
    bool IsActive, int SortOrder, int ReservePriority, bool IsCentral, string? ErpCode, int SectionCount, DateTime CreatedAt);

public record ExportWarehousesQuery(WarehouseListFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<WarehouseExportRow>>>;

public class ExportWarehousesQueryHandler(IInventoryDbContext db)
    : IRequestHandler<ExportWarehousesQuery, Result<GridExportSource<WarehouseExportRow>>>
{
    public async Task<Result<GridExportSource<WarehouseExportRow>>> Handle(ExportWarehousesQuery r, CancellationToken ct)
    {
        var q = WarehouseGrid.ApplyAll(db.Warehouses.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<WarehouseExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");

        var rows = WarehouseGrid.Schema.ApplySort(q, r.Grid).Select(w => new WarehouseExportRow(
            w.Code, GridJson.Text(w.NameI18n, "tr"), w.WarehouseType, w.Address, w.IsSellableOnline,
            w.IsActive, w.SortOrder, w.ReservePriority, w.IsCentral, w.ErpCode,
            w.Sections.Count(x => !x.IsDeleted), w.CreatedAt));
        return Result.Success(new GridExportSource<WarehouseExportRow>(count, rows));
    }
}
