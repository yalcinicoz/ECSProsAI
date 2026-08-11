using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.SetPackageShipment;

/// <summary>
/// Satıcı API P2 (2026-08-11): satıcı kargo bildiriminde paketin Shipment bağını ve dış
/// takip kodunu yazar — kod motoru sözlüğüne uygun: dışarıdan gelen kod 'external' kaynaklı
/// aynen saklanır (kod havuza dönmez).
/// </summary>
public record SetPackageShipmentCommand(
    Guid PackageId,
    Guid ShipmentId,
    string TrackingNumber) : IRequest<Result<bool>>;

public class SetPackageShipmentCommandHandler(IFulfillmentDbContext context)
    : IRequestHandler<SetPackageShipmentCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(SetPackageShipmentCommand request, CancellationToken ct)
    {
        var package = await context.Packages.FirstOrDefaultAsync(p => p.Id == request.PackageId, ct);
        if (package is null) return Result.Failure<bool>("Paket bulunamadı.");

        package.ShipmentId = request.ShipmentId;
        package.CargoIntegrationCode = request.TrackingNumber;
        package.CargoIntegrationCodeSource = "external";
        package.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
