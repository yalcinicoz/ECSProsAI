using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Events;

public class PickingPlanCompletedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid PlanId { get; }
    public Guid CompletedBy { get; }
    public IReadOnlyList<Guid> OrderIds { get; }

    public PickingPlanCompletedEvent(Guid planId, Guid completedBy, IReadOnlyList<Guid> orderIds)
    {
        PlanId = planId;
        CompletedBy = completedBy;
        OrderIds = orderIds;
    }
}
