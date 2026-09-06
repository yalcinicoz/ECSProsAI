namespace ECSPros.Order.Application.Services;

/// <summary>Seri × yıl sayacından atomik fatura numarası tahsisi (FE0 §2.5).</summary>
public interface IInvoiceNumberService
{
    /// <summary>Sayacı tek atomik adımda ilerletir; çağıran, açık transaction içinde fatura satırını yazıp
    /// commit eder — fatura yazılamazsa sayaç geri alınır, boşluk oluşmaz.</summary>
    Task<InvoiceNumberAllocation> AllocateAsync(Guid seriesId, string serial, DateTime invoiceDateUtc, CancellationToken ct = default);
}

public sealed record InvoiceNumberAllocation(string Serial, string Year, int Sequence, string Number);
