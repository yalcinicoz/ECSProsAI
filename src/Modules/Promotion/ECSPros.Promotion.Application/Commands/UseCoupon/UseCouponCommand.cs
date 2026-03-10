using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Promotion.Application.Commands.UseCoupon;

public record UseCouponCommand(
    Guid CouponId,
    Guid MemberId,
    Guid OrderId,
    decimal DiscountAmount) : IRequest<Result<bool>>;
