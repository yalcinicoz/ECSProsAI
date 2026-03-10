using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Commands.CreatePickingPlan;

public record CreatePickingPlanCommand(
    Guid WarehouseId,
    string PlanType,
    List<Guid> OrderIds,
    Guid PlannedBy) : IRequest<Result<Guid>>;
