using ECSPros.Storefront.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetMemberEngagementSummary;

// P4: admin üye detayı — üyenin storefront etkileşim özeti (sayılar)
public record GetMemberEngagementSummaryQuery(Guid MemberId) : IRequest<Result<MemberEngagementSummaryDto>>;

public record MemberEngagementSummaryDto(
    int FavoriteCount,
    int CollectionCount,
    int ReviewCount,
    int SavedSearchCount,
    int ActiveStockAlertCount,
    int ViewedProductCount);

public class GetMemberEngagementSummaryQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetMemberEngagementSummaryQuery, Result<MemberEngagementSummaryDto>>
{
    public async Task<Result<MemberEngagementSummaryDto>> Handle(GetMemberEngagementSummaryQuery request, CancellationToken ct)
    {
        var favori = await db.Favorites.CountAsync(f => f.MemberId == request.MemberId, ct);
        var koleksiyon = await db.Collections.CountAsync(c => c.MemberId == request.MemberId, ct);
        var yorum = await db.ProductReviews.CountAsync(r => r.MemberId == request.MemberId, ct);
        var arama = await db.SavedSearches.CountAsync(s => s.MemberId == request.MemberId, ct);
        var alarm = await db.StockAlerts.CountAsync(a => a.MemberId == request.MemberId && a.Status == "active", ct);
        var gezilen = await db.ViewedProducts.CountAsync(v => v.MemberId == request.MemberId, ct);

        return Result.Success(new MemberEngagementSummaryDto(favori, koleksiyon, yorum, arama, alarm, gezilen));
    }
}
