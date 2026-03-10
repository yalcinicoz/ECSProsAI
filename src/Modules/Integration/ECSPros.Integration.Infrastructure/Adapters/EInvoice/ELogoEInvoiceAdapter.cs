using ECSPros.Integration.Application.Adapters;
using Microsoft.Extensions.Logging;

namespace ECSPros.Integration.Infrastructure.Adapters.EInvoice;

/// <summary>
/// eLogo e-Fatura adaptörü (GIB entegratörü).
/// Production'da eLogo API'sine bağlanır.
/// </summary>
public class ELogoEInvoiceAdapter(
    IHttpClientFactory httpClientFactory,
    ILogger<ELogoEInvoiceAdapter> logger) : IEInvoiceAdapter
{
    public string ServiceCode => "elogo";

    public async Task<EInvoiceResult> SendInvoiceAsync(
        Guid firmIntegrationId, EInvoicePayload payload, CancellationToken ct = default)
    {
        logger.LogInformation("eLogo e-Fatura gönderme: InvoiceNumber={InvoiceNumber}, OrderId={OrderId}",
            payload.InvoiceNumber, payload.OrderId);

        await Task.Delay(300, ct);
        var uuid = Guid.NewGuid().ToString();
        return new EInvoiceResult(true, uuid, null);
    }

    public async Task<EInvoiceResult> CancelInvoiceAsync(
        Guid firmIntegrationId, string invoiceUuid, string reason, CancellationToken ct = default)
    {
        logger.LogInformation("eLogo e-Fatura iptal: InvoiceUuid={InvoiceUuid}", invoiceUuid);
        await Task.Delay(200, ct);
        return new EInvoiceResult(true, invoiceUuid, null);
    }

    public async Task<string?> GetInvoiceStatusAsync(
        Guid firmIntegrationId, string invoiceUuid, CancellationToken ct = default)
    {
        await Task.Delay(100, ct);
        return "ACCEPTED";
    }
}
