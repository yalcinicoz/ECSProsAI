using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.RerouteCargoOutbox;

/// <summary>
/// OP5 (K-9): kargo yönlendirme — henüz GÖNDERİLMEMİŞ (pending/failed) bildirimler hedef
/// taşıyıcıya toplu/tekil taşınır; kayıt yeniden pending olur. Gönderilmiş paketlerin
/// iptal+yeniden gönderimi gerçek taşıyıcı API'leriyle (KG1) gelecek.
/// Order.RequestedCargo* senkronu API katmanında (Order komutu) yapılır.
/// </summary>
public record RerouteCargoOutboxCommand(
    List<Guid> OutboxIds,
    Guid TargetIntegrationId,
    string TargetName,
    Guid ActorId) : IRequest<Result<int>>;

public class RerouteCargoOutboxCommandHandler(IFulfillmentDbContext db)
    : IRequestHandler<RerouteCargoOutboxCommand, Result<int>>
{
    public async Task<Result<int>> Handle(RerouteCargoOutboxCommand request, CancellationToken ct)
    {
        if (request.OutboxIds.Count == 0) return Result.Failure<int>("Kayıt seçilmedi.");
        var kayitlar = await db.CargoNotifyOutbox
            .Where(o => request.OutboxIds.Contains(o.Id))
            .ToListAsync(ct);
        var gonderilmis = kayitlar.Count(k => k.Status == "sent");
        if (gonderilmis > 0)
            return Result.Failure<int>(
                $"{gonderilmis} kayıt taşıyıcıya zaten gönderilmiş — iptal+yeniden gönderim gerçek API entegrasyonuyla (KG1) gelecek.");

        var now = DateTime.UtcNow;
        foreach (var k in kayitlar)
        {
            var eski = k.CargoName;
            k.CargoIntegrationId = request.TargetIntegrationId;
            k.CargoName = request.TargetName;
            k.Status = "pending";
            k.LastError = null;
            k.NextAttemptAt = now;
            k.UpdatedAt = now;
            k.UpdatedBy = request.ActorId;
            db.OperationLogs.Add(new OperationLog
            {
                OrderId = k.OrderId, PackageId = k.PackageId, Action = "cargo_rerouted",
                ActorId = request.ActorId, CreatedBy = request.ActorId,
                Detail = new Dictionary<string, object>
                    { ["from"] = eski ?? "atanmamış", ["to"] = request.TargetName }
            });
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(kayitlar.Count);
    }
}
