using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Commands.CompletePickingPlan;

public record CompletePickingPlanCommand(Guid PlanId, Guid CompletedBy) : IRequest<Result<bool>>;
