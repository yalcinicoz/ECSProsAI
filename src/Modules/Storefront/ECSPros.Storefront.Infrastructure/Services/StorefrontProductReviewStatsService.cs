using ECSPros.Shared.Contracts;
using ECSPros.Storefront.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Infrastructure.Services;

/// <summary>
/// E7: IProductReviewStatsService implementasyonu — çok kaynaklı puan özeti.
/// Kendi-site yorumlarını (storefront.product_reviews, yalnız approved) ve dış kanal
/// özetlerini (storefront.product_rating_sources) platformun görünüm ayarına
/// (product_review_display_settings.AggregateChannels) göre birleştirir. Ayar yoksa
/// tüm kaynaklar toplamaya katılır. Ortalama, yorum sayısıyla ağırlıklı birleştirilir.
/// </summary>
public class StorefrontProductReviewStatsService(StorefrontDbContext db) : IProductReviewStatsService
{
    public async Task<Dictionary<string, ReviewStats>> GetStatsAsync(
        Guid firmPlatformId, IReadOnlyCollection<string> productCodes, CancellationToken ct)
    {
        if (productCodes.Count == 0) return new();
        var kodlar = productCodes.Distinct().ToList();

        var ayar = await db.ProductReviewDisplaySettings.AsNoTracking()
            .Where(s => s.FirmPlatformId == firmPlatformId)
            .Select(s => s.AggregateChannels)
            .FirstOrDefaultAsync(ct);
        var dahilKanallar = ayar ?? new List<string>(); // boş = tüm kanallar

        bool KanalDahil(string kanal) => dahilKanallar.Count == 0 || dahilKanallar.Contains(kanal);

        // (kod) → (ağırlıklı toplam, sayı)
        var toplamlar = new Dictionary<string, (double Sum, int Count)>();

        // 1) Kendi-site yorumları (own) — yalnız approved.
        if (KanalDahil("own"))
        {
            var own = await db.ProductReviews.AsNoTracking()
                .Where(r => r.FirmPlatformId == firmPlatformId
                            && r.Status == "approved" && kodlar.Contains(r.ProductCode))
                .GroupBy(r => r.ProductCode)
                .Select(g => new { Kod = g.Key, Sum = g.Sum(r => (double)r.Rating), Sayi = g.Count() })
                .ToListAsync(ct);
            foreach (var o in own)
                toplamlar[o.Kod] = (o.Sum, o.Sayi);
        }

        // 2) Dış kanal özetleri — görünüm ayarının izin verdiği kanallar.
        var dis = await db.ProductRatingSources.AsNoTracking()
            .Where(r => r.FirmPlatformId == firmPlatformId
                        && kodlar.Contains(r.ProductCode) && r.ReviewCount > 0)
            .Select(r => new { r.ProductCode, r.Channel, r.AverageRating, r.ReviewCount })
            .ToListAsync(ct);
        foreach (var r in dis)
        {
            if (!KanalDahil(r.Channel)) continue;
            (double Sum, int Count) mevcut = toplamlar.TryGetValue(r.ProductCode, out var t) ? t : (0.0, 0);
            toplamlar[r.ProductCode] = (mevcut.Sum + (double)r.AverageRating * r.ReviewCount, mevcut.Count + r.ReviewCount);
        }

        var sonuc = new Dictionary<string, ReviewStats>();
        foreach (var kv in toplamlar)
        {
            if (kv.Value.Count <= 0) continue;
            var ortalama = Math.Round(kv.Value.Sum / kv.Value.Count, 1);
            sonuc[kv.Key] = new ReviewStats(ortalama, kv.Value.Count);
        }
        return sonuc;
    }
}
