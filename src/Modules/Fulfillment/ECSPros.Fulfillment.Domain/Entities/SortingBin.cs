using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Entities;

public class SortingBin : BaseEntity
{
    public Guid PickingPlanId { get; set; }
    public int BinNumber { get; set; }
    public string Status { get; set; } = string.Empty;

    public PickingPlan PickingPlan { get; set; } = null!;
}
