using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Finance.Domain.Entities;

public class SupplierPayment : BaseEntity
{
    public Guid SupplierId { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public Dictionary<string, object> Details { get; set; } = new();
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
}
