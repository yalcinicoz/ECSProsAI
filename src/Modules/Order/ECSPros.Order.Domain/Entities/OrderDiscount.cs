using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class OrderDiscount : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid? OrderItemId { get; set; }
    public string DiscountType { get; set; } = string.Empty;
    public Guid? DiscountSourceId { get; set; }
    public string DiscountName { get; set; } = string.Empty;
    public decimal DiscountAmount { get; set; }

    public Order Order { get; set; } = null!;
}
