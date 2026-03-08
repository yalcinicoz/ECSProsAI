using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Inventory.Domain.Entities;

public class StockReservation : BaseEntity
{
    public Guid StockId { get; set; }
    public Guid VariantId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public int Quantity { get; set; }
    public string ReferenceType { get; set; } = string.Empty; // order, transfer_request
    public Guid ReferenceId { get; set; }
    public string Status { get; set; } = "reserved"; // reserved, picked, released, cancelled

    public Stock Stock { get; set; } = null!;
}
