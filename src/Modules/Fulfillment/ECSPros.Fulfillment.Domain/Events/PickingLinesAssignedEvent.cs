using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Events;

public record AssignedLine(Guid OrderId, Guid OrderItemId);

/// <summary>
/// OP1: toplama satırları personele dağıtıldığında — Order modülü OrderItem.PickAssignedTo/At
/// alanlarını senkron günceller (modüller arası mevcut event kalıbı).
/// </summary>
public class PickingLinesAssignedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid PlanId { get; }
    public Guid AssignedTo { get; }
    public IReadOnlyList<AssignedLine> Lines { get; }

    public PickingLinesAssignedEvent(Guid planId, Guid assignedTo, IReadOnlyList<AssignedLine> lines)
    {
        PlanId = planId;
        AssignedTo = assignedTo;
        Lines = lines;
    }
}
