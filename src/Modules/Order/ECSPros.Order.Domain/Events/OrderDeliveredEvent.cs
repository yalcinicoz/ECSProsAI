using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Events;

/// <summary>P3a (2026-08-11): teslim — satıcı hakediş satırlarının üretim tetiği
/// (teslim ŞART kararı; uygunlaşma teslim + sözleşme X günü).</summary>
public class OrderDeliveredEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid OrderId { get; }
    public Guid DeliveredBy { get; }

    public OrderDeliveredEvent(Guid orderId, Guid deliveredBy)
    {
        OrderId = orderId;
        DeliveredBy = deliveredBy;
    }
}
