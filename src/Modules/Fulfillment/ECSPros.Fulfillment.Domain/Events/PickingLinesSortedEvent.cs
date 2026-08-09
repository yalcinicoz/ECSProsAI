using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Events;

/// <summary>OP3: ara ayrıştırma okutması — Order modülü OrderItem.SortingBinQuantity ve
/// Order.SortingBinId (koli eşleme kaydı) alanlarını senkron günceller.</summary>
public class PickingLinesSortedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid PlanId { get; }
    public Guid ActorId { get; }
    public Guid OrderId { get; }
    public Guid OrderItemId { get; }
    public Guid SortingBinId { get; }
    public int Quantity { get; }

    public PickingLinesSortedEvent(Guid planId, Guid actorId, Guid orderId, Guid orderItemId, Guid sortingBinId, int quantity)
    {
        PlanId = planId;
        ActorId = actorId;
        OrderId = orderId;
        OrderItemId = orderItemId;
        SortingBinId = sortingBinId;
        Quantity = quantity;
    }
}
