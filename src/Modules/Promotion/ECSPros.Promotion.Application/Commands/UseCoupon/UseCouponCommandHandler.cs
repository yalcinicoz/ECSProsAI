using ECSPros.Promotion.Application.Services;
using ECSPros.Promotion.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Commands.UseCoupon;

public class UseCouponCommandHandler : IRequestHandler<UseCouponCommand, Result<bool>>
{
    private readonly IPromotionDbContext _context;

    public UseCouponCommandHandler(IPromotionDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UseCouponCommand request, CancellationToken cancellationToken)
    {
        var coupon = await _context.Coupons
            .FirstOrDefaultAsync(c => c.Id == request.CouponId, cancellationToken);

        if (coupon is null)
            return Result.Failure<bool>("Kupon bulunamadı.");

        coupon.UsageCount++;

        var usage = new CouponUsage
        {
            CouponId = request.CouponId,
            MemberId = request.MemberId,
            OrderId = request.OrderId,
            DiscountAmount = request.DiscountAmount,
            UsedAt = DateTime.UtcNow
        };

        _context.CouponUsages.Add(usage);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
