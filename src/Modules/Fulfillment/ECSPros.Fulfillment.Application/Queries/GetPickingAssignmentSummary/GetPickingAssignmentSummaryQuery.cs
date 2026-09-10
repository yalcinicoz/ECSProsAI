using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Queries.GetPickingAssignmentSummary;

/// <summary>FAZ 15.4e (2026-09-10, eski "Kullanıcı Toplama Yönetimi"): aktif görevlerde personel başına atanan / toplanan / kalan
/// satır sayıları + atanmamış havuz. Ad çözümü panelde (IAM kullanıcı listesi).</summary>
public record GetPickingAssignmentSummaryQuery(Guid? PlanId) : IRequest<Result<PickingAssignmentSummaryDto>>;

public record AssignmentPlanDto(Guid PlanId, string PlanNumber, string Status, DateTime PlannedAt, int Unassigned, int Assigned, int Picked, int Total);
public record AssignmentPersonDto(Guid PlanId, Guid UserId, int Assigned, int Picked, int Remaining);
public record PickingAssignmentSummaryDto(List<AssignmentPlanDto> Plans, List<AssignmentPersonDto> People);

public class GetPickingAssignmentSummaryQueryHandler(IFulfillmentDbContext db) : IRequestHandler<GetPickingAssignmentSummaryQuery, Result<PickingAssignmentSummaryDto>>
{
    public async Task<Result<PickingAssignmentSummaryDto>> Handle(GetPickingAssignmentSummaryQuery r, CancellationToken ct)
    {
        var plans = db.PickingPlans.AsNoTracking().Where(p => p.Status == "pending" || p.Status == "picking");
        if (r.PlanId is { } pid) plans = plans.Where(p => p.Id == pid);
        var planList = await plans.OrderByDescending(p => p.PlannedAt).Take(50).ToListAsync(ct);
        var ids = planList.Select(p => p.Id).ToList();

        var satirlar = await db.PickingPlanLines.AsNoTracking()
            .Where(l => ids.Contains(l.PickingPlanId))
            .GroupBy(l => new { l.PickingPlanId, l.AssignedTo })
            .Select(g => new
            {
                g.Key.PickingPlanId, g.Key.AssignedTo,
                Total = g.Count(),
                Picked = g.Count(l => l.Status != "pending" && l.Status != "assigned"),
                Pending = g.Count(l => l.Status == "pending" || l.Status == "assigned"),
            }).ToListAsync(ct);

        var planDtos = planList.Select(p =>
        {
            var s = satirlar.Where(x => x.PickingPlanId == p.Id).ToList();
            var unassigned = s.Where(x => x.AssignedTo == null).Sum(x => x.Pending);
            var assigned = s.Where(x => x.AssignedTo != null).Sum(x => x.Pending);
            return new AssignmentPlanDto(p.Id, p.PlanNumber, p.Status, p.PlannedAt, unassigned, assigned, s.Sum(x => x.Picked), s.Sum(x => x.Total));
        }).ToList();
        var people = satirlar.Where(x => x.AssignedTo != null)
            .Select(x => new AssignmentPersonDto(x.PickingPlanId, x.AssignedTo!.Value, x.Total, x.Picked, x.Pending)).ToList();
        return Result.Success(new PickingAssignmentSummaryDto(planDtos, people));
    }
}
