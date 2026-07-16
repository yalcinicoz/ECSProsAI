using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetReviewsForModeration;

/// <summary>E7: admin moderasyon kuyruğu — durum filtreli, sayfalı.</summary>
public record GetReviewsForModerationQuery(
    string? Status = "pending",
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<ModerationReviewDto>>>;

public record ModerationReviewDto(
    Guid Id,
    Guid FirmPlatformId,
    Guid MemberId,
    string MemberName,
    string ProductCode,
    int Rating,
    string? Text,
    string Status,
    string? RejectReason,
    DateTime CreatedAt,
    DateTime? ModeratedAt,
    string? Topic = null,                          // İP-5
    IReadOnlyList<string>? Photos = null);         // İP-5: moderasyon foto önizlemesi (K16)

public class GetReviewsForModerationQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetReviewsForModerationQuery, Result<PagedResult<ModerationReviewDto>>>
{
    public async Task<Result<PagedResult<ModerationReviewDto>>> Handle(
        GetReviewsForModerationQuery request, CancellationToken ct)
    {
        var q = db.ProductReviews.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Status))
            q = q.Where(r => r.Status == request.Status);

        var toplam = await q.CountAsync(ct);
        var kayitlar = await q.OrderBy(r => r.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new ModerationReviewDto(
                r.Id, r.FirmPlatformId, r.MemberId, r.MemberName, r.ProductCode,
                r.Rating, r.Text, r.Status, r.RejectReason, r.CreatedAt, r.ModeratedAt, r.Topic, null))
            .ToListAsync(ct);

        // İP-5 (K16): foto önizlemeleri — yorumla birlikte moderatöre gösterilir.
        var idler = kayitlar.Select(k => k.Id).ToList();
        if (idler.Count > 0)
        {
            var fotolar = await db.ProductReviewPhotos.AsNoTracking()
                .Where(p => idler.Contains(p.ReviewId))
                .OrderBy(p => p.SortOrder)
                .ToListAsync(ct);
            if (fotolar.Count > 0)
            {
                var grup = fotolar.GroupBy(p => p.ReviewId)
                    .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(p => p.PhotoUrl).ToList());
                kayitlar = kayitlar
                    .Select(k => grup.TryGetValue(k.Id, out var f) ? k with { Photos = f } : k)
                    .ToList();
            }
        }

        return Result.Success(new PagedResult<ModerationReviewDto>(kayitlar, toplam, request.Page, request.PageSize));
    }
}
