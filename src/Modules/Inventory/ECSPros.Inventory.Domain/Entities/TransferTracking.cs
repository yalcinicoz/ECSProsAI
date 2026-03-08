namespace ECSPros.Inventory.Domain.Entities;

public class TransferTracking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransferRequestId { get; set; }
    public Guid? TransferItemId { get; set; }
    public string Action { get; set; } = string.Empty; // created, approved, picking_started, item_picked, handed_to_carrier, received, item_placed, completed, cancelled
    public Guid? FromUserId { get; set; }
    public Guid? ToUserId { get; set; }
    public int? Quantity { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;

    public TransferRequest TransferRequest { get; set; } = null!;
}
