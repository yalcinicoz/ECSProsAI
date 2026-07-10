using ECSPros.Promotion.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Queries.GetMemberCoupons;

public class GetMemberCouponsQueryHandler : IRequestHandler<GetMemberCouponsQuery, Result<List<MemberCouponDto>>>
{
    private readonly IPromotionDbContext _context;

    public GetMemberCouponsQueryHandler(IPromotionDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<MemberCouponDto>>> Handle(GetMemberCouponsQuery request, CancellationToken ct)
    {
        var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        var now = DateTime.UtcNow;

        var adaylar = await _context.Coupons
            .Include(c => c.Usages)
            .Where(c => c.IsActive
                        && c.StartsAt <= now
                        && (c.EndsAt == null || c.EndsAt >= now)
                        && (c.UsageLimitTotal == null || c.UsageCount < c.UsageLimitTotal)
                        && (c.MemberId == request.MemberId
                            || (c.MemberId == null && c.MemberGroupId != null
                                && c.MemberGroupId == request.MemberGroupId)))
            .OrderBy(c => c.EndsAt == null).ThenBy(c => c.EndsAt)
            .ToListAsync(ct);

        var liste = adaylar
            .Where(c => c.UsageLimitPerMember == null
                        || c.Usages.Count(u => u.MemberId == request.MemberId) < c.UsageLimitPerMember)
            .Select(c => new MemberCouponDto(
                c.Id,
                c.Code,
                c.NameI18n,
                c.CouponType,
                c.DiscountValue,
                c.CouponType switch
                {
                    "percentage" => $"%{c.DiscountValue.ToString("0.##", tr)} indirim",
                    "fixed"      => $"{c.DiscountValue.ToString("N2", tr)} TL indirim",
                    _            => $"{c.DiscountValue.ToString("N2", tr)} indirim"
                },
                c.MinimumCartTotal,
                c.EndsAt,
                c.ValidForFirstOrderOnly))
            .ToList();

        return Result.Success(liste);
    }
}
