using ECSPros.Integration.Application.Adapters;
using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Integration.Application.Commands.SendEInvoice;

public record SendEInvoiceCommand(
    Guid FirmIntegrationId,
    string ServiceCode,
    EInvoicePayload Payload) : IRequest<Result<EInvoiceResult>>;

public class SendEInvoiceCommandHandler(
    IIntegrationDbContext db,
    IAdapterResolver adapterResolver)
    : IRequestHandler<SendEInvoiceCommand, Result<EInvoiceResult>>
{
    public async Task<Result<EInvoiceResult>> Handle(SendEInvoiceCommand request, CancellationToken ct)
    {
        var adapter = adapterResolver.GetEInvoiceAdapter(request.ServiceCode);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await adapter.SendInvoiceAsync(request.FirmIntegrationId, request.Payload, ct);
        sw.Stop();

        db.IntegrationLogs.Add(new IntegrationLog
        {
            FirmIntegrationId = request.FirmIntegrationId,
            ServiceType = "invoice_integrator",
            OperationType = "send_invoice",
            Status = result.Success ? "success" : "failure",
            ErrorMessage = result.ErrorMessage,
            DurationMs = (int)sw.ElapsedMilliseconds,
            ReferenceId = request.Payload.OrderId,
            ReferenceType = "Order"
        });
        await db.SaveChangesAsync(ct);

        return result.Success
            ? Result.Success(result)
            : Result.Failure<EInvoiceResult>(result.ErrorMessage ?? "e-Fatura gönderilemedi.");
    }
}
