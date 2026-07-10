using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Promotion.Application.Queries.GetMemberCoupons;

/// <summary>
/// E9: üyenin kullanabileceği kuponlar — Hesabım → İndirim Kuponlarım sayfası ve
/// sepetteki "Kuponlarım" modalı. Yalnız üyeye (MemberId) veya üyenin grubuna
/// (MemberGroupId) TANIMLI kuponlar listelenir; herkese açık pazarlama kodları
/// (ikisi de null) elle girilir, burada sızdırılmaz. Aktiflik/tarih/limit koşulları
/// ValidateCoupon ile aynı; sepet tutarı bilinmediğinden MinimumCartTotal koşul
/// metni olarak döner (asıl doğrulama uygulanırken yine ValidateCoupon'da).
/// MemberGroupId'yi API katmanı CRM'den çözer (modüller birbirini bilmez).
/// </summary>
public record GetMemberCouponsQuery(Guid MemberId, Guid? MemberGroupId = null)
    : IRequest<Result<List<MemberCouponDto>>>;

public record MemberCouponDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    string CouponType,          // percentage | fixed
    decimal DiscountValue,
    string DiscountText,        // "%10 indirim" / "150,00 TL indirim"
    decimal? MinimumCartTotal,
    DateTime? EndsAt,
    bool ValidForFirstOrderOnly);
