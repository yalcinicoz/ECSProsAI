using ECSPros.Integration.Application.Adapters;
using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Integration.Application.Commands.CreateCargoShipment;

public record CreateCargoShipmentCommand(
    Guid FirmIntegrationId,
    string ServiceCode,
    CargoShipmentRequest ShipmentRequest) : IRequest<Result<CargoShipmentResult>>;

public class CreateCargoShipmentCommandHandler(
    IIntegrationDbContext db,
    IAdapterResolver adapterResolver)
    : IRequestHandler<CreateCargoShipmentCommand, Result<CargoShipmentResult>>
{
    public async Task<Result<CargoShipmentResult>> Handle(CreateCargoShipmentCommand request, CancellationToken ct)
    {
        var adapter = adapterResolver.GetCargoAdapter(request.ServiceCode);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await adapter.CreateShipmentAsync(request.FirmIntegrationId, request.ShipmentRequest, ct);
        sw.Stop();

        db.IntegrationLogs.Add(new IntegrationLog
        {
            FirmIntegrationId = request.FirmIntegrationId,
            ServiceType = "cargo",
            OperationType = "create_shipment",
            Status = result.Success ? "success" : "failure",
            ErrorMessage = result.ErrorMessage,
            DurationMs = (int)sw.ElapsedMilliseconds,
            ReferenceId = request.ShipmentRequest.OrderId,
            ReferenceType = "Order"
        });
        await db.SaveChangesAsync(ct);

        return result.Success
            ? Result.Success(result)
            : Result.Failure<CargoShipmentResult>(result.ErrorMessage ?? "Kargo gönderisi oluşturulamadı.");
    }
}
