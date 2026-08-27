using ECSPros.Promotion.Application.Queries.ValidateCoupon;
using ECSPros.Shared.Contracts;
using MediatR;

namespace ECSPros.Promotion.Application.Services;

/// <summary>ICouponValidator adaptörü — mevcut ValidateCoupon sorgusunu (sepetteki doğrulamayla
/// AYNI kurallar: aktiflik/tarih/limitler/min sepet/üye sahipliği) checkout'a açar.</summary>
public class CouponValidatorService(ISender sender) : ICouponValidator
{
    public async Task<CouponCheckResult> ValidateAsync(
        string code, decimal cartTotal, Guid? memberId, CancellationToken ct = default)
    {
        var sonuc = await sender.Send(new ValidateCouponQuery(code, cartTotal, memberId), ct);
        return sonuc.IsSuccess
            ? new CouponCheckResult(sonuc.Value!.CouponId, sonuc.Value.DiscountAmount, null)
            : new CouponCheckResult(Guid.Empty, 0, sonuc.Error ?? "Kupon doğrulanamadı.");
    }
}
