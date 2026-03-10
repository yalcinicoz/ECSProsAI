using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.SendQuote;

public class SendQuoteCommandHandler : IRequestHandler<SendQuoteCommand, Result<bool>>
{
    private readonly IOrderDbContext _context;

    public SendQuoteCommandHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(SendQuoteCommand request, CancellationToken cancellationToken)
    {
        var quote = await _context.Quotes
            .FirstOrDefaultAsync(q => q.Id == request.QuoteId, cancellationToken);

        if (quote is null)
            return Result.Failure<bool>("Teklif bulunamadı.");

        if (quote.Status != "draft")
            return Result.Failure<bool>($"'{quote.Status}' durumundaki teklif gönderilemez.");

        quote.Status = "sent";
        quote.SentAt = DateTime.UtcNow;
        quote.UpdatedAt = DateTime.UtcNow;
        quote.UpdatedBy = request.SentBy;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
