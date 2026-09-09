using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Storefront.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Infrastructure.Services;

public class StorefrontChannelPricingService(StorefrontDbContext db, ICatalogDbContext catDb) : IChannelPricingService
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

    /// <summary>Ürünün aktif varyantlarındaki en yüksek kanal çizili fiyatı (liste kartıyla aynı kural).</summary>
    public async Task<Dictionary<Guid, decimal>> GetProductCompareAtPricesAsync(
        Guid firmPlatformId, IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
    {
        if (productIds.Count == 0) return new Dictionary<Guid, decimal>();

        var varyantlar = await catDb.ProductVariants.AsNoTracking()
            .Where(v => productIds.Contains(v.ProductId) && v.IsActive)
            .Select(v => new { v.Id, v.ProductId })
            .ToListAsync(ct);
        if (varyantlar.Count == 0) return new Dictionary<Guid, decimal>();

        var ids = varyantlar.Select(v => v.Id).ToList();
        var cizililer = await db.ChannelVariants.AsNoTracking()
            .Where(cv => cv.FirmPlatformId == firmPlatformId && cv.IsActive
                      && cv.CompareAtPrice != null && cv.CompareAtPrice > 0 && ids.Contains(cv.VariantId))
            .Select(cv => new { cv.VariantId, Ca = cv.CompareAtPrice!.Value })
            .ToListAsync(ct);
        if (cizililer.Count == 0) return new Dictionary<Guid, decimal>();

        var urunByVariant = varyantlar.ToDictionary(v => v.Id, v => v.ProductId);
        return cizililer
            .GroupBy(c => urunByVariant[c.VariantId])
            .ToDictionary(g => g.Key, g => g.Max(x => x.Ca));
    }
}
