using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetProductReviews;

/// <summary>E7: ürünün yayında (approved) yorumları — sayfalı; ürün detayı /
/// değerlendirmeler sayfası bundan beslenir.</summary>
public record GetProductReviewsQuery(
    Guid FirmPlatformId,
    string ProductCode,
    int Page = 1,
    int PageSize = 10) : IRequest<Result<PagedResult<ProductReviewDto>>>;

public record ProductReviewDto(
    Guid Id,
    int Rating,
    string? Text,
    string MemberName,
    DateTime CreatedAt);

public class GetProductReviewsQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetProductReviewsQuery, Result<PagedResult<ProductReviewDto>>>
{
    public async Task<Result<PagedResult<ProductReviewDto>>> Handle(GetProductReviewsQuery request, CancellationToken ct)
    {
        var q = db.ProductReviews.AsNoTracking()
            .Where(r => r.FirmPlatformId == request.FirmPlatformId
                        && r.ProductCode == request.ProductCode && r.Status == "approved");
        var toplam = await q.CountAsync(ct);
        var kayitlar = await q.OrderByDescending(r => r.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new ProductReviewDto(r.Id, r.Rating, r.Text, r.MemberName, r.CreatedAt))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<ProductReviewDto>(kayitlar, toplam, request.Page, request.PageSize));
    }
}
