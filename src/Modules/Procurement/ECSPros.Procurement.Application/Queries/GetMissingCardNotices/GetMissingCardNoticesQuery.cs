using ECSPros.Procurement.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Queries.GetMissingCardNotices;

public record GetMissingCardNoticesQuery(Guid? ReceiptBatchId = null, string? Status = "open")
    : IRequest<Result<List<MissingCardNoticeDto>>>;

public record MissingCardNoticeDto(
    Guid Id, Guid? ReceiptBatchId, string DescriptionText, string Status, DateTime CreatedAt, DateTime? ResolvedAt);

public class GetMissingCardNoticesQueryHandler(IProcurementDbContext db)
    : IRequestHandler<GetMissingCardNoticesQuery, Result<List<MissingCardNoticeDto>>>
{
    public async Task<Result<List<MissingCardNoticeDto>>> Handle(GetMissingCardNoticesQuery request, CancellationToken ct)
    {
        var q = db.MissingCardNotices.AsNoTracking();
        if (request.ReceiptBatchId.HasValue) q = q.Where(n => n.ReceiptBatchId == request.ReceiptBatchId.Value);
        if (!string.IsNullOrWhiteSpace(request.Status)) q = q.Where(n => n.Status == request.Status);
        var list = await q.OrderByDescending(n => n.CreatedAt).Take(200)
            .Select(n => new MissingCardNoticeDto(n.Id, n.ReceiptBatchId, n.DescriptionText, n.Status, n.CreatedAt, n.ResolvedAt))
            .ToListAsync(ct);
        return Result.Success(list);
    }
}
