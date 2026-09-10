using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Shelf;

public record BinCountLineDto(Guid Id, Guid VariantId, int Expected, int Counted, int Diff,
    string? ProductCode, string? ProductName, string? OptionsText, string? ImageUrl);

public record BinCountListDto(Guid Id, Guid WarehouseId, string WarehouseCode, Guid BinId, string BinCode, string BinBarcode,
    string Status, DateTime StartedAt, DateTime? FinishedAt, DateTime? AppliedAt, int ExpectedTotal, int CountedTotal, int DiffTotal, int LineCount, string? Notes)
{
    public string StatusLabel => DurumEtiketleri.Etiket(DurumEtiketleri.Panel.SayimDurumu, Status);
}

public record BinCountDetailDto(BinCountListDto Header, List<BinCountLineDto> Lines);

/// <summary>Raf sayımları DataGrid şeması (F4 kalıbı).</summary>
public static class BinCountGrid
{
    public static readonly string[] Statuses = { "open", "finished", "applied", "cancelled" };

    public static readonly GridSchema<BinCount> Schema = new GridSchema<BinCount>()
        .Text("binCode", c => c.Bin.Code)
        .Text("binBarcode", c => c.Bin.Barcode)
        .Enum("status", c => c.Status, Statuses)
        .Date("startedAt", c => c.StartedAt)
        .Date("finishedAt", c => c.FinishedAt)
        .Number("expectedTotal", c => c.ExpectedTotal)
        .Number("countedTotal", c => c.CountedTotal)
        .Number("diffTotal", c => c.DiffTotal)
        .Guid("warehouseId", c => c.WarehouseId)
        .Guid("binId", c => c.BinId)
        .Sort("binCode", c => c.Bin.Code)
        .Sort("status", c => c.Status)
        .Sort("startedAt", c => c.StartedAt)
        .Sort("diffTotal", c => c.DiffTotal)
        .Sort("expectedTotal", c => c.ExpectedTotal)
        .Sort("countedTotal", c => c.CountedTotal)
        .DefaultSort(c => c.StartedAt, desc: true)
        .TieBreaker(c => c.Id);

    public static IQueryable<BinCount> ApplyAll(IQueryable<BinCount> q, string? search, GridRequest? grid)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var t = search.Trim().ToLower();
            q = q.Where(c => c.Bin.Code.ToLower().Contains(t) || c.Bin.Barcode.ToLower().Contains(t));
        }
        return Schema.ApplyFilters(q, grid);
    }

    public static string StatusLabel(string s) => DurumEtiketleri.Etiket(DurumEtiketleri.Panel.SayimDurumu, s);
}

public record GetBinCountsQuery(int Page, int PageSize, string? Search, GridRequest? Grid) : IRequest<Result<PagedResult<BinCountListDto>>>;

public class GetBinCountsQueryHandler(IInventoryDbContext db) : IRequestHandler<GetBinCountsQuery, Result<PagedResult<BinCountListDto>>>
{
    public async Task<Result<PagedResult<BinCountListDto>>> Handle(GetBinCountsQuery r, CancellationToken ct)
    {
        var q = BinCountGrid.ApplyAll(db.BinCounts.AsNoTracking(), r.Search, r.Grid);
        var total = await q.CountAsync(ct);
        var items = await BinCountGrid.Schema.ApplySort(q, r.Grid)
            .Skip((r.Page - 1) * r.PageSize).Take(r.PageSize)
            .Select(c => new BinCountListDto(c.Id, c.WarehouseId, c.Warehouse.Code, c.BinId, c.Bin.Code, c.Bin.Barcode, c.Status,
                c.StartedAt, c.FinishedAt, c.AppliedAt, c.ExpectedTotal, c.CountedTotal, c.DiffTotal, c.Lines.Count, c.Notes))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<BinCountListDto>(items, total, r.Page, r.PageSize));
    }
}

public record BinCountExportRow(string WarehouseCode, string BinCode, string BinBarcode, string Status, DateTime StartedAt,
    DateTime? FinishedAt, DateTime? AppliedAt, int ExpectedTotal, int CountedTotal, int DiffTotal, string? Notes);

public record ExportBinCountsQuery(string? Search, GridRequest Grid, int MaxRows) : IRequest<Result<GridExportSource<BinCountExportRow>>>;

public class ExportBinCountsQueryHandler(IInventoryDbContext db) : IRequestHandler<ExportBinCountsQuery, Result<GridExportSource<BinCountExportRow>>>
{
    public async Task<Result<GridExportSource<BinCountExportRow>>> Handle(ExportBinCountsQuery r, CancellationToken ct)
    {
        var q = BinCountGrid.ApplyAll(db.BinCounts.AsNoTracking(), r.Search, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<BinCountExportRow>>($"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = BinCountGrid.Schema.ApplySort(q, r.Grid).Select(c => new BinCountExportRow(c.Warehouse.Code, c.Bin.Code, c.Bin.Barcode, c.Status,
            c.StartedAt, c.FinishedAt, c.AppliedAt, c.ExpectedTotal, c.CountedTotal, c.DiffTotal, c.Notes));
        return Result.Success(new GridExportSource<BinCountExportRow>(count, rows));
    }
}

public record GetBinCountDetailQuery(Guid CountId) : IRequest<Result<BinCountDetailDto>>;

public class GetBinCountDetailQueryHandler(IInventoryDbContext db, IProductService products) : IRequestHandler<GetBinCountDetailQuery, Result<BinCountDetailDto>>
{
    public async Task<Result<BinCountDetailDto>> Handle(GetBinCountDetailQuery r, CancellationToken ct)
    {
        var c = await db.BinCounts.AsNoTracking().Include(x => x.Lines).Include(x => x.Bin).Include(x => x.Warehouse)
            .FirstOrDefaultAsync(x => x.Id == r.CountId, ct);
        if (c is null) return Result.Failure<BinCountDetailDto>("Sayım oturumu bulunamadı.");
        var g = await GetBinContentsQueryHandler.GorunumAsync(products, c.Lines.Select(l => l.VariantId), ct);
        var header = new BinCountListDto(c.Id, c.WarehouseId, c.Warehouse.Code, c.BinId, c.Bin.Code, c.Bin.Barcode, c.Status,
            c.StartedAt, c.FinishedAt, c.AppliedAt, c.ExpectedTotal, c.CountedTotal, c.DiffTotal, c.Lines.Count, c.Notes);
        var lines = c.Lines.OrderByDescending(l => Math.Abs(l.Diff)).ThenByDescending(l => l.ExpectedQuantity).Select(l =>
        {
            g.TryGetValue(l.VariantId, out var v);
            return new BinCountLineDto(l.Id, l.VariantId, l.ExpectedQuantity, l.CountedQuantity, l.Diff,
                v?.ProductCode, v?.ProductNameI18n.GetValueOrDefault("tr") ?? v?.ProductNameI18n.Values.FirstOrDefault(), v?.OptionsText, v?.ImageUrl);
        }).ToList();
        return Result.Success(new BinCountDetailDto(header, lines));
    }
}
