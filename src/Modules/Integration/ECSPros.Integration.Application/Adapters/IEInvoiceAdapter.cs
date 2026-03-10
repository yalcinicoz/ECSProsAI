namespace ECSPros.Integration.Application.Adapters;

public interface IEInvoiceAdapter
{
    string ServiceCode { get; }
    Task<EInvoiceResult> SendInvoiceAsync(Guid firmIntegrationId, EInvoicePayload payload, CancellationToken ct = default);
    Task<EInvoiceResult> CancelInvoiceAsync(Guid firmIntegrationId, string invoiceUuid, string reason, CancellationToken ct = default);
    Task<string?> GetInvoiceStatusAsync(Guid firmIntegrationId, string invoiceUuid, CancellationToken ct = default);
}

public record EInvoicePayload(
    Guid OrderId,
    string InvoiceNumber,
    string RecipientName,
    string RecipientTaxId,
    string RecipientAddress,
    decimal TotalAmount,
    decimal TaxAmount,
    string CurrencyCode,
    DateTime InvoiceDate,
    List<EInvoiceLineDto> Lines);

public record EInvoiceLineDto(
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    decimal LineTotal);

public record EInvoiceResult(bool Success, string? InvoiceUuid, string? ErrorMessage);
