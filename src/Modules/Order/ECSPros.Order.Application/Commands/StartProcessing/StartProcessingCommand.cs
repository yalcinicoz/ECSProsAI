using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.StartProcessing;

public record StartProcessingCommand(
    Guid OrderId,
    Guid? PickingPlanId,
    Guid UpdatedBy) : IRequest<Result<bool>>;
