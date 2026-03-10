using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.CreateGiftCard;

public record CreateGiftCardCommand(
    Guid FirmId,
    decimal Amount,
    string CurrencyCode,
    DateOnly ValidFrom,
    DateOnly? ValidUntil,
    bool IsSingleUse,
    Guid? CreatedForMemberId,
    Guid? CreatedFromOrderId,
    Guid CreatedBy) : IRequest<Result<Guid>>;
