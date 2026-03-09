using ECSPros.Pos.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Pos.Application.Commands.CloseSession;

public class CloseSessionCommandHandler : IRequestHandler<CloseSessionCommand, Result<bool>>
{
    private readonly IPosDbContext _context;

    public CloseSessionCommandHandler(IPosDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(CloseSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.PosSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.Status == "open", cancellationToken);

        if (session == null)
            return Result.Failure<bool>("Açık oturum bulunamadı.");

        // Calculate expected cash from transactions
        var transactions = await _context.PosSessionTransactions
            .Where(t => t.SessionId == request.SessionId)
            .ToListAsync(cancellationToken);

        var cashTransactions = transactions
            .Where(t => t.PaymentMethod == "cash")
            .Sum(t => t.Amount);

        var expectedCash = session.OpeningCash + cashTransactions;
        var difference = request.ClosingCash - expectedCash;

        session.ClosedAt = DateTime.UtcNow;
        session.ClosingCash = request.ClosingCash;
        session.ExpectedCash = expectedCash;
        session.CashDifference = difference;
        session.Status = "closed";
        session.Notes = request.Notes ?? session.Notes;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
