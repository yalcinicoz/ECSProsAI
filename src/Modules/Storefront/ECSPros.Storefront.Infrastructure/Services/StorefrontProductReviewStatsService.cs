using ECSPros.Shared.Contracts;
using ECSPros.Storefront.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Infrastructure.Services;

/// <summary>E7: IProductReviewStatsService implementasyonu (storefront.product_reviews,
/// yalnız approved).</summary>
public class StorefrontProductReviewStatsService(StorefrontDbContext db) : IProductReviewStatsService
{
    public async Task<Dictionary<string, ReviewStats>> GetStatsAsync(
        Guid firmPlatformId, IReadOnlyCollection<string> productCodes, CancellationToken ct = default)
    {
        if (productCodes.Count == 0) return new();
        var kodlar = productCodes.ToList();
        var gruplar = await db.ProductReviews.AsNoTracking()
            .Where(r => r.FirmPlatformId == firmPlatformId
                        && r.Status == "approved" && kodlar.Contains(r.ProductCode))
            .GroupBy(r => r.ProductCode)
            .Select(g => new { Kod = g.Key, Ortalama = g.Average(r => r.Rating), Sayi = g.Count() })
            .ToListAsync(ct);
        return gruplar.ToDictionary(g => g.Kod, g => new ReviewStats(Math.Round(g.Ortalama, 1), g.Sayi));
    }
}
