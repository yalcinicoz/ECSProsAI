using ECSPros.Pos.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Pos.Application.Queries.GetSessionSummary;

public class GetSessionSummaryQueryHandler : IRequestHandler<GetSessionSummaryQuery, Result<SessionSummaryDto>>
{
    private readonly IPosDbContext _context;

    public GetSessionSummaryQueryHandler(IPosDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SessionSummaryDto>> Handle(GetSessionSummaryQuery request, CancellationToken cancellationToken)
    {
        var session = await _context.PosSessions
            .Include(s => s.Register)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session is null)
            return Result.Failure<SessionSummaryDto>("Oturum bulunamadı.");

        var sales = await _context.PosSales
            .Include(s => s.Payments)
            .Where(s => s.SessionId == request.SessionId)
            .ToListAsync(cancellationToken);

        var completedSales = sales.Where(s => s.Status == "completed").ToList();
        var refundedSales = sales.Where(s => s.Status == "refunded").ToList();

        var totalSalesAmount = completedSales.Sum(s => s.GrandTotal);
        var totalRefundAmount = refundedSales.Sum(s => s.GrandTotal);
        var netAmount = totalSalesAmount - totalRefundAmount;

        // Ödeme yöntemine göre özet (yalnızca tamamlanmış satışlar)
        var byPaymentMethod = completedSales
            .SelectMany(s => s.Payments)
            .GroupBy(p => p.PaymentMethod)
            .Select(g => new PaymentMethodSummaryDto(
                g.Key,
                completedSales.Count(s => s.Payments.Any(p => p.PaymentMethod == g.Key)),
                g.Sum(p => p.Amount)))
            .ToList();

        // Nakit hesabı: açılış bakiyesi + nakit satışlar - nakit iadeler
        var cashSales = completedSales
            .SelectMany(s => s.Payments)
            .Where(p => p.PaymentMethod == "cash")
            .Sum(p => p.Amount);

        var cashRefunds = refundedSales
            .SelectMany(s => s.Payments)
            .Where(p => p.PaymentMethod == "cash")
            .Sum(p => p.Amount);

        var expectedCash = session.OpeningCash + cashSales - cashRefunds;
        var closingCash = session.ClosingCash ?? 0m;
        var cashDifference = session.Status == "closed" ? closingCash - expectedCash : 0m;

        return Result.Success(new SessionSummaryDto(
            session.Id,
            session.RegisterId,
            session.Register.Name,
            session.OpenedAt,
            session.ClosedAt,
            session.Status,
            session.OpeningCash,
            closingCash,
            completedSales.Count,
            totalSalesAmount,
            refundedSales.Count,
            totalRefundAmount,
            netAmount,
            byPaymentMethod,
            expectedCash,
            cashDifference));
    }
}
