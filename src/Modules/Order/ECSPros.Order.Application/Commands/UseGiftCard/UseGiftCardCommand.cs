using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.UseGiftCard;

public record UseGiftCardCommand(
    string Code,
    decimal Amount,
    Guid OrderId,
    Guid UsedBy) : IRequest<Result<decimal>>;
