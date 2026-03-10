using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Events;

public class OrderCancelledEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid OrderId { get; }
    public Guid CancelledBy { get; }

    public OrderCancelledEvent(Guid orderId, Guid cancelledBy)
    {
        OrderId = orderId;
        CancelledBy = cancelledBy;
    }
}
