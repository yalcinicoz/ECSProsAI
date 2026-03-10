using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Events;

public record OrderedItem(Guid VariantId, int Quantity);

public class OrderConfirmedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid OrderId { get; }
    public Guid WarehouseId { get; }
    public Guid ConfirmedBy { get; }
    public IReadOnlyList<OrderedItem> Items { get; }

    public OrderConfirmedEvent(Guid orderId, Guid warehouseId, Guid confirmedBy, IReadOnlyList<OrderedItem> items)
    {
        OrderId = orderId;
        WarehouseId = warehouseId;
        ConfirmedBy = confirmedBy;
        Items = items;
    }
}
