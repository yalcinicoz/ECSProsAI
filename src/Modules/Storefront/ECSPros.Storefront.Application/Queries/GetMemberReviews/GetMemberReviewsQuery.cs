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
    DateTime? DeletedAt,
    string? Topic = null,                          // İP-5
    IReadOnlyList<string>? Photos = null);         // İP-5

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
                r.IsDeleted, r.CreatedAt, r.DeletedAt, r.Topic, null))
            .ToListAsync(ct);

        // İP-5: fotoğraflar (silinen yorumunki de görünür — kayıt sahibi üyenin kendisi)
        var idler = yorumlar.Select(y => y.Id).ToList();
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
                yorumlar = yorumlar
                    .Select(y => grup.TryGetValue(y.Id, out var f) ? y with { Photos = f } : y)
                    .ToList();
            }
        }

        return Result.Success(yorumlar);
    }
}
