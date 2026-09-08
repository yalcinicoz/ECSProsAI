using ECSPros.Fulfillment.Domain.Events;
using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Entities;

public class PickingPlan : AggregateRoot
{
    public string PlanNumber { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string PlanType { get; set; } = string.Empty; // "single_item", "bulk", "dropshipping", "manual"
    public string Status { get; set; } = string.Empty;   // "pending" | "picking" | "completed" | "cancelled"
    public Guid PlannedBy { get; set; }
    public DateTime PlannedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ICollection<SortingBin> Bins { get; set; } = new List<SortingBin>();
    /// <summary>DataGrid (2026-09-08): sipariş sayısı / dağıtım / ilerleme filtreleri için satır navigasyonu (yalnız sorgu; şema değişmez).</summary>
    public ICollection<PickingPlanLine> Lines { get; set; } = new List<PickingPlanLine>();

    public void Start(Guid startedBy)
    {
        if (Status != "pending")
            throw new InvalidOperationException($"'{Status}' durumundaki plan başlatılamaz.");

        Status = "picking";
        StartedAt = DateTime.UtcNow;
        UpdatedBy = startedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete(Guid completedBy)
    {
        if (Status != "picking")
            throw new InvalidOperationException($"'{Status}' durumundaki plan tamamlanamaz.");

        Status = "completed";
        CompletedAt = DateTime.UtcNow;
        UpdatedBy = completedBy;
        UpdatedAt = DateTime.UtcNow;

        var orderIds = Bins
            .Where(b => b.OrderId.HasValue)
            .Select(b => b.OrderId!.Value)
            .Distinct()
            .ToList();

        AddDomainEvent(new PickingPlanCompletedEvent(Id, completedBy, orderIds));
    }
}
