using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Inventory.Domain.Entities;

public class Stock : BaseEntity
{
    public Guid VariantId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    // Üçlü depo yapısı (2026-07-14): stok artık varyant+raf (BinId) başına tutulur; SectionId/
    // WarehouseId denormalize. LocationId eski yapıdan kalan (cutover'da emekliye ayrılacak).
    public Guid? SectionId { get; set; }
    public Guid? BinId { get; set; }
    public string StockType { get; set; } = "physical"; // physical, virtual
    public int Quantity { get; set; } = 0;
    public int ReservedQuantity { get; set; } = 0;
    public int AvailableQuantity => Quantity - ReservedQuantity;

    public Warehouse Warehouse { get; set; } = null!;
    public WarehouseLocation? Location { get; set; }
    public ICollection<StockReservation> Reservations { get; set; } = new List<StockReservation>();
}
