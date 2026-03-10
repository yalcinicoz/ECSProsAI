using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Commands.StartPickingPlan;

public record StartPickingPlanCommand(Guid PlanId, Guid StartedBy) : IRequest<Result<bool>>;
