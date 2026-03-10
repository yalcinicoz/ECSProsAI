using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Queries.GetGiftCardBalance;

public record GetGiftCardBalanceQuery(string Code) : IRequest<Result<GiftCardBalanceDto>>;

public record GiftCardBalanceDto(
    Guid Id,
    string Code,
    decimal OriginalAmount,
    decimal RemainingAmount,
    string CurrencyCode,
    DateOnly ValidFrom,
    DateOnly? ValidUntil,
    string Status);
