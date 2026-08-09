using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.UpdateOrderCargo;

/// <summary>OP5 (K-9): kargo yönlendirmesinde siparişlerin taşıyıcısı güncellenir
/// (+ paketin gönderilmemiş Shipment kaydı yeni taşıyıcıya çevrilir).</summary>
public record UpdateOrderCargoCommand(
    List<Guid> OrderIds,
    Guid CargoIntegrationId,
    string CargoName,
    Guid ActorId) : IRequest<Result<int>>;

public class UpdateOrderCargoCommandHandler(IOrderDbContext db)
    : IRequestHandler<UpdateOrderCargoCommand, Result<int>>
{
    public async Task<Result<int>> Handle(UpdateOrderCargoCommand request, CancellationToken ct)
    {
        var siparisler = await db.Orders
            .Where(o => request.OrderIds.Contains(o.Id))
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var o in siparisler)
        {
            o.RequestedCargoIntegrationId = request.CargoIntegrationId;
            o.RequestedCargoName = request.CargoName;
            o.UpdatedAt = now;
            o.UpdatedBy = request.ActorId;
        }
        // Gönderilmemiş shipment'lar yeni taşıyıcıya çevrilir
        var shipmentlar = await db.Shipments
            .Where(s => request.OrderIds.Contains(s.OrderId) && s.ApiStatus == "pending")
            .ToListAsync(ct);
        foreach (var s in shipmentlar)
            s.FirmIntegrationId = request.CargoIntegrationId;

        await db.SaveChangesAsync(ct);
        return Result.Success(siparisler.Count);
    }
}
