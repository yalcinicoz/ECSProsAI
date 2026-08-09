using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.MarkLineShort;

/// <summary>OP2: "Bulunamadı" — personel rafta ürünü bulamadı; satır short işaretlenir
/// (görev izlemede görünür, OBM/eksik akışında ele alınır).</summary>
public record MarkLineShortCommand(Guid LineId, Guid ActorId) : IRequest<Result<bool>>;

public class MarkLineShortCommandHandler(IFulfillmentDbContext db)
    : IRequestHandler<MarkLineShortCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(MarkLineShortCommand request, CancellationToken ct)
    {
        var satir = await db.PickingPlanLines.FirstOrDefaultAsync(l => l.Id == request.LineId, ct);
        if (satir is null) return Result.Failure<bool>("Satır bulunamadı.");
        if (satir.AssignedTo != request.ActorId)
            return Result.Failure<bool>("Satır size atanmamış.");
        if (satir.Status is not ("assigned" or "pending"))
            return Result.Failure<bool>($"'{satir.Status}' durumundaki satır bulunamadı işaretlenemez.");

        satir.Status = "short";
        satir.UpdatedAt = DateTime.UtcNow;
        satir.UpdatedBy = request.ActorId;
        db.OperationLogs.Add(new OperationLog
        {
            OrderId = satir.OrderId, OrderItemId = satir.OrderItemId,
            PickingPlanId = satir.PickingPlanId, Action = "line_short",
            ActorId = request.ActorId, CreatedBy = request.ActorId,
            Detail = new Dictionary<string, object> { ["sku"] = satir.Sku, ["bin"] = satir.SourceBinCode ?? "" }
        });
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
