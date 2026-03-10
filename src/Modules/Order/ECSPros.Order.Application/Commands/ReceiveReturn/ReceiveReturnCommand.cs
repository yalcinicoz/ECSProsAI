using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.ReceiveReturn;

public record ReceiveReturnCommand(
    Guid ReturnId,
    Guid WarehouseId,
    string? InspectionNotes,
    Guid ReceivedBy) : IRequest<Result<bool>>;
