using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Events;

/// <summary>Toplanan kalem — FinalScanned: tek ürünlü hızlı hatta toplama + son kontrol
/// tek adımdır (K: tek ürünlüde ayrıştırma yok). PickedBinId fiilen toplanan raf (K-15).</summary>
public record PickedLineItem(
    Guid OrderId, Guid OrderItemId, Guid VariantId, int Quantity,
    Guid? PickedBinId, bool FinalScanned);

/// <summary>
/// OP2: toplama okutması gerçekleşti — Order modülü OrderItem.PickedBy/At (+FinalScan*)
/// alanlarını, Inventory modülü rezervasyon/stok düşümünü (fiili raftan, K-14 kurallı,
/// StockMovement izli) senkron işler.
/// </summary>
public class PickingLinePickedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid PlanId { get; }
    public Guid ActorId { get; }
    public IReadOnlyList<PickedLineItem> Items { get; }

    public PickingLinePickedEvent(Guid planId, Guid actorId, IReadOnlyList<PickedLineItem> items)
    {
        PlanId = planId;
        ActorId = actorId;
        Items = items;
    }
}
