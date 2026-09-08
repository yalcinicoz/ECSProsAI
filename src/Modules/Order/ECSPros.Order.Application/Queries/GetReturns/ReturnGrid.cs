using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetReturns;

/// <summary>İadeler DataGrid şeması (F4): beyaz listeli sıralama/filtre + adlandırılmış filtreler + global arama; liste ve export aynı modeli kullanır.</summary>
public static class ReturnGrid
{
    public static readonly string[] Statuses = { "requested", "approved", "received", "refunded", "rejected" };

    public static readonly GridSchema<Return> Schema = new GridSchema<Return>()
        .Text("returnNumber", r => r.ReturnNumber)
        .Text("trackingNumber", r => r.ReturnTrackingNumber)
        .Text("cargoReturnCode", r => r.CargoReturnCode)
        .Enum("status", r => r.Status, Statuses)
        .Enum("returnType", r => r.ReturnType)
        .Enum("refundMethod", r => r.RefundMethod)
        .Enum("refundStatus", r => r.RefundStatus)
        .Date("createdAt", r => r.CreatedAt)
        .Date("cargoReceivedAt", r => r.ReturnCargoReceivedAt)
        .Number("refundAmount", r => r.RefundAmount)
        .Guid("orderId", r => r.OrderId)
        .Guid("memberId", r => r.MemberId)
        .Sort("returnNumber", r => r.ReturnNumber)
        .Sort("refundAmount", r => r.RefundAmount)
        .Sort("status", r => r.Status)
        .Sort("refundStatus", r => r.RefundStatus)
        .Sort("createdAt", r => r.CreatedAt)
        .DefaultSort(r => r.CreatedAt, desc: true)
        .TieBreaker(r => r.Id);

    public static IQueryable<Return> ApplyNamed(IQueryable<Return> query, ReturnListFilters f, bool includeStatus = true)
    {
        if (f.OrderId.HasValue) query = query.Where(r => r.OrderId == f.OrderId.Value);
        if (f.MemberId.HasValue) query = query.Where(r => r.MemberId == f.MemberId.Value);
        if (includeStatus && !string.IsNullOrEmpty(f.Status)) query = query.Where(r => r.Status == f.Status);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToLower();
            query = query.Where(r =>
                r.ReturnNumber.ToLower().Contains(term) ||
                (r.ReturnTrackingNumber != null && r.ReturnTrackingNumber.ToLower().Contains(term)) ||
                (r.CargoReturnCode != null && r.CargoReturnCode.ToLower().Contains(term)));
        }
        return query;
    }

    public static IQueryable<Return> ApplyAll(IQueryable<Return> query, ReturnListFilters f, GridRequest? grid, bool includeStatus = true)
    {
        query = ApplyNamed(query, f, includeStatus);
        return includeStatus ? Schema.ApplyFilters(query, grid) : Schema.ApplyFilters(query, grid, "status");
    }

    public static string StatusLabel(string s) => s switch
    {
        "requested" => "Talep Edildi", "approved" => "Onaylandı", "received" => "Teslim Alındı", "refunded" => "Geri Ödendi", "rejected" => "Reddedildi", _ => s,
    };
    public static string TypeLabel(string s) => s == "refund" ? "İade" : s;
}

public record ReturnListFilters(Guid? OrderId = null, Guid? MemberId = null, string? Status = null, string? Search = null);

public record ReturnExportRow(
    string ReturnNumber, string OrderNumber, DateTime CreatedAt, string ReturnType, string Status, string RefundMethod, string RefundStatus,
    decimal RefundAmount, string? ReturnTrackingNumber, string? CargoReturnCode, DateTime? ReturnCargoSentAt, DateTime? ReturnCargoReceivedAt,
    DateTime? InspectionCompletedAt, string? CustomerNotes, string? InspectionNotes);

public record ExportReturnsQuery(ReturnListFilters Filters, GridRequest Grid, int MaxRows) : IRequest<Result<GridExportSource<ReturnExportRow>>>;

public class ExportReturnsQueryHandler(IOrderDbContext db) : IRequestHandler<ExportReturnsQuery, Result<GridExportSource<ReturnExportRow>>>
{
    public async Task<Result<GridExportSource<ReturnExportRow>>> Handle(ExportReturnsQuery r, CancellationToken ct)
    {
        var q = ReturnGrid.ApplyAll(db.Returns.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<ReturnExportRow>>($"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var orders = db.Orders.AsNoTracking();
        var rows = ReturnGrid.Schema.ApplySort(q, r.Grid).Select(x => new ReturnExportRow(
            x.ReturnNumber, orders.Where(o => o.Id == x.OrderId).Select(o => o.OrderNumber).FirstOrDefault() ?? "", x.CreatedAt, x.ReturnType, x.Status,
            x.RefundMethod, x.RefundStatus, x.RefundAmount, x.ReturnTrackingNumber, x.CargoReturnCode, x.ReturnCargoSentAt, x.ReturnCargoReceivedAt,
            x.InspectionCompletedAt, x.CustomerNotes, x.InspectionNotes));
        return Result.Success(new GridExportSource<ReturnExportRow>(count, rows));
    }
}
