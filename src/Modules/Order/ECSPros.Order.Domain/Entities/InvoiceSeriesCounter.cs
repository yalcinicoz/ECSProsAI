using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

/// <summary>
/// Seri × yıl sayacı (FE0 §2.5). Numara üretimi bu satırı tek atomik INSERT…ON CONFLICT…RETURNING ile
/// ilerletir (IInvoiceNumberService); eşzamanlı istekler sıralanır, boşluk oluşmaz. Yıl değişince
/// yeni satır 1'den başlar. LastInvoiceDate tarih-sıra kuralının (FE1, K3) dayanağıdır.
/// </summary>
public class InvoiceSeriesCounter : BaseEntity
{
    public Guid InvoiceSeriesId { get; set; }
    public string Year { get; set; } = string.Empty;
    public int LastSequence { get; set; }
    public DateTime? LastInvoiceDate { get; set; }

    public InvoiceSeries InvoiceSeries { get; set; } = null!;
}
