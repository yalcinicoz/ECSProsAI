using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Pos.Domain.Events;

public record SoldItem(Guid VariantId, decimal Quantity);

public class PosSaleCompletedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid SaleId { get; }
    public Guid WarehouseId { get; }
    public Guid CompletedBy { get; }
    public IReadOnlyList<SoldItem> Items { get; }

    public PosSaleCompletedEvent(Guid saleId, Guid warehouseId, Guid completedBy, IReadOnlyList<SoldItem> items)
    {
        SaleId = saleId;
        WarehouseId = warehouseId;
        CompletedBy = completedBy;
        Items = items;
    }
}
