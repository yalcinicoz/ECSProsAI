using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Finance.Domain.Entities;

public class SupplierInvoice : BaseEntity
{
    public Guid SupplierId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateOnly InvoiceDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalTax { get; set; }
    public decimal GrandTotal { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public ICollection<SupplierInvoiceItem> Items { get; set; } = new List<SupplierInvoiceItem>();
    public ICollection<SupplierDelivery> Deliveries { get; set; } = new List<SupplierDelivery>();
}
