using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.RespondQuote;

public class RespondQuoteCommandHandler : IRequestHandler<RespondQuoteCommand, Result<bool>>
{
    private readonly IOrderDbContext _context;

    public RespondQuoteCommandHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(RespondQuoteCommand request, CancellationToken cancellationToken)
    {
        var quote = await _context.Quotes
            .FirstOrDefaultAsync(q => q.Id == request.QuoteId, cancellationToken);

        if (quote is null)
            return Result.Failure<bool>("Teklif bulunamadı.");

        if (quote.Status != "sent")
            return Result.Failure<bool>($"'{quote.Status}' durumundaki teklife yanıt verilemez.");

        if (quote.ValidUntil < DateTime.UtcNow)
            return Result.Failure<bool>("Teklifin geçerlilik süresi dolmuş.");

        quote.Status = request.Accepted ? "accepted" : "rejected";
        quote.RespondedAt = DateTime.UtcNow;
        quote.ViewedAt ??= DateTime.UtcNow;
        quote.UpdatedAt = DateTime.UtcNow;
        quote.UpdatedBy = request.RespondedBy;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
