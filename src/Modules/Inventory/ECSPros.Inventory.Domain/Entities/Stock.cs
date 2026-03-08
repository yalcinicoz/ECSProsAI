using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Inventory.Domain.Entities;

public class Stock : BaseEntity
{
    public Guid VariantId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public string StockType { get; set; } = "physical"; // physical, virtual
    public int Quantity { get; set; } = 0;
    public int ReservedQuantity { get; set; } = 0;
    public int AvailableQuantity => Quantity - ReservedQuantity;

    public Warehouse Warehouse { get; set; } = null!;
    public WarehouseLocation? Location { get; set; }
    public ICollection<StockReservation> Reservations { get; set; } = new List<StockReservation>();
}
