using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class OrderExpense : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid? OrderItemId { get; set; }
    public Guid ExpenseTypeId { get; set; }
    public string ExpenseName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal TaxAmount { get; set; }

    public Order Order { get; set; } = null!;
}
