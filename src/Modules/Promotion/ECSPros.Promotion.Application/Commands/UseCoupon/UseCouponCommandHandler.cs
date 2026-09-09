using ECSPros.Promotion.Application.Services;
using ECSPros.Promotion.Domain.Entities;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Commands.UseCoupon;

public class UseCouponCommandHandler : IRequestHandler<UseCouponCommand, Result<bool>>
{
    private readonly IPromotionDbContext _context;
    private readonly IMemberService _memberService;

    public UseCouponCommandHandler(IPromotionDbContext context, IMemberService memberService)
    {
        _context = context;
        _memberService = memberService;
    }

    public async Task<Result<bool>> Handle(UseCouponCommand request, CancellationToken cancellationToken)
    {
        var coupon = await _context.Coupons
            .FirstOrDefaultAsync(c => c.Id == request.CouponId, cancellationToken);

        if (coupon is null)
            return Result.Failure<bool>("Kupon bulunamadı.");

        // Hedef kontrolü ValidateCoupon'da da var; kullanım kaydı doğrudan çağrılırsa
        // kişiye/gruba özel kuponun başkasına yazılmaması için burada da uygulanır.
        Guid? uyeGrubuId = null;
        if (KuponHedefKurali.UyeGrubuGerekli(coupon.MemberGroupId, request.MemberId))
            uyeGrubuId = (await _memberService.GetMemberAsync(request.MemberId, cancellationToken))?.MemberGroupId;

        var hedefEngeli = KuponHedefKurali.Engel(
            coupon.MemberId, coupon.MemberGroupId, request.MemberId, uyeGrubuId);
        if (hedefEngeli is not null)
            return Result.Failure<bool>(hedefEngeli);

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
