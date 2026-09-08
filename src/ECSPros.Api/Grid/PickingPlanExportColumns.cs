using ECSPros.Fulfillment.Application.Queries.GetPickingPlans;

namespace ECSPros.Api.Grid;

/// <summary>Toplama planları Excel kolonları (plan no kilitli).</summary>
public static class PickingPlanExportColumns
{
    private static object? Tarih(DateTime? d) => d.HasValue ? GridExportWriter.ToIstanbul(d.Value) : null;

    public static readonly IReadOnlyList<GridExportColumn<PickingPlanExportRow>> All = new GridExportColumn<PickingPlanExportRow>[]
    {
        new("planNumber", "Plan No", r => r.PlanNumber, Locked: true),
        new("planType", "Tip", r => PickingPlanGrid.PlanTypeLabel(r.PlanType)),
        new("status", "Durum", r => PickingPlanGrid.StatusLabel(r.Status)),
        new("orderCount", "Sipariş Sayısı", r => r.OrderCount),
        new("totalLines", "Satır", r => r.TotalLines),
        new("assignedLines", "Dağıtılan Satır", r => r.AssignedLines),
        new("pickedLines", "Toplanan Satır", r => r.PickedLines),
        new("progress", "Toplanma %", r => r.TotalLines == 0 ? 0 : r.PickedLines * 100 / r.TotalLines),
        new("plannedAt", "Planlama", r => GridExportWriter.ToIstanbul(r.PlannedAt)),
        new("startedAt", "Başlangıç", r => Tarih(r.StartedAt)),
        new("completedAt", "Tamamlanma", r => Tarih(r.CompletedAt)),
        new("warehouseId", "Depo Id", r => r.WarehouseId.ToString()),
        new("plannedBy", "Planlayan Id", r => r.PlannedBy.ToString()),
    };
}
