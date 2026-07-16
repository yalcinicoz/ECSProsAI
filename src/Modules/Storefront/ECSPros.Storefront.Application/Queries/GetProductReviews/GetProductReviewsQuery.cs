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
    string? Search = null,       // H9 additive: metin araması
    List<string>? Topics = null, // İP-5 additive: konu filtresi (çoklu seçim)
    bool PhotosOnly = false) : IRequest<Result<PagedResult<ProductReviewDto>>>; // İP-5: yalnız fotoğraflı

public record ProductReviewDto(
    Guid Id,
    int Rating,
    string? Text,
    string MemberName,
    DateTime CreatedAt,
    string? Topic = null,                          // İP-5: konu etiketi
    IReadOnlyList<string>? Photos = null);         // İP-5: yorum fotoğrafları (sıralı)

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
        if (request.Topics is { Count: > 0 })
            q = q.Where(r => r.Topic != null && request.Topics.Contains(r.Topic));
        if (request.PhotosOnly)
            q = q.Where(r => db.ProductReviewPhotos.Any(p => p.ReviewId == r.Id));
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
            .Select(r => new ProductReviewDto(r.Id, r.Rating, r.Text, r.MemberName, r.CreatedAt, r.Topic, null))
            .ToListAsync(ct);

        // İP-5: fotoğraflar tek sorguda çekilip yorumlara dağıtılır.
        var idler = kayitlar.Select(k => k.Id).ToList();
        if (idler.Count > 0)
        {
            var fotolar = await db.ProductReviewPhotos.AsNoTracking()
                .Where(p => idler.Contains(p.ReviewId))
                .OrderBy(p => p.SortOrder)
                .ToListAsync(ct);
            if (fotolar.Count > 0)
            {
                var grup = fotolar.GroupBy(p => p.ReviewId)
                    .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(p => p.PhotoUrl).ToList());
                kayitlar = kayitlar
                    .Select(k => grup.TryGetValue(k.Id, out var f) ? k with { Photos = f } : k)
                    .ToList();
            }
        }

        return Result.Success(new PagedResult<ProductReviewDto>(kayitlar, toplam, request.Page, request.PageSize));
    }
}
