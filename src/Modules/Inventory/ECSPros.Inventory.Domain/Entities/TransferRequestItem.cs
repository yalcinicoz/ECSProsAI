using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Inventory.Domain.Entities;

public class TransferRequestItem : BaseEntity
{
    public Guid TransferRequestId { get; set; }
    public Guid VariantId { get; set; }
    public int RequestedQuantity { get; set; }
    public int PickedQuantity { get; set; } = 0;
    public int DeliveredQuantity { get; set; } = 0;
    public Guid? FromLocationId { get; set; }
    public Guid? ToLocationId { get; set; }
    public string Status { get; set; } = "pending"; // pending, picking, picked, in_transit, delivered, completed, cancelled

    public TransferRequest TransferRequest { get; set; } = null!;
}
