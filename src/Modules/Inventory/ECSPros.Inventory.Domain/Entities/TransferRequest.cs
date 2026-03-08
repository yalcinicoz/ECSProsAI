using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Inventory.Domain.Entities;

public class TransferRequest : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Guid FromWarehouseId { get; set; }
    public Guid ToWarehouseId { get; set; }
    public string TransferType { get; set; } = string.Empty; // studio, tailor, inter_warehouse, defective, donation, supplier_return, other
    public string Status { get; set; } = "draft"; // draft, pending, picking, picked, in_transit, delivered, completed, cancelled
    public Guid RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    public Warehouse FromWarehouse { get; set; } = null!;
    public Warehouse ToWarehouse { get; set; } = null!;
    public ICollection<TransferRequestItem> Items { get; set; } = new List<TransferRequestItem>();
    public ICollection<TransferTracking> Trackings { get; set; } = new List<TransferTracking>();
}
