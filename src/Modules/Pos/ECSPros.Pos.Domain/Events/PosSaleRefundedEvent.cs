using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Pos.Domain.Events;

public class PosSaleRefundedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid SaleId { get; }
    public Guid WarehouseId { get; }
    public Guid RefundedBy { get; }
    public IReadOnlyList<SoldItem> Items { get; }

    public PosSaleRefundedEvent(Guid saleId, Guid warehouseId, Guid refundedBy, IReadOnlyList<SoldItem> items)
    {
        SaleId = saleId;
        WarehouseId = warehouseId;
        RefundedBy = refundedBy;
        Items = items;
    }
}
