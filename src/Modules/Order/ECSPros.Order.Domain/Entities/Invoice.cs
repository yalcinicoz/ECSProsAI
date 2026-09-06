using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class Invoice : BaseEntity
{
    /// <summary>Geçici legacy MySQL importunda opinvoices.Id.</summary>
    public int? LegacyInvoiceId { get; set; }
    public Guid OrderId { get; set; }
    /// <summary>Faturanın bağlı olduğu paket — normal akış paket başına faturadır;
    /// null, tek-fatura istisna akışı ve eski kayıtlar içindir (F2, karar 2026-07-19).</summary>
    public Guid? PackageId { get; set; }
    /// <summary>Bizim serimizden üretilen faturada dolu; dış numaralı (ERP/pazaryeri/entegratör) faturada null (FE0 §2.4).</summary>
    public Guid? InvoiceSeriesId { get; set; }
    public string InvoiceType { get; set; } = string.Empty;
    /// <summary>internal | erp | marketplace | integrator — bkz. <see cref="InvoiceNumberSources"/>.</summary>
    public string NumberSource { get; set; } = InvoiceNumberSources.Internal;
    /// <summary>Kesim anındaki kanal gönderim yöntemi (anlık görüntü; bkz. <see cref="InvoiceSendMethods"/>).</summary>
    public string? SendMethod { get; set; }
    /// <summary>Kullanılan entegratör sözleşmesi (seri üzerinden çözülür; anlık görüntü).</summary>
    public Guid? IntegrationContractId { get; set; }
    /// <summary>GİB evrensel tekil tanımlayıcı (ETTN).</summary>
    public Guid? Ettn { get; set; }
    /// <summary>Dış kaynaktaki belge kimliği ve kaynağı (erp | marketplace:&lt;kanal&gt; | integrator:&lt;kod&gt;).</summary>
    public string? ExternalDocumentId { get; set; }
    public string? ExternalSource { get; set; }
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
    public InvoiceSeries? InvoiceSeries { get; set; }
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
}
