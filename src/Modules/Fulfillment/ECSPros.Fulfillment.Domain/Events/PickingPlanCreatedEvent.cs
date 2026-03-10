using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Events;

public record AssignedOrder(Guid OrderId, int BinNumber);

public class PickingPlanCreatedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid PlanId { get; }
    public Guid WarehouseId { get; }
    public Guid CreatedBy { get; }
    public IReadOnlyList<AssignedOrder> Orders { get; }

    public PickingPlanCreatedEvent(Guid planId, Guid warehouseId, Guid createdBy, IReadOnlyList<AssignedOrder> orders)
    {
        PlanId = planId;
        WarehouseId = warehouseId;
        CreatedBy = createdBy;
        Orders = orders;
    }
}
