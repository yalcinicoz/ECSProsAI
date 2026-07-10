namespace ECSPros.Shared.Contracts;

/// <summary>
/// E7: ürün puan istatistikleri — Catalog listelemeleri/detayı, Storefront'a doğrudan
/// referans vermeden onaylı yorum ortalamasına erişir (IChannelProductFlagService deseni).
/// Yalnız approved yorumlar sayılır; yorumu olmayan kodlar sözlükte yer almaz.
/// </summary>
public interface IProductReviewStatsService
{
    Task<Dictionary<string, ReviewStats>> GetStatsAsync(
        Guid firmPlatformId, IReadOnlyCollection<string> productCodes, CancellationToken ct = default);
}

public record ReviewStats(double Average, int Count);
