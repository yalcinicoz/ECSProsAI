using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Queries.GetPickingPlanDetail;

public record GetPickingPlanDetailQuery(Guid PlanId) : IRequest<Result<PickingPlanDetailDto>>;

public record PickingPlanDetailDto(
    Guid Id,
    string PlanNumber,
    Guid WarehouseId,
    string PlanType,
    string Status,
    Guid PlannedBy,
    DateTime PlannedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    List<SortingBinDto> Bins);

public record SortingBinDto(
    Guid Id,
    int BinNumber,
    Guid? OrderId,
    string Status);
