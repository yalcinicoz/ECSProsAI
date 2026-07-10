using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetMemberReviews;

/// <summary>E7: Yorumlarım — üyenin tüm yorumları (silinenler dahil; sekmeler
/// Status/IsDeleted'a göre ayrışır).</summary>
public record GetMemberReviewsQuery(Guid FirmPlatformId, Guid MemberId)
    : IRequest<Result<List<MemberReviewDto>>>;

public record MemberReviewDto(
    Guid Id,
    string ProductCode,
    int Rating,
    string? Text,
    string Status,
    string? RejectReason,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime? DeletedAt);

public class GetMemberReviewsQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetMemberReviewsQuery, Result<List<MemberReviewDto>>>
{
    public async Task<Result<List<MemberReviewDto>>> Handle(GetMemberReviewsQuery request, CancellationToken ct)
    {
        var yorumlar = await db.ProductReviews
            .IgnoreQueryFilters() // silinenler sekmesi için
            .Where(r => r.FirmPlatformId == request.FirmPlatformId && r.MemberId == request.MemberId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new MemberReviewDto(
                r.Id, r.ProductCode, r.Rating, r.Text, r.Status, r.RejectReason,
                r.IsDeleted, r.CreatedAt, r.DeletedAt))
            .ToListAsync(ct);
        return Result.Success(yorumlar);
    }
}
