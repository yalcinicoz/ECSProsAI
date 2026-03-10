using ECSPros.Promotion.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Queries.ValidateCoupon;

public class ValidateCouponQueryHandler : IRequestHandler<ValidateCouponQuery, Result<CouponValidationResult>>
{
    private readonly IPromotionDbContext _context;

    public ValidateCouponQueryHandler(IPromotionDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CouponValidationResult>> Handle(ValidateCouponQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var coupon = await _context.Coupons
            .Include(c => c.Usages)
            .FirstOrDefaultAsync(c => c.Code == request.Code.Trim().ToUpper(), cancellationToken);

        if (coupon is null || !coupon.IsActive)
            return Result.Failure<CouponValidationResult>("Geçersiz veya pasif kupon kodu.");

        if (coupon.StartsAt > now)
            return Result.Failure<CouponValidationResult>("Kupon henüz aktif değil.");

        if (coupon.EndsAt.HasValue && coupon.EndsAt < now)
            return Result.Failure<CouponValidationResult>("Kupon süresi dolmuş.");

        if (coupon.UsageLimitTotal.HasValue && coupon.UsageCount >= coupon.UsageLimitTotal)
            return Result.Failure<CouponValidationResult>("Kupon kullanım limiti dolmuş.");

        if (coupon.MinimumCartTotal.HasValue && request.CartTotal < coupon.MinimumCartTotal)
            return Result.Failure<CouponValidationResult>(
                $"Bu kupon için minimum sepet tutarı {coupon.MinimumCartTotal:N2} ₺ olmalıdır.");

        if (coupon.ValidForFirstOrderOnly && !request.IsFirstOrder)
            return Result.Failure<CouponValidationResult>("Bu kupon yalnızca ilk sipariş için geçerlidir.");

        if (request.MemberId.HasValue)
        {
            if (coupon.MemberId.HasValue && coupon.MemberId != request.MemberId)
                return Result.Failure<CouponValidationResult>("Bu kupon size ait değil.");

            if (coupon.UsageLimitPerMember.HasValue)
            {
                var memberUsage = coupon.Usages.Count(u => u.MemberId == request.MemberId.Value);
                if (memberUsage >= coupon.UsageLimitPerMember)
                    return Result.Failure<CouponValidationResult>("Bu kuponu zaten kullandınız.");
            }
        }

        // İndirim hesapla
        decimal discountAmount = coupon.CouponType switch
        {
            "percentage" => Math.Round(request.CartTotal * coupon.DiscountValue / 100, 2),
            "fixed"      => Math.Min(coupon.DiscountValue, request.CartTotal),
            _            => coupon.DiscountValue
        };

        var description = coupon.CouponType switch
        {
            "percentage" => $"%{coupon.DiscountValue} indirim",
            "fixed"      => $"{coupon.DiscountValue:N2} ₺ indirim",
            _            => $"{coupon.DiscountValue:N2} indirim"
        };

        return Result.Success(new CouponValidationResult(
            coupon.Id,
            coupon.CouponType,
            discountAmount,
            description));
    }
}
