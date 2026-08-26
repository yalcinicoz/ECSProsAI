using ECSPros.Shared.Contracts;
using ECSPros.Storefront.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Infrastructure.Services;

public class StorefrontChannelPricingService(StorefrontDbContext db) : IChannelPricingService
{
    public async Task<Dictionary<Guid, ChannelVariantPrice>> GetActiveVariantPricesAsync(
        Guid firmPlatformId, CancellationToken ct = default)
    {
        var rows = await db.ChannelVariants
            .AsNoTracking()
            .Where(cv => cv.FirmPlatformId == firmPlatformId && cv.IsActive)
            .Select(cv => new { cv.VariantId, cv.Price, cv.CompareAtPrice })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.VariantId, r => new ChannelVariantPrice(r.Price, r.CompareAtPrice));
    }

    public async Task<Dictionary<Guid, ChannelVariantPrice>> GetActiveVariantPricesAsync(
        Guid firmPlatformId, IReadOnlyCollection<Guid> variantIds, CancellationToken ct = default)
    {
        if (variantIds.Count == 0) return new Dictionary<Guid, ChannelVariantPrice>();
        var rows = await db.ChannelVariants
            .AsNoTracking()
            .Where(cv => cv.FirmPlatformId == firmPlatformId && cv.IsActive && variantIds.Contains(cv.VariantId))
            .Select(cv => new { cv.VariantId, cv.Price, cv.CompareAtPrice })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.VariantId, r => new ChannelVariantPrice(r.Price, r.CompareAtPrice));
    }
}
