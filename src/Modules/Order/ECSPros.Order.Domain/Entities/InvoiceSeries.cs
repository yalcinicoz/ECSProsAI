using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

/// <summary>
/// Tekil ve TİPLİ fatura serisi (FE0, 2026-09-06 — docs/fatura-entegrasyon-plani.md §2.2).
/// Bir seri = bir üç harfli ön ek = bir numara akışı; tipi (e-arşiv / e-fatura / ihracat) tanımda
/// seçilir ve sayaç ilerledikten sonra değiştirilemez. Aynı harfler firma içinde tipten bağımsız
/// yalnız bir kez tanımlanabilir (bir e-arşiv serisinin başka yerde e-fatura için kullanılması
/// karışıklığının yapısal önlemi). Her seri bir entegratör sözleşmesine (core_firm_platform_integrations,
/// ServiceType=einvoice) bağlanır; sözleşme kataloğu FE3'te geldiğinde zorunlu hale gelir.
/// </summary>
public class InvoiceSeries : BaseEntity
{
    public Guid FirmId { get; set; }
    /// <summary>3 büyük harf (GİB seri ön eki).</summary>
    public string Serial { get; set; } = string.Empty;
    /// <summary>e_archive | e_invoice | export — bkz. <see cref="InvoiceTypes"/>.</summary>
    public string InvoiceType { get; set; } = InvoiceTypes.EArchive;
    public string? Name { get; set; }
    public string? Description { get; set; }
    /// <summary>Firma bazlı entegratör sözleşmesi (core_firm_platform_integrations.Id, gevşek referans).</summary>
    public Guid? IntegrationContractId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? RetiredAt { get; set; }

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<InvoiceSeriesCounter> Counters { get; set; } = new List<InvoiceSeriesCounter>();
    public ICollection<ChannelInvoiceSeriesBinding> ChannelBindings { get; set; } = new List<ChannelInvoiceSeriesBinding>();
}
