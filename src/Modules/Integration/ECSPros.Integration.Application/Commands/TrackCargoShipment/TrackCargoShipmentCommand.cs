using ECSPros.Integration.Application.Adapters;
using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Integration.Application.Commands.TrackCargoShipment;

public record TrackCargoShipmentCommand(
    Guid FirmIntegrationId,
    string ServiceCode,
    string TrackingNumber) : IRequest<Result<CargoTrackingResult>>;

public class TrackCargoShipmentCommandHandler(
    IIntegrationDbContext db,
    IAdapterResolver adapterResolver)
    : IRequestHandler<TrackCargoShipmentCommand, Result<CargoTrackingResult>>
{
    public async Task<Result<CargoTrackingResult>> Handle(TrackCargoShipmentCommand request, CancellationToken ct)
    {
        var adapter = adapterResolver.GetCargoAdapter(request.ServiceCode);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await adapter.TrackShipmentAsync(request.FirmIntegrationId, request.TrackingNumber, ct);
        sw.Stop();

        db.IntegrationLogs.Add(new IntegrationLog
        {
            FirmIntegrationId = request.FirmIntegrationId,
            ServiceType = "cargo",
            OperationType = "track_shipment",
            Status = result.Success ? "success" : "failure",
            ErrorMessage = result.ErrorMessage,
            DurationMs = (int)sw.ElapsedMilliseconds
        });
        await db.SaveChangesAsync(ct);

        return result.Success
            ? Result.Success(result)
            : Result.Failure<CargoTrackingResult>(result.ErrorMessage ?? "Kargo takip sorgulanamadı.");
    }
}
