using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.UseGiftCard;

public class UseGiftCardCommandHandler : IRequestHandler<UseGiftCardCommand, Result<decimal>>
{
    private readonly IOrderDbContext _context;

    public UseGiftCardCommandHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<decimal>> Handle(UseGiftCardCommand request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var giftCard = await _context.GiftCards
            .FirstOrDefaultAsync(g => g.Code == request.Code.Trim().ToUpper(), cancellationToken);

        if (giftCard is null)
            return Result.Failure<decimal>("Geçersiz hediye kartı kodu.");

        if (giftCard.Status != "active")
            return Result.Failure<decimal>("Hediye kartı aktif değil.");

        if (giftCard.ValidFrom > today)
            return Result.Failure<decimal>("Hediye kartı henüz geçerli değil.");

        if (giftCard.ValidUntil.HasValue && giftCard.ValidUntil < today)
            return Result.Failure<decimal>("Hediye kartının geçerlilik süresi dolmuş.");

        if (giftCard.RemainingAmount <= 0)
            return Result.Failure<decimal>("Hediye kartı bakiyesi tükenmiş.");

        var deductAmount = Math.Min(request.Amount, giftCard.RemainingAmount);
        giftCard.RemainingAmount -= deductAmount;

        if (giftCard.IsSingleUse || giftCard.RemainingAmount == 0)
            giftCard.Status = "used";

        giftCard.UpdatedAt = DateTime.UtcNow;
        giftCard.UpdatedBy = request.UsedBy;

        _context.GiftCardTransactions.Add(new GiftCardTransaction
        {
            GiftCardId = giftCard.Id,
            TransactionType = "redeem",
            Amount = deductAmount,
            BalanceAfter = giftCard.RemainingAmount,
            OrderId = request.OrderId,
            Notes = $"Sipariş ödemesi: {request.OrderId}"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(deductAmount);
    }
}
