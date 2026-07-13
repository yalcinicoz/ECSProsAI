using ECSPros.Promotion.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Queries.GetCoupons;

// P3: admin kupon listesi — tanımlar bugüne dek yalnız API/SQL'den giriliyordu
public record GetCouponsQuery(
    string? Search = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<CouponDto>>>;

public record CouponDto(
    Guid Id,
    Guid? CampaignId,
    Guid? MemberId,
    string Code,
    Dictionary<string, string> NameI18n,
    string CouponType,
    decimal DiscountValue,
    int? UsageLimitTotal,
    int? UsageLimitPerMember,
    int UsageCount,
    decimal? MinimumCartTotal,
    bool ValidForFirstOrderOnly,
    Guid? MemberGroupId,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive,
    DateTime CreatedAt);

public class GetCouponsQueryHandler(IPromotionDbContext db)
    : IRequestHandler<GetCouponsQuery, Result<PagedResult<CouponDto>>>
{
    public async Task<Result<PagedResult<CouponDto>>> Handle(GetCouponsQuery request, CancellationToken ct)
    {
        var query = db.Coupons.AsNoTracking();

        if (request.IsActive.HasValue)
            query = query.Where(c => c.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(c => c.Code.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CouponDto(
                c.Id, c.CampaignId, c.MemberId, c.Code, c.NameI18n, c.CouponType,
                c.DiscountValue, c.UsageLimitTotal, c.UsageLimitPerMember, c.UsageCount,
                c.MinimumCartTotal, c.ValidForFirstOrderOnly, c.MemberGroupId,
                c.StartsAt, c.EndsAt, c.IsActive, c.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<CouponDto>(items, totalCount, request.Page, request.PageSize));
    }
}
