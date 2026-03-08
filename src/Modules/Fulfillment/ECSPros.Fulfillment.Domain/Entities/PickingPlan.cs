using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Entities;

public class PickingPlan : BaseEntity
{
    public string PlanNumber { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string PlanType { get; set; } = string.Empty; // "single_item", "bulk", "dropshipping", "manual"
    public string Status { get; set; } = string.Empty;
    public Guid PlannedBy { get; set; }
    public DateTime PlannedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ICollection<SortingBin> Bins { get; set; } = new List<SortingBin>();
}
