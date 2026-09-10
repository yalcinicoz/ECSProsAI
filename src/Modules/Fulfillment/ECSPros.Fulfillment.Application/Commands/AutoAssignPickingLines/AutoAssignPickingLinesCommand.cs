using ECSPros.Fulfillment.Application.Commands.AssignPickingLines;
using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.AutoAssignPickingLines;

/// <summary>FAZ 15.4e: sayıya göre dağıtım — görevin atanmamış (ya da <paramref name="FromAssignee"/>'nin henüz toplamadığı)
/// satırlarından rota sırasına göre <paramref name="Count"/> tanesi <paramref name="AssignTo"/>'ya verilir
/// (eski "CreateAssignToPersonnel" / "ShareOtherPersonnel"). Asıl atama AssignPickingLinesCommand ile (log + olay aynı).</summary>
public record AutoAssignPickingLinesCommand(Guid PlanId, Guid AssignTo, int Count, Guid? FromAssignee, Guid ActorId) : IRequest<Result<int>>;

public class AutoAssignPickingLinesCommandHandler(IFulfillmentDbContext db, ISender sender) : IRequestHandler<AutoAssignPickingLinesCommand, Result<int>>
{
    public async Task<Result<int>> Handle(AutoAssignPickingLinesCommand request, CancellationToken ct)
    {
        if (request.Count <= 0) return Result.Failure<int>("Adet 0'dan büyük olmalı.");
        if (request.FromAssignee == request.AssignTo) return Result.Failure<int>("Kaynak ve hedef personel aynı.");
        var q = db.PickingPlanLines.AsNoTracking().Where(l => l.PickingPlanId == request.PlanId && (l.Status == "pending" || l.Status == "assigned"));
        q = request.FromAssignee is { } from ? q.Where(l => l.AssignedTo == from) : q.Where(l => l.AssignedTo == null);
        var ids = await q.OrderBy(l => l.RouteOrder).ThenBy(l => l.Id).Take(request.Count).Select(l => l.Id).ToListAsync(ct);
        if (ids.Count == 0) return Result.Failure<int>(request.FromAssignee is null ? "Görevde atanmamış satır kalmadı." : "Kaynak personelin bekleyen satırı yok.");
        return await sender.Send(new AssignPickingLinesCommand(request.PlanId, ids, request.AssignTo, request.ActorId), ct);
    }
}
