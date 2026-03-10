using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Events;

public class OrderShippedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid OrderId { get; }
    public Guid ShippedBy { get; }
    public IReadOnlyList<OrderedItem> Items { get; }

    public OrderShippedEvent(Guid orderId, Guid shippedBy, IReadOnlyList<OrderedItem> items)
    {
        OrderId = orderId;
        ShippedBy = shippedBy;
        Items = items;
    }
}
