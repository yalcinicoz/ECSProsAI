using ECSPros.Promotion.Application.Services;
using ECSPros.Promotion.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Commands.ManageCoupon;

// P3: kupon tanımı panelden — storefront C3/C10 kupon akışının veri kaynağı.
// CouponType: "percentage" (DiscountValue = %) | "fixed" (DiscountValue = ₺)

public record CreateCouponCommand(
    string Code,
    Dictionary<string, string> NameI18n,
    string CouponType,
    decimal DiscountValue,
    int? UsageLimitTotal,
    int? UsageLimitPerMember,
    decimal? MinimumCartTotal,
    bool ValidForFirstOrderOnly,
    DateTime StartsAt,
    DateTime? EndsAt,
    Guid CreatedBy) : IRequest<Result<Guid>>;

public record UpdateCouponCommand(
    Guid Id,
    Dictionary<string, string> NameI18n,
    string CouponType,
    decimal DiscountValue,
    int? UsageLimitTotal,
    int? UsageLimitPerMember,
    decimal? MinimumCartTotal,
    bool ValidForFirstOrderOnly,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive,
    Guid UpdatedBy) : IRequest<Result<bool>>;

public class CreateCouponCommandHandler(IPromotionDbContext db)
    : IRequestHandler<CreateCouponCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateCouponCommand request, CancellationToken ct)
    {
        var kod = request.Code.Trim().ToUpperInvariant();
        if (kod.Length < 3)
            return Result.Failure<Guid>("Kupon kodu en az 3 karakter olmalıdır.");
        if (request.CouponType is not ("percentage" or "fixed"))
            return Result.Failure<Guid>("Kupon tipi 'percentage' veya 'fixed' olmalıdır.");
        if (request.DiscountValue <= 0)
            return Result.Failure<Guid>("İndirim değeri sıfırdan büyük olmalıdır.");
        if (request.CouponType == "percentage" && request.DiscountValue > 100)
            return Result.Failure<Guid>("Yüzde indirim 100'ü aşamaz.");

        var exists = await db.Coupons.AnyAsync(c => c.Code == kod, ct);
        if (exists)
            return Result.Failure<Guid>($"'{kod}' kupon kodu zaten mevcut.");

        var coupon = new Coupon
        {
            Code = kod,
            NameI18n = request.NameI18n,
            CouponType = request.CouponType,
            DiscountValue = request.DiscountValue,
            UsageLimitTotal = request.UsageLimitTotal,
            UsageLimitPerMember = request.UsageLimitPerMember,
            MinimumCartTotal = request.MinimumCartTotal,
            ValidForFirstOrderOnly = request.ValidForFirstOrderOnly,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            IsActive = true,
            CreatedBy = request.CreatedBy
        };

        db.Coupons.Add(coupon);
        await db.SaveChangesAsync(ct);
        return Result.Success(coupon.Id);
    }
}

public class UpdateCouponCommandHandler(IPromotionDbContext db)
    : IRequestHandler<UpdateCouponCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateCouponCommand request, CancellationToken ct)
    {
        var coupon = await db.Coupons.FirstOrDefaultAsync(c => c.Id == request.Id, ct);
        if (coupon is null)
            return Result.Failure<bool>("Kupon bulunamadı.");
        if (request.CouponType is not ("percentage" or "fixed"))
            return Result.Failure<bool>("Kupon tipi 'percentage' veya 'fixed' olmalıdır.");
        if (request.DiscountValue <= 0)
            return Result.Failure<bool>("İndirim değeri sıfırdan büyük olmalıdır.");
        if (request.CouponType == "percentage" && request.DiscountValue > 100)
            return Result.Failure<bool>("Yüzde indirim 100'ü aşamaz.");

        coupon.NameI18n = request.NameI18n;
        coupon.CouponType = request.CouponType;
        coupon.DiscountValue = request.DiscountValue;
        coupon.UsageLimitTotal = request.UsageLimitTotal;
        coupon.UsageLimitPerMember = request.UsageLimitPerMember;
        coupon.MinimumCartTotal = request.MinimumCartTotal;
        coupon.ValidForFirstOrderOnly = request.ValidForFirstOrderOnly;
        coupon.StartsAt = request.StartsAt;
        coupon.EndsAt = request.EndsAt;
        coupon.IsActive = request.IsActive;
        coupon.UpdatedAt = DateTime.UtcNow;
        coupon.UpdatedBy = request.UpdatedBy;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
