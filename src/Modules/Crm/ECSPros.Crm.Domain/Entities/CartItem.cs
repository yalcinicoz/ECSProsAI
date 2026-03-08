using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class CartItem : BaseEntity
{
    public Guid CartId { get; set; }
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal AddedPrice { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public bool IsAvailable { get; set; } = true;
    public int AvailableQuantity { get; set; } = 0;
    public DateTime LastCheckedAt { get; set; } = DateTime.UtcNow;

    public Cart Cart { get; set; } = null!;
}
