using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Contracts.Channels;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Services.ChannelScoping;

/// <summary>
/// F1: kanal kapsam filtresini (CategoryFilterRules + HasChannelPrice/MinStock) ürün Id kümesine çözer.
/// Sync, önizleme ve (ileride) olay tabanlı tetikler aynı çözücüyü kullanır.
/// </summary>
public sealed class ChannelScopeResolver(
    ICatalogDbContext catDb,
    IStockService stockService,
    IChannelPricingService pricingService,
    IChannelStockCalculator stockCalculator,
    IChannelCapabilityResolver capabilityResolver)
{
    /// <summary>F5 K6: kanalın yeteneklerine göre izinli ürün kaynakları (own her zaman izinli).</summary>
    public static List<string> AllowedSourceTypes(ChannelCapabilities caps)
    {
        var allowed = new List<string> { "own" };
        if (caps.ThirdPartySellerProducts) allowed.Add("seller");
        if (caps.ExternalSupplyProducts) allowed.Add("supply");
        return allowed;
    }

    /// <summary>Filtreden geçen ürün Id'leri (görselli + silinmemiş katalog tabanı; IsSaleOpen kapsam şartı DEĞİLDİR — katman 3 sebebi).</summary>
    public async Task<List<Guid>> ResolveAsync(Guid firmPlatformId, CategoryFilterRules? rules, CancellationToken ct)
    {
        rules ??= new CategoryFilterRules();

        // F5 K6: yetenek bazlı kaynak zorlaması — filtre kuralı ne derse desin, kanalın kapalı olduğu
        // kaynak (seller/supply) kapsama giremez. rules.SourceTypes varsa kesişim alınır.
        var caps = await capabilityResolver.GetAsync(firmPlatformId, ct);
        var allowed = AllowedSourceTypes(caps);
        rules.SourceTypes = rules.SourceTypes is { Count: > 0 }
            ? rules.SourceTypes.Intersect(allowed, StringComparer.OrdinalIgnoreCase).ToList()
            : allowed;
        if (rules.SourceTypes.Count == 0) return new List<Guid>();

        HashSet<Guid>? stockRangeIds = null;
        if (rules.StockMin.HasValue || rules.StockMax.HasValue)
            stockRangeIds = await ProductFilterHelper.ResolveStockRangeProductIds(catDb, stockService, rules.StockMin, rules.StockMax, ct);

        HashSet<Guid>? platformPriceIds = null;
        if (rules.PlatformPriceMin.HasValue || rules.PlatformPriceMax.HasValue)
            platformPriceIds = await ProductFilterHelper.ResolvePlatformPriceRangeProductIds(
                catDb, pricingService, firmPlatformId, rules.PlatformPriceMin, rules.PlatformPriceMax, ct);

        HashSet<Guid>? channelPricedIds = null;
        if (rules.HasChannelPrice == true)
        {
            var prices = await pricingService.GetActiveVariantPricesAsync(firmPlatformId, ct);
            var pricedVariantIds = prices.Where(kv => kv.Value.Price is > 0).Select(kv => kv.Key).ToList();
            channelPricedIds = pricedVariantIds.Count == 0 ? new HashSet<Guid>() :
                (await catDb.ProductVariants.AsNoTracking()
                    .Where(v => pricedVariantIds.Contains(v.Id))
                    .Select(v => v.ProductId).Distinct().ToListAsync(ct)).ToHashSet();
        }

        HashSet<Guid>? channelStockIds = null;
        if (rules.MinStock is > 0)
            channelStockIds = await stockCalculator.GetProductIdsWithChannelStockAsync(rules.MinStock.Value, ct);

        return await ProductFilterHelper
            .BuildFilterQuery(catDb, rules, platformPriceIds, stockRangeIds, channelPricedIds, channelStockIds)
            .Where(p => catDb.ProductImages.Any(img => img.ProductId == p.Id))
            .Select(p => p.Id)
            .ToListAsync(ct);
    }
}
