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
    int PageSize = 10,
    List<int>? Ratings = null,   // H9 additive: puan filtresi (çoklu seçim)
    string? Sort = null,         // H9 additive: newest (vars.) | oldest | top (puan)
    string? Search = null) : IRequest<Result<PagedResult<ProductReviewDto>>>; // H9 additive: metin araması

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

        if (request.Ratings is { Count: > 0 })
            q = q.Where(r => request.Ratings.Contains(r.Rating));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var aranan = request.Search.Trim().ToLower();
            q = q.Where(r => r.Text != null && r.Text.ToLower().Contains(aranan));
        }

        var toplam = await q.CountAsync(ct);
        var sirali = request.Sort switch
        {
            "oldest" => q.OrderBy(r => r.CreatedAt),
            "top"    => q.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            _        => q.OrderByDescending(r => r.CreatedAt)
        };
        var kayitlar = await sirali
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new ProductReviewDto(r.Id, r.Rating, r.Text, r.MemberName, r.CreatedAt))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<ProductReviewDto>(kayitlar, toplam, request.Page, request.PageSize));
    }
}
