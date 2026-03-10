using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Promotion.Application.Queries.ValidateCoupon;

public record ValidateCouponQuery(
    string Code,
    decimal CartTotal,
    Guid? MemberId = null,
    bool IsFirstOrder = false) : IRequest<Result<CouponValidationResult>>;

public record CouponValidationResult(
    Guid CouponId,
    string CouponType,
    decimal DiscountAmount,
    string DiscountDescription);
