using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Queries.GetPickingPlans;

public record GetPickingPlansQuery(
    string? Status,
    Guid? WarehouseId,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<PickingPlanDto>>>;

public record PickingPlanDto(
    Guid Id,
    string PlanNumber,
    Guid WarehouseId,
    string PlanType,
    string Status,
    Guid PlannedBy,
    DateTime PlannedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt);
