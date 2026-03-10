using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Queries.GetPickingPlans;

public class GetPickingPlansQueryHandler : IRequestHandler<GetPickingPlansQuery, Result<PagedResult<PickingPlanDto>>>
{
    private readonly IFulfillmentDbContext _context;

    public GetPickingPlansQueryHandler(IFulfillmentDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedResult<PickingPlanDto>>> Handle(GetPickingPlansQuery request, CancellationToken cancellationToken)
    {
        var query = _context.PickingPlans.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(p => p.Status == request.Status);

        if (request.WarehouseId.HasValue)
            query = query.Where(p => p.WarehouseId == request.WarehouseId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.PlannedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new PickingPlanDto(
                p.Id,
                p.PlanNumber,
                p.WarehouseId,
                p.PlanType,
                p.Status,
                p.PlannedBy,
                p.PlannedAt,
                p.StartedAt,
                p.CompletedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<PickingPlanDto>(items, totalCount, request.Page, request.PageSize));
    }
}
