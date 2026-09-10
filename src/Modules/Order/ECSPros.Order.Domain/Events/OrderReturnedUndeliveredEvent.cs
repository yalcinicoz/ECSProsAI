using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Events;

/// <summary>
/// İade planı §2.2 (2026-09-10): Teslimatsız İade — sipariş <c>returned</c> oldu.
/// <see cref="WasShipped"/>=false → sipariş hiç kargoya verilmedi, stok yalnız rezerveydi; Inventory
/// rezervasyonları serbest bırakır (iptalle aynı işlem). true → stok kargoyla tüketilmişti; depo girişi
/// iadenin "teslim al" adımında <see cref="ReturnReceivedEvent"/> ile olur.
/// </summary>
public class OrderReturnedUndeliveredEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid OrderId { get; }
    public Guid ReturnedBy { get; }
    public bool WasShipped { get; }
    public IReadOnlyList<OrderedItem> Items { get; }

    public OrderReturnedUndeliveredEvent(Guid orderId, Guid returnedBy, bool wasShipped, IReadOnlyList<OrderedItem> items)
    {
        OrderId = orderId;
        ReturnedBy = returnedBy;
        WasShipped = wasShipped;
        Items = items;
    }
}
