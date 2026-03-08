using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Inventory.Domain.Entities;

public class Warehouse : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public string WarehouseType { get; set; } = "main"; // main, secondary, store, store_warehouse, virtual, receiving, studio, tailor, defective, other
    public string? Address { get; set; }
    public bool IsSellableOnline { get; set; } = true;
    public int ReservePriority { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public ICollection<WarehouseLocation> Locations { get; set; } = new List<WarehouseLocation>();
    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
}
