using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Queries.GetPickingPlans;

/// <summary>
/// Toplama planları DataGrid şeması (tüm sütunlarda filtre, 2026-09-08): beyaz listeli filtre/sıralama + adlandırılmış
/// filtreler (status, warehouseId) + global arama (plan no). Sipariş sayısı / dağıtım / ilerleme satır (PickingPlan.Lines)
/// alt sorgularıyla SQL'de hesaplanır (COUNT DISTINCT, CASE); satırı olmayan planda ilerleme 0, dağıtım "none".
/// </summary>
public static class PickingPlanGrid
{
    public static readonly string[] Statuses = { "pending", "picking", "completed", "cancelled" };
    public static readonly string[] Assignments = { "none", "unassigned", "partial", "full" };

    public static readonly GridSchema<PickingPlan> Schema = new GridSchema<PickingPlan>()
        .Text("planNumber", p => p.PlanNumber)
        .Enum("planType", p => p.PlanType)
        .Enum("status", p => p.Status, Statuses)
        .Number("orderCount", p => p.Lines.Select(l => l.OrderId).Distinct().Count())
        .Number("totalLines", p => p.Lines.Count())
        .Number("pickedLines", p => p.Lines.Count(l => l.Status == "picked"))
        .Number("progress", p => p.Lines.Count() == 0 ? 0 : p.Lines.Count(l => l.Status == "picked") * 100 / p.Lines.Count())
        .Enum("assignment", p => p.Lines.Count() == 0 ? "none"
            : p.Lines.Count(l => l.AssignedTo != null) == 0 ? "unassigned"
            : p.Lines.Count(l => l.AssignedTo != null) < p.Lines.Count() ? "partial" : "full", Assignments)
        .Date("plannedAt", p => p.PlannedAt)
        .Date("startedAt", p => p.StartedAt)
        .Date("completedAt", p => p.CompletedAt)
        .Date("createdAt", p => p.CreatedAt)
        .Guid("warehouseId", p => p.WarehouseId)
        .Guid("plannedBy", p => p.PlannedBy)
        .Sort("planNumber", p => p.PlanNumber)
        .Sort("planType", p => p.PlanType)
        .Sort("status", p => p.Status)
        .Sort("orderCount", p => p.Lines.Select(l => l.OrderId).Distinct().Count())
        .Sort("progress", p => p.Lines.Count() == 0 ? 0 : p.Lines.Count(l => l.Status == "picked") * 100 / p.Lines.Count())
        .Sort("plannedAt", p => p.PlannedAt)
        .Sort("startedAt", p => p.StartedAt)
        .Sort("completedAt", p => p.CompletedAt)
        .Sort("createdAt", p => p.CreatedAt)
        .DefaultSort(p => p.PlannedAt, desc: true)
        .TieBreaker(p => p.Id);

    public static IQueryable<PickingPlan> ApplyNamed(IQueryable<PickingPlan> query, PickingPlanListFilters f)
    {
        if (!string.IsNullOrWhiteSpace(f.Status)) query = query.Where(p => p.Status == f.Status);
        if (f.WarehouseId.HasValue) query = query.Where(p => p.WarehouseId == f.WarehouseId.Value);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToLower();
            query = query.Where(p => p.PlanNumber.ToLower().Contains(term));
        }
        return query;
    }

    public static IQueryable<PickingPlan> ApplyAll(IQueryable<PickingPlan> query, PickingPlanListFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);

    public static string StatusLabel(string s) => s switch
    { "pending" => "Bekliyor", "picking" => "Toplanıyor", "completed" => "Tamamlandı", "cancelled" => "İptal", _ => s };

    public static string PlanTypeLabel(string s) => s switch
    { "single_item" => "Tek ürünlü", "bulk" => "Çok ürünlü", "single" => "Tekli", "batch" => "Toplu", "wave" => "Dalga", "dropshipping" => "Dropshipping", "manual" => "Manuel", _ => s };
}

public record PickingPlanListFilters(string? Status = null, Guid? WarehouseId = null, string? Search = null);

public record PickingPlanExportRow(
    string PlanNumber, string PlanType, string Status, int OrderCount, int TotalLines, int AssignedLines, int PickedLines,
    DateTime PlannedAt, DateTime? StartedAt, DateTime? CompletedAt, Guid WarehouseId, Guid PlannedBy);

public record ExportPickingPlansQuery(PickingPlanListFilters Filters, GridRequest Grid, int MaxRows) : IRequest<Result<GridExportSource<PickingPlanExportRow>>>;

public class ExportPickingPlansQueryHandler(IFulfillmentDbContext db) : IRequestHandler<ExportPickingPlansQuery, Result<GridExportSource<PickingPlanExportRow>>>
{
    public async Task<Result<GridExportSource<PickingPlanExportRow>>> Handle(ExportPickingPlansQuery r, CancellationToken ct)
    {
        var q = PickingPlanGrid.ApplyAll(db.PickingPlans.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<PickingPlanExportRow>>($"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = PickingPlanGrid.Schema.ApplySort(q, r.Grid).Select(p => new PickingPlanExportRow(
            p.PlanNumber, p.PlanType, p.Status,
            p.Lines.Select(l => l.OrderId).Distinct().Count(), p.Lines.Count(),
            p.Lines.Count(l => l.AssignedTo != null), p.Lines.Count(l => l.Status == "picked"),
            p.PlannedAt, p.StartedAt, p.CompletedAt, p.WarehouseId, p.PlannedBy));
        return Result.Success(new GridExportSource<PickingPlanExportRow>(count, rows));
    }
}
