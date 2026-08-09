using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.TakeSortingBox;

/// <summary>
/// OP3: paketleme personeli koliyi zimmetine alır — süreç bitene dek o personelde kalır,
/// başkası alamaz (kurgu). Zimmet bilgisi kolideki TÜM siparişlere loglanır (personel + zaman).
/// Koli dolmaya devam edebilir ("taken" koli hâlâ aynı kolidir).
/// </summary>
public record TakeSortingBoxCommand(Guid BoxId, Guid ActorId) : IRequest<Result<bool>>;

public class TakeSortingBoxCommandHandler(IFulfillmentDbContext db)
    : IRequestHandler<TakeSortingBoxCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(TakeSortingBoxCommand request, CancellationToken ct)
    {
        var koli = await db.SortingBoxes.FirstOrDefaultAsync(b => b.Id == request.BoxId, ct);
        if (koli is null) return Result.Failure<bool>("Koli bulunamadı.");
        if (koli.Status == "taken")
            return Result.Failure<bool>("Koli zaten başka bir personelin zimmetinde.");
        if (koli.Status == "closed")
            return Result.Failure<bool>("Kapalı koli alınamaz.");

        var now = DateTime.UtcNow;
        koli.Status = "taken";
        koli.TakenBy = request.ActorId;
        koli.TakenAt = now;
        koli.UpdatedAt = now;
        koli.UpdatedBy = request.ActorId;

        // Zimmet kolideki tüm siparişlere yazılır (kurgu: personel ID + zaman)
        var siparisler = await db.SortingBins
            .Where(sb => sb.SortingBoxId == koli.Id && sb.OrderId != null)
            .Select(sb => sb.OrderId!.Value)
            .ToListAsync(ct);
        foreach (var orderId in siparisler)
        {
            db.OperationLogs.Add(new OperationLog
            {
                OrderId = orderId, PickingPlanId = koli.PickingPlanId, Action = "box_taken",
                ActorId = request.ActorId, CreatedBy = request.ActorId,
                Detail = new Dictionary<string, object>
                    { ["box"] = koli.BoxNumber, ["generation"] = koli.Generation }
            });
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
