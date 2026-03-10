using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.CreateGiftCard;

public class CreateGiftCardCommandHandler : IRequestHandler<CreateGiftCardCommand, Result<Guid>>
{
    private readonly IOrderDbContext _context;

    public CreateGiftCardCommandHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateGiftCardCommand request, CancellationToken cancellationToken)
    {
        // Benzersiz kod üret: GC-XXXX-XXXX-XXXX
        var raw = Guid.NewGuid().ToString("N").ToUpper();
        var code = $"GC-{raw[..4]}-{raw[4..8]}-{raw[8..12]}";

        var giftCard = new GiftCard
        {
            Code = code,
            FirmId = request.FirmId,
            OriginalAmount = request.Amount,
            RemainingAmount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            ValidFrom = request.ValidFrom,
            ValidUntil = request.ValidUntil,
            IsSingleUse = request.IsSingleUse,
            CreatedForMemberId = request.CreatedForMemberId,
            CreatedFromOrderId = request.CreatedFromOrderId,
            Status = "active",
            CreatedBy = request.CreatedBy
        };

        _context.GiftCards.Add(giftCard);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(giftCard.Id);
    }
}
