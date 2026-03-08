using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class InvoiceSeries : BaseEntity
{
    public Guid FirmId { get; set; }
    public string? Name { get; set; }
    public string EArchiveSerial { get; set; } = string.Empty;
    public string EInvoiceSerial { get; set; } = string.Empty;
    public string ExportSerial { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
