using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Queries.GetTransfers;

/// <summary>
/// Depo transferleri DataGrid şeması (2026-09-09): beyaz listeli filtre/sıralama + mevcut adlandırılmış
/// filtreler (fromWarehouseId/toWarehouseId/status/transferType) + global arama (kod, not).
///
/// <para>Depo ADINA göre süzme/sıralama <c>FromWarehouse</c>/<c>ToWarehouse</c> navigation'ları
/// üzerinden yapılır (aynı modül, join güvenli).</para>
/// </summary>
public static class TransferGrid
{
    public static readonly string[] Statuses =
        { "draft", "pending", "picking", "picked", "in_transit", "delivered", "completed", "cancelled" };

    public static readonly GridSchema<TransferRequest> Schema = new GridSchema<TransferRequest>()
        .Text("code", t => t.Code)
        .Text("notes", t => t.Notes)
        .Text("fromWarehouse", t => GridJson.Text(t.FromWarehouse.NameI18n, "tr"))
        .Text("toWarehouse", t => GridJson.Text(t.ToWarehouse.NameI18n, "tr"))
        .Enum("status", t => t.Status, Statuses)
        .Enum("transferType", t => t.TransferType)
        .Number("itemCount", t => t.Items.Count)
        .Bool("open", t => t.Status != "completed" && t.Status != "cancelled")
        .Date("requestedAt", t => t.RequestedAt)
        .Date("createdAt", t => t.CreatedAt)
        .Guid("fromWarehouseId", t => t.FromWarehouseId)
        .Guid("toWarehouseId", t => t.ToWarehouseId)
        .Guid("requestedBy", t => t.RequestedBy)
        .Sort("code", t => t.Code)
        .Sort("fromWarehouse", t => GridJson.Text(t.FromWarehouse.NameI18n, "tr"))
        .Sort("toWarehouse", t => GridJson.Text(t.ToWarehouse.NameI18n, "tr"))
        .Sort("transferType", t => t.TransferType)
        .Sort("status", t => t.Status)
        .Sort("itemCount", t => t.Items.Count)
        .Sort("requestedAt", t => t.RequestedAt)
        .Sort("createdAt", t => t.CreatedAt)
        .DefaultSort(t => t.CreatedAt, desc: true)
        .TieBreaker(t => t.Id);

    public static IQueryable<TransferRequest> ApplyNamed(IQueryable<TransferRequest> query, TransferListFilters f)
    {
        if (f.FromWarehouseId.HasValue) query = query.Where(t => t.FromWarehouseId == f.FromWarehouseId.Value);
        if (f.ToWarehouseId.HasValue) query = query.Where(t => t.ToWarehouseId == f.ToWarehouseId.Value);
        if (!string.IsNullOrWhiteSpace(f.Status)) query = query.Where(t => t.Status == f.Status);
        if (!string.IsNullOrWhiteSpace(f.TransferType)) query = query.Where(t => t.TransferType == f.TransferType);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToLower();
            query = query.Where(t => t.Code.ToLower().Contains(term)
                || (t.Notes != null && t.Notes.ToLower().Contains(term)));
        }
        return query;
    }

    public static IQueryable<TransferRequest> ApplyAll(IQueryable<TransferRequest> query, TransferListFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);

    public static string StatusLabel(string s) => s switch
    {
        "draft" => "Taslak", "pending" => "Bekliyor", "picking" => "Toplanıyor", "picked" => "Toplandı",
        "in_transit" => "Yolda", "delivered" => "Teslim Edildi", "completed" => "Tamamlandı",
        "cancelled" => "İptal", _ => s,
    };
}

public record TransferListFilters(
    Guid? FromWarehouseId = null, Guid? ToWarehouseId = null,
    string? Status = null, string? TransferType = null, string? Search = null);

public record TransferExportRow(
    string Code, string FromWarehouse, string ToWarehouse, string TransferType, string Status,
    int ItemCount, string? Notes, DateTime RequestedAt, DateTime CreatedAt);

public record ExportTransfersQuery(TransferListFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<TransferExportRow>>>;

public class ExportTransfersQueryHandler(IInventoryDbContext db)
    : IRequestHandler<ExportTransfersQuery, Result<GridExportSource<TransferExportRow>>>
{
    public async Task<Result<GridExportSource<TransferExportRow>>> Handle(ExportTransfersQuery r, CancellationToken ct)
    {
        var q = TransferGrid.ApplyAll(db.TransferRequests.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<TransferExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");

        var rows = TransferGrid.Schema.ApplySort(q, r.Grid).Select(t => new TransferExportRow(
            t.Code, GridJson.Text(t.FromWarehouse.NameI18n, "tr"), GridJson.Text(t.ToWarehouse.NameI18n, "tr"),
            t.TransferType, t.Status, t.Items.Count, t.Notes, t.RequestedAt, t.CreatedAt));
        return Result.Success(new GridExportSource<TransferExportRow>(count, rows));
    }
}
