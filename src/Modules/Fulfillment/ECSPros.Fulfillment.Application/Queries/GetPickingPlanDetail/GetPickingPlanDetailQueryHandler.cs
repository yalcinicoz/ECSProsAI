using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Queries.GetPickingPlanDetail;

public class GetPickingPlanDetailQueryHandler : IRequestHandler<GetPickingPlanDetailQuery, Result<PickingPlanDetailDto>>
{
    private readonly IFulfillmentDbContext _context;

    public GetPickingPlanDetailQueryHandler(IFulfillmentDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PickingPlanDetailDto>> Handle(GetPickingPlanDetailQuery request, CancellationToken cancellationToken)
    {
        var plan = await _context.PickingPlans
            .Include(p => p.Bins)
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, cancellationToken);

        if (plan is null)
            return Result.Failure<PickingPlanDetailDto>("Toplama planı bulunamadı.");

        return Result.Success(new PickingPlanDetailDto(
            plan.Id,
            plan.PlanNumber,
            plan.WarehouseId,
            plan.PlanType,
            plan.Status,
            plan.PlannedBy,
            plan.PlannedAt,
            plan.StartedAt,
            plan.CompletedAt,
            plan.Bins.Select(b => new SortingBinDto(b.Id, b.BinNumber, b.OrderId, b.Status)).ToList()));
    }
}
