using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetGiftCardBalance;

public class GetGiftCardBalanceQueryHandler : IRequestHandler<GetGiftCardBalanceQuery, Result<GiftCardBalanceDto>>
{
    private readonly IOrderDbContext _context;

    public GetGiftCardBalanceQueryHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<GiftCardBalanceDto>> Handle(GetGiftCardBalanceQuery request, CancellationToken cancellationToken)
    {
        var giftCard = await _context.GiftCards
            .FirstOrDefaultAsync(g => g.Code == request.Code.Trim().ToUpper(), cancellationToken);

        if (giftCard is null)
            return Result.Failure<GiftCardBalanceDto>("Hediye kartı bulunamadı.");

        return Result.Success(new GiftCardBalanceDto(
            giftCard.Id,
            giftCard.Code,
            giftCard.OriginalAmount,
            giftCard.RemainingAmount,
            giftCard.CurrencyCode,
            giftCard.ValidFrom,
            giftCard.ValidUntil,
            giftCard.Status));
    }
}
