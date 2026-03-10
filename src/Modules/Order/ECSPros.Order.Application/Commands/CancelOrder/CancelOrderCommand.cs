using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.CancelOrder;

public record CancelOrderCommand(
    Guid OrderId,
    Guid CancelledBy,
    string? Reason = null) : IRequest<Result<bool>>;
