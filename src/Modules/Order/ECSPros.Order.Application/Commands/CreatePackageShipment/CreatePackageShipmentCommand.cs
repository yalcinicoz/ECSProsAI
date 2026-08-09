using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.CreatePackageShipment;

/// <summary>
/// OP5: paket kapanışında pakete bağlı Shipment kaydı — taşıyıcı siparişte kesinleşmiştir
/// (K-9, RequestedCargoIntegrationId). API'ye gönderim outbox+worker'ındır (ApiStatus=pending);
/// taşıyıcı atanmamışsa Shipment açılmaz (yönlendirme ekranından atanınca açılır).
/// </summary>
public record CreatePackageShipmentCommand(
    Guid OrderId,
    Guid PackageId,
    string PackageNumber,
    Guid CreatedBy) : IRequest<Result<PackageShipmentDto>>;

public record PackageShipmentDto(Guid? ShipmentId, Guid? CargoIntegrationId, string? CargoName);

public class CreatePackageShipmentCommandHandler(IOrderDbContext db)
    : IRequestHandler<CreatePackageShipmentCommand, Result<PackageShipmentDto>>
{
    public async Task<Result<PackageShipmentDto>> Handle(CreatePackageShipmentCommand request, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);
        if (order is null) return Result.Failure<PackageShipmentDto>("Sipariş bulunamadı.");

        // Aynı pakete ikinci shipment açılmaz (idempotent)
        var mevcut = await db.Shipments.AsNoTracking()
            .FirstOrDefaultAsync(s => s.PackageId == request.PackageId, ct);
        if (mevcut is not null)
            return Result.Success(new PackageShipmentDto(mevcut.Id, order.RequestedCargoIntegrationId, order.RequestedCargoName));

        if (order.RequestedCargoIntegrationId is not { } kargoId)
            return Result.Success(new PackageShipmentDto(null, null, null));

        var shipment = new Shipment
        {
            OrderId = request.OrderId,
            PackageId = request.PackageId,
            FirmIntegrationId = kargoId,
            ShipmentNumber = $"SHP-{request.PackageNumber}",
            Status = "created",
            ApiStatus = "pending",
            PackageCount = 1,
            CreatedBy = request.CreatedBy
        };
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync(ct);
        return Result.Success(new PackageShipmentDto(shipment.Id, kargoId, order.RequestedCargoName));
    }
}
