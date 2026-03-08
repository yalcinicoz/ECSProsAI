using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Finance.Domain.Entities;

public class SupplierReturnItem : BaseEntity
{
    public Guid ReturnId { get; set; }
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }

    public SupplierReturn Return { get; set; } = null!;
}
