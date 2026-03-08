using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class OrderTax : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid? OrderItemId { get; set; }
    public Guid? OrderExpenseId { get; set; }
    public string TaxType { get; set; } = string.Empty;
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }

    public Order Order { get; set; } = null!;
}
