using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Events;

/// <summary>
/// OP4: masa ilerlemesi — Order modülü OrderItem.FinalSortQuantity/FinalScan* ve
/// Order.PackingStationCode/PackingSlotNumber alanlarını senkron günceller.
/// FinalScanDelta stok DÜŞÜRMEZ (stok toplama okutmasında düştü — OP2).
/// </summary>
public class DeskLineProgressEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid OrderId { get; }
    public Guid OrderItemId { get; }
    public Guid ActorId { get; }
    public int FinalSortDelta { get; }
    public int FinalScanDelta { get; }
    public string? StationCode { get; }
    public int? SlotNumber { get; }

    public DeskLineProgressEvent(Guid orderId, Guid orderItemId, Guid actorId,
        int finalSortDelta, int finalScanDelta, string? stationCode, int? slotNumber)
    {
        OrderId = orderId;
        OrderItemId = orderItemId;
        ActorId = actorId;
        FinalSortDelta = finalSortDelta;
        FinalScanDelta = finalScanDelta;
        StationCode = stationCode;
        SlotNumber = slotNumber;
    }
}
