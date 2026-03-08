using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class Invoice : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid InvoiceSeriesId { get; set; }
    public string InvoiceType { get; set; } = string.Empty;
    public string InvoiceSerial { get; set; } = string.Empty;
    public string InvoiceYear { get; set; } = string.Empty;
    public int InvoiceSequence { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }

    // Recipient
    public string RecipientName { get; set; } = string.Empty;
    public string? RecipientTaxOffice { get; set; }
    public string? RecipientTaxNumber { get; set; }
    public string? RecipientCompanyName { get; set; }
    public string RecipientAddress { get; set; } = string.Empty;

    // Amounts
    public decimal Subtotal { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalTax { get; set; }
    public decimal GrandTotal { get; set; }

    // Integrator
    public string IntegratorStatus { get; set; } = string.Empty;
    public DateTime? IntegratorSentAt { get; set; }
    public Dictionary<string, object>? IntegratorResponse { get; set; }
    public string? IntegratorInvoiceUrl { get; set; }

    // ERP
    public string ErpStatus { get; set; } = string.Empty;
    public DateTime? ErpSentAt { get; set; }
    public string? ErpReference { get; set; }

    // Status
    public string Status { get; set; } = string.Empty;
    public Guid? CancelledByInvoiceId { get; set; }
    public Guid? CancelsInvoiceId { get; set; }

    public Order Order { get; set; } = null!;
    public InvoiceSeries InvoiceSeries { get; set; } = null!;
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
}
