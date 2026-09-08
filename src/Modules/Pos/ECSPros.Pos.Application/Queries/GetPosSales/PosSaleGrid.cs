using ECSPros.Pos.Application.Services;
using ECSPros.Pos.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Pos.Application.Queries.GetPosSales;

/// <summary>
/// POS satışları DataGrid şeması (tüm sütunlarda filtre, 2026-09-08): beyaz listeli filtre/sıralama + adlandırılmış filtreler
/// (sessionId, registerId, dateFrom, dateTo, status — eski istemciler korunur) + global arama (fiş no). Kasa adı Session.Register üzerinden.
/// </summary>
public static class PosSaleGrid
{
    public static readonly string[] Statuses = { "open", "completed", "cancelled", "refunded" };

    public static readonly GridSchema<PosSale> Schema = new GridSchema<PosSale>()
        .Text("saleNumber", s => s.SaleNumber)
        .Text("register", s => s.Session.Register.Name)
        .Text("notes", s => s.Notes)
        .Enum("status", s => s.Status, Statuses)
        .Number("total", s => s.GrandTotal)
        .Number("subtotal", s => s.Subtotal)
        .Number("totalDiscount", s => s.TotalDiscount)
        .Number("totalTax", s => s.TotalTax)
        .Number("itemCount", s => s.Items.Count())
        .Bool("hasMember", s => s.MemberId != null)
        .Date("createdAt", s => s.CreatedAt)
        .Date("printedAt", s => s.PrintedAt)
        .Guid("registerId", s => s.RegisterId)
        .Guid("sessionId", s => s.SessionId)
        .Guid("memberId", s => s.MemberId)
        .Guid("warehouseId", s => s.WarehouseId)
        .Sort("saleNumber", s => s.SaleNumber)
        .Sort("register", s => s.Session.Register.Name)
        .Sort("total", s => s.GrandTotal)
        .Sort("status", s => s.Status)
        .Sort("createdAt", s => s.CreatedAt)
        .DefaultSort(s => s.CreatedAt, desc: true)
        .TieBreaker(s => s.Id);

    public static IQueryable<PosSale> ApplyNamed(IQueryable<PosSale> query, PosSaleListFilters f)
    {
        if (f.SessionId.HasValue) query = query.Where(s => s.SessionId == f.SessionId.Value);
        if (f.RegisterId.HasValue) query = query.Where(s => s.RegisterId == f.RegisterId.Value);
        if (f.DateFrom.HasValue) query = query.Where(s => s.CreatedAt >= f.DateFrom.Value);
        if (f.DateTo.HasValue) query = query.Where(s => s.CreatedAt <= f.DateTo.Value);
        if (!string.IsNullOrEmpty(f.Status)) query = query.Where(s => s.Status == f.Status);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToLower();
            query = query.Where(s => s.SaleNumber.ToLower().Contains(term));
        }
        return query;
    }

    public static IQueryable<PosSale> ApplyAll(IQueryable<PosSale> query, PosSaleListFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);

    public static string StatusLabel(string s) => s switch
    { "open" => "Açık", "completed" => "Tamamlandı", "cancelled" => "İptal", "refunded" => "İade Edildi", _ => s };
}

public record PosSaleListFilters(Guid? SessionId = null, Guid? RegisterId = null, DateTime? DateFrom = null, DateTime? DateTo = null, string? Status = null, string? Search = null);

public record PosSaleExportRow(
    string SaleNumber, string Status, string RegisterName, string SessionNumber, decimal Subtotal, decimal TotalDiscount, decimal TotalTax, decimal GrandTotal,
    int ItemCount, DateTime CreatedAt, DateTime? PrintedAt, int ReprintCount, Guid? MemberId, string? Notes);

public record ExportPosSalesQuery(PosSaleListFilters Filters, GridRequest Grid, int MaxRows) : IRequest<Result<GridExportSource<PosSaleExportRow>>>;

public class ExportPosSalesQueryHandler(IPosDbContext db) : IRequestHandler<ExportPosSalesQuery, Result<GridExportSource<PosSaleExportRow>>>
{
    public async Task<Result<GridExportSource<PosSaleExportRow>>> Handle(ExportPosSalesQuery r, CancellationToken ct)
    {
        var q = PosSaleGrid.ApplyAll(db.PosSales.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<PosSaleExportRow>>($"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = PosSaleGrid.Schema.ApplySort(q, r.Grid).Select(s => new PosSaleExportRow(
            s.SaleNumber, s.Status, s.Session.Register.Name, s.Session.SessionNumber, s.Subtotal, s.TotalDiscount, s.TotalTax, s.GrandTotal,
            s.Items.Count(), s.CreatedAt, s.PrintedAt, s.ReprintCount, s.MemberId, s.Notes));
        return Result.Success(new GridExportSource<PosSaleExportRow>(count, rows));
    }
}
