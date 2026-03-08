using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Inventory.Domain.Entities;

public class WarehouseLocation : BaseEntity
{
    public Guid WarehouseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string? Name { get; set; }
    public Guid? ParentId { get; set; }
    public string LocationType { get; set; } = "bin"; // zone, aisle, rack, shelf, bin
    public int ReservePriority { get; set; } = 0;
    public int PickingOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public Warehouse Warehouse { get; set; } = null!;
    public WarehouseLocation? Parent { get; set; }
    public ICollection<WarehouseLocation> Children { get; set; } = new List<WarehouseLocation>();
}
