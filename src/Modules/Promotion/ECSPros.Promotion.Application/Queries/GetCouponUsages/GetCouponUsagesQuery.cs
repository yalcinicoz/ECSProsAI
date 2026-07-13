using ECSPros.Promotion.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Queries.GetCouponUsages;

// P3: kupon kullanım kayıtları (hangi üye hangi siparişte ne kadar indirim aldı)
public record GetCouponUsagesQuery(
    Guid CouponId,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<CouponUsageDto>>>;

public record CouponUsageDto(
    Guid Id,
    Guid MemberId,
    Guid OrderId,
    decimal DiscountAmount,
    DateTime UsedAt);

public class GetCouponUsagesQueryHandler(IPromotionDbContext db)
    : IRequestHandler<GetCouponUsagesQuery, Result<PagedResult<CouponUsageDto>>>
{
    public async Task<Result<PagedResult<CouponUsageDto>>> Handle(GetCouponUsagesQuery request, CancellationToken ct)
    {
        var query = db.CouponUsages.AsNoTracking().Where(u => u.CouponId == request.CouponId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(u => u.UsedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new CouponUsageDto(u.Id, u.MemberId, u.OrderId, u.DiscountAmount, u.UsedAt))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<CouponUsageDto>(items, totalCount, request.Page, request.PageSize));
    }
}
