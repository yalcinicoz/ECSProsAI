using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Finance.Domain.Entities;

public class SupplierInvoiceItem : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public Guid? VariantId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountRate { get; set; } = 0;
    public decimal DiscountAmount { get; set; } = 0;
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }

    public SupplierInvoice Invoice { get; set; } = null!;
}
