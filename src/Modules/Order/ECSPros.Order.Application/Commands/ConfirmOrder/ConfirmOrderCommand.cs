using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.ConfirmOrder;

public record ConfirmOrderCommand(
    Guid OrderId,
    Guid WarehouseId,
    Guid ConfirmedBy) : IRequest<Result<bool>>;
