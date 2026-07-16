using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetProductReviewSummary;

/// <summary>
/// H9: değerlendirmeler sayfası başlık istatistiği — onaylı yorumların ortalaması,
/// toplamı ve puan dağılımı (filtre chip'indeki adetler). Yorum sayısı = metinli olanlar
/// (tasarım "218 Değerlendirme · 140 Yorum" ayrımı).
/// </summary>
public record GetProductReviewSummaryQuery(Guid FirmPlatformId, string ProductCode)
    : IRequest<Result<ProductReviewSummaryDto>>;

public record ProductReviewSummaryDto(
    double Average,
    int TotalCount,
    int TextCount,
    Dictionary<int, int> RatingCounts,                    // 5→adet, 4→adet... (0 olanlar da dolu)
    Dictionary<string, int>? TopicCounts = null,          // İP-5: konu→adet (yalnız konusu olanlar)
    int PhotoReviewCount = 0);                            // İP-5: fotoğraflı yorum adedi

public class GetProductReviewSummaryQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetProductReviewSummaryQuery, Result<ProductReviewSummaryDto>>
{
    public async Task<Result<ProductReviewSummaryDto>> Handle(
        GetProductReviewSummaryQuery request, CancellationToken ct)
    {
        var gruplar = await db.ProductReviews.AsNoTracking()
            .Where(r => r.FirmPlatformId == request.FirmPlatformId
                        && r.ProductCode == request.ProductCode && r.Status == "approved")
            .GroupBy(r => r.Rating)
            .Select(g => new { Rating = g.Key, Adet = g.Count(), Metinli = g.Count(r => r.Text != null && r.Text != "") })
            .ToListAsync(ct);

        var dagilim = Enumerable.Range(1, 5).ToDictionary(p => p, _ => 0);
        foreach (var g in gruplar) dagilim[g.Rating] = g.Adet;

        var toplam = gruplar.Sum(g => g.Adet);
        var ortalama = toplam == 0 ? 0 : Math.Round(gruplar.Sum(g => (double)g.Rating * g.Adet) / toplam, 1);

        // İP-5: konu dağılımı + fotoğraflı yorum adedi (sol filtre chip'leri)
        var onayli = db.ProductReviews.AsNoTracking()
            .Where(r => r.FirmPlatformId == request.FirmPlatformId
                        && r.ProductCode == request.ProductCode && r.Status == "approved");
        var konular = await onayli
            .Where(r => r.Topic != null)
            .GroupBy(r => r.Topic!)
            .Select(g => new { Konu = g.Key, Adet = g.Count() })
            .ToDictionaryAsync(g => g.Konu, g => g.Adet, ct);
        var fotografli = await onayli
            .CountAsync(r => db.ProductReviewPhotos.Any(p => p.ReviewId == r.Id), ct);

        return Result.Success(new ProductReviewSummaryDto(
            ortalama, toplam, gruplar.Sum(g => g.Metinli), dagilim, konular, fotografli));
    }
}
