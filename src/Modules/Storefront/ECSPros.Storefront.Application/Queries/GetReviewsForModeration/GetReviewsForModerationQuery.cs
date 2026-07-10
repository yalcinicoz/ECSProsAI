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
    DateTime? ModeratedAt);

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
                r.Rating, r.Text, r.Status, r.RejectReason, r.CreatedAt, r.ModeratedAt))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<ModerationReviewDto>(kayitlar, toplam, request.Page, request.PageSize));
    }
}
