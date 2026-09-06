using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetInvoiceSeriesGaps;

/// <summary>FE1 §2.5 boşluk denetimi: seri×yıl için sayaç (beklenen) ↔ kayıtlı sıra numaraları; eksikler listelenir.</summary>
public record GetInvoiceSeriesGapsQuery(Guid SeriesId) : IRequest<Result<List<InvoiceSeriesGapDto>>>;

public record InvoiceSeriesGapDto(
    string Year,
    int ExpectedLast,       // sayaç (en son verilen sıra)
    int RecordedCount,      // kayıtlı fatura (iptal dahil, silinmiş hariç)
    int CancelledCount,
    List<int> MissingSequences,   // ilk 200
    int MissingTotal,
    DateTime? LastInvoiceDate);

public class GetInvoiceSeriesGapsQueryHandler(IOrderDbContext db)
    : IRequestHandler<GetInvoiceSeriesGapsQuery, Result<List<InvoiceSeriesGapDto>>>
{
    public async Task<Result<List<InvoiceSeriesGapDto>>> Handle(GetInvoiceSeriesGapsQuery request, CancellationToken ct)
    {
        var exists = await db.InvoiceSeries.AsNoTracking().AnyAsync(s => s.Id == request.SeriesId, ct);
        if (!exists) return Result.Failure<List<InvoiceSeriesGapDto>>("Fatura serisi bulunamadı.");

        var counters = await db.InvoiceSeriesCounters.AsNoTracking()
            .Where(c => c.InvoiceSeriesId == request.SeriesId)
            .OrderBy(c => c.Year)
            .ToListAsync(ct);
        var recorded = await db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceSeriesId == request.SeriesId && i.NumberSource == InvoiceNumberSources.Internal)
            .Select(i => new { i.InvoiceYear, i.InvoiceSequence, i.Status })
            .ToListAsync(ct);

        var result = counters.Select(c =>
        {
            var rows = recorded.Where(r => r.InvoiceYear == c.Year).ToList();
            var seqs = rows.Select(r => r.InvoiceSequence).ToHashSet();
            var missing = new List<int>();
            for (var n = 1; n <= c.LastSequence; n++) if (!seqs.Contains(n)) missing.Add(n);
            return new InvoiceSeriesGapDto(
                c.Year, c.LastSequence, rows.Count, rows.Count(r => r.Status == "cancelled"),
                missing.Take(200).ToList(), missing.Count, c.LastInvoiceDate);
        }).ToList();

        return Result.Success(result);
    }
}
