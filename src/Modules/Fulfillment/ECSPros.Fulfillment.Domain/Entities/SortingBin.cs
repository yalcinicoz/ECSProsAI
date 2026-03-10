using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Entities;

public class SortingBin : BaseEntity
{
    public Guid PickingPlanId { get; set; }
    public Guid? OrderId { get; set; }
    public int BinNumber { get; set; }
    public string Status { get; set; } = string.Empty; // "empty" | "filling" | "ready"

    public PickingPlan PickingPlan { get; set; } = null!;
}
