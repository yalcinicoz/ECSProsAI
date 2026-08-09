using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Fulfillment.Domain.Events;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.AssignPickingLines;

/// <summary>
/// OP1: toplama satırlarını personele dağıtır (yeniden atama serbest — toplanmamış satırda).
/// OrderItem.PickAssignedTo/At, PickingLinesAssignedEvent ile Order modülünde senkron güncellenir.
/// </summary>
public record AssignPickingLinesCommand(
    Guid PlanId,
    List<Guid> LineIds,
    Guid AssignTo,
    Guid ActorId) : IRequest<Result<int>>;

public class AssignPickingLinesCommandHandler(IFulfillmentDbContext db, IPublisher publisher)
    : IRequestHandler<AssignPickingLinesCommand, Result<int>>
{
    public async Task<Result<int>> Handle(AssignPickingLinesCommand request, CancellationToken ct)
    {
        if (request.LineIds.Count == 0)
            return Result.Failure<int>("En az bir satır seçilmeli.");

        var plan = await db.PickingPlans.FirstOrDefaultAsync(p => p.Id == request.PlanId, ct);
        if (plan is null) return Result.Failure<int>("Görev bulunamadı.");
        if (plan.Status is not ("pending" or "picking"))
            return Result.Failure<int>($"'{plan.Status}' durumundaki görevde dağıtım yapılamaz.");

        var satirlar = await db.PickingPlanLines
            .Where(l => l.PickingPlanId == request.PlanId && request.LineIds.Contains(l.Id))
            .ToListAsync(ct);
        if (satirlar.Count == 0) return Result.Failure<int>("Satır bulunamadı.");

        var toplanmis = satirlar.Where(l => l.Status is not ("pending" or "assigned")).ToList();
        if (toplanmis.Count > 0)
            return Result.Failure<int>($"{toplanmis.Count} satır toplanmış/kapanmış — yeniden atanamaz.");

        var now = DateTime.UtcNow;
        foreach (var satir in satirlar)
        {
            satir.AssignedTo = request.AssignTo;
            satir.AssignedAt = now;
            satir.Status = "assigned";
            satir.UpdatedAt = now;
            satir.UpdatedBy = request.ActorId;

            db.OperationLogs.Add(new OperationLog
            {
                OrderId = satir.OrderId, OrderItemId = satir.OrderItemId,
                PickingPlanId = request.PlanId, Action = "line_assigned",
                ActorId = request.ActorId, CreatedBy = request.ActorId,
                Detail = new Dictionary<string, object>
                {
                    ["assignedTo"] = request.AssignTo,
                    ["sku"] = satir.Sku,
                    ["bin"] = satir.SourceBinCode ?? ""
                }
            });
        }

        await db.SaveChangesAsync(ct);
        await publisher.Publish(new PickingLinesAssignedEvent(
            request.PlanId, request.AssignTo,
            satirlar.Select(l => new AssignedLine(l.OrderId, l.OrderItemId)).ToList()), ct);

        return Result.Success(satirlar.Count);
    }
}
