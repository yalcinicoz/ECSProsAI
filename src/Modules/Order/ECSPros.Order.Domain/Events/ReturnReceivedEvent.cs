using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Events;

public record ReturnedItem(Guid VariantId, int Quantity);

public class ReturnReceivedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid ReturnId { get; }
    public Guid OrderId { get; }
    public Guid WarehouseId { get; }
    public Guid ReceivedBy { get; }
    public IReadOnlyList<ReturnedItem> Items { get; }

    public ReturnReceivedEvent(Guid returnId, Guid orderId, Guid warehouseId, Guid receivedBy, IReadOnlyList<ReturnedItem> items)
    {
        ReturnId = returnId;
        OrderId = orderId;
        WarehouseId = warehouseId;
        ReceivedBy = receivedBy;
        Items = items;
    }
}
