using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class ShipmentItem : BaseEntity
{
    public Guid ShipmentId { get; set; }
    public Guid OrderItemId { get; set; }
    public int Quantity { get; set; }

    public Shipment Shipment { get; set; } = null!;
}
