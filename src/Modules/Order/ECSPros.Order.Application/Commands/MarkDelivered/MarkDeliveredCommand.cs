using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.MarkDelivered;

public record MarkDeliveredCommand(
    Guid OrderId,
    Guid UpdatedBy) : IRequest<Result<bool>>;
