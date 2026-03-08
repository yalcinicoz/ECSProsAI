using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Entities;

public class PackingStation : BaseEntity
{
    public Guid WarehouseId { get; set; }
    public string StationCode { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string? StationName { get; set; }
    public int SlotCount { get; set; } = 20;
    public bool IsObm { get; set; }
    public Guid? AssignedTo { get; set; }
    public string Status { get; set; } = string.Empty;
}
