using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.QueueCargoNotify;

/// <summary>OP5: paket kapanışında Package.ShipmentId bağlanır ve kargo bildirimi
/// outbox'a düşer (worker gönderir). Taşıyıcı yoksa kayıt 'failed' açılır — yönlendirme
/// ekranında "taşıyıcı atanmamış" olarak görünür.</summary>
public record QueueCargoNotifyCommand(
    Guid PackageId,
    Guid OrderId,
    Guid FirmPlatformId,
    Guid? ShipmentId,
    Guid? CargoIntegrationId,
    string? CargoName,
    Guid ActorId) : IRequest<Result<bool>>;

public class QueueCargoNotifyCommandHandler(IFulfillmentDbContext db)
    : IRequestHandler<QueueCargoNotifyCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(QueueCargoNotifyCommand request, CancellationToken ct)
    {
        var paket = await db.Packages.FirstOrDefaultAsync(p => p.Id == request.PackageId, ct);
        if (paket is null) return Result.Failure<bool>("Paket bulunamadı.");
        if (request.ShipmentId is { } sid && paket.ShipmentId is null)
            paket.ShipmentId = sid;

        var varMi = await db.CargoNotifyOutbox.AnyAsync(o => o.PackageId == request.PackageId, ct);
        if (!varMi)
        {
            db.CargoNotifyOutbox.Add(new CargoNotifyOutbox
            {
                PackageId = request.PackageId,
                OrderId = request.OrderId,
                FirmPlatformId = request.FirmPlatformId,
                ShipmentId = request.ShipmentId,
                CargoIntegrationId = request.CargoIntegrationId,
                CargoName = request.CargoName,
                Status = request.CargoIntegrationId is null ? "failed" : "pending",
                LastError = request.CargoIntegrationId is null ? "Taşıyıcı atanmamış" : null,
                NextAttemptAt = DateTime.UtcNow,
                CreatedBy = request.ActorId
            });
            db.OperationLogs.Add(new OperationLog
            {
                OrderId = request.OrderId, PackageId = request.PackageId, Action = "cargo_queued",
                ActorId = request.ActorId, CreatedBy = request.ActorId,
                Detail = new Dictionary<string, object> { ["cargo"] = request.CargoName ?? "atanmamış" }
            });
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
