using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.ApproveReturn;

public record ApproveReturnCommand(
    Guid ReturnId,
    Guid ApprovedBy) : IRequest<Result<bool>>;
