using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Helpers;

public static class ProductFilterHelper
{
    public static IQueryable<Product> BuildFilterQuery(
        ICatalogDbContext db,
        CategoryFilterRules? rules,
        HashSet<Guid>? platformPriceProductIds = null,
        HashSet<Guid>? productIdsInStockRange = null,
        HashSet<Guid>? channelPricedProductIds = null,
        HashSet<Guid>? channelStockProductIds = null)
    {
        var q = db.Products.AsNoTracking();
        if (rules is null) return q;

        // F1 kanal kapsamı: kanal fiyatı var / kanal stok eşiği — Storefront/Inventory tarafında çözülen Id kümeleri.
        if (rules.HasChannelPrice == true)
            q = channelPricedProductIds is not null ? q.Where(p => channelPricedProductIds.Contains(p.Id)) : q.Where(p => false);
        if (rules.MinStock is > 0)
            q = channelStockProductIds is not null ? q.Where(p => channelStockProductIds.Contains(p.Id)) : q.Where(p => false);

        if (rules.ProductGroupIds is { Count: > 0 })
            q = q.Where(p => rules.ProductGroupIds.Contains(p.ProductGroupId));

        if (rules.ExcludedProductGroupIds is { Count: > 0 })
            q = q.Where(p => !rules.ExcludedProductGroupIds.Contains(p.ProductGroupId));

        if (rules.SourceTypes is { Count: > 0 })
            q = q.Where(p => rules.SourceTypes.Contains(p.SourceType));

        if (rules.PriceMin.HasValue) q = q.Where(p => p.BasePrice >= rules.PriceMin.Value);
        if (rules.PriceMax.HasValue) q = q.Where(p => p.BasePrice <= rules.PriceMax.Value);

        // Platform (kanal) fiyatı Storefront şemasında (ChannelVariant) tutulur — bu değer
        // çağıran taraf (Storefront) tarafından IChannelPricingService ile önceden çözülüp verilir.
        if (rules.PlatformPriceMin.HasValue || rules.PlatformPriceMax.HasValue)
        {
            q = platformPriceProductIds is not null
                ? q.Where(p => platformPriceProductIds.Contains(p.Id))
                : q.Where(p => false);
        }

        if (rules.TaxRateMin.HasValue) q = q.Where(p => p.TaxRate >= rules.TaxRateMin.Value);
        if (rules.TaxRateMax.HasValue) q = q.Where(p => p.TaxRate <= rules.TaxRateMax.Value);

        if (rules.SupplierIds is { Count: > 0 })
            q = q.Where(p => p.SupplierId.HasValue && rules.SupplierIds.Contains(p.SupplierId.Value));

        // Kural alanı adı (IsActive) kontratı korumak için sabit; anlamı artık global satış
        // anahtarı (Product.IsSaleOpen). Kanal kategori filtre kurallarında 0 saklı kullanım var.
        if (rules.IsActive.HasValue)
            q = q.Where(p => p.IsSaleOpen == rules.IsActive.Value);

        if (productIdsInStockRange is not null)
            q = q.Where(p => productIdsInStockRange.Contains(p.Id));

        if (rules.CreatedAfterDays.HasValue)
        {
            var threshold = DateTime.UtcNow.AddDays(-rules.CreatedAfterDays.Value);
            q = q.Where(p => p.CreatedAt >= threshold);
        }
        else
        {
            if (rules.CreatedAfter.HasValue)  q = q.Where(p => p.CreatedAt >= rules.CreatedAfter.Value);
            if (rules.CreatedBefore.HasValue) q = q.Where(p => p.CreatedAt <= rules.CreatedBefore.Value);
        }

        // Görsel hiç güncellenmemişse UpdatedAt NULL kalır — yükleme tarihi (CreatedAt) esas alınır.
        if (rules.ImageUpdatedAfterDays.HasValue)
        {
            var threshold = DateTime.UtcNow.AddDays(-rules.ImageUpdatedAfterDays.Value);
            q = q.Where(p => db.ProductImages.Any(img => img.ProductId == p.Id && (img.UpdatedAt ?? img.CreatedAt) >= threshold));
        }
        else
        {
            if (rules.ImageUpdatedAfter.HasValue)
                q = q.Where(p => db.ProductImages.Any(img => img.ProductId == p.Id && (img.UpdatedAt ?? img.CreatedAt) >= rules.ImageUpdatedAfter.Value));
            if (rules.ImageUpdatedBefore.HasValue)
                q = q.Where(p => db.ProductImages.Any(img => img.ProductId == p.Id && (img.UpdatedAt ?? img.CreatedAt) <= rules.ImageUpdatedBefore.Value));
        }

        if (rules.Tags is { Count: > 0 })
            q = q.Where(p => p.Tags.Any(t => rules.Tags.Contains(t)));

        if (rules.AttributeFilters is { Count: > 0 })
        {
            foreach (var af in rules.AttributeFilters)
            {
                var typeId = af.AttributeTypeId;
                var valueIds = af.ValueIds;
                q = q.Where(p => p.Attributes.Any(a =>
                    a.AttributeTypeId == typeId && valueIds.Contains(a.AttributeValueId!.Value)));
            }
        }

        return q;
    }

    public static async Task<HashSet<Guid>> ResolveStockRangeProductIds(
        ICatalogDbContext db, IStockService stockService,
        int? stockMin, int? stockMax, CancellationToken ct)
    {
        var variantStocks = await stockService.GetVariantAvailableStocksAsync(ct);
        var variantIds = variantStocks.Keys.ToList();
        var variantProductMap = await db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => new { v.Id, v.ProductId })
            .ToDictionaryAsync(v => v.Id, v => v.ProductId, ct);

        var productStocks = variantStocks
            .Where(kv => variantProductMap.ContainsKey(kv.Key))
            .GroupBy(kv => variantProductMap[kv.Key])
            .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));

        return productStocks
            .Where(kv =>
                (stockMin == null || kv.Value >= stockMin) &&
                (stockMax == null || kv.Value <= stockMax))
            .Select(kv => kv.Key)
            .ToHashSet();
    }

    /// <summary>
    /// Bir satış kanalındaki (FirmPlatform) fiyat override'larına göre eşleşen ürün ID'lerini döner.
    /// Min ve max bağımsız olarak değerlendirilir (min'i sağlayan varyant ile max'ı sağlayan varyant aynı olmak zorunda değildir),
    /// tıpkı eski FirmPlatformVariant tabanlı sorgunun davrandığı gibi.
    /// </summary>
    public static async Task<HashSet<Guid>> ResolvePlatformPriceRangeProductIds(
        ICatalogDbContext db, IChannelPricingService pricingService,
        Guid firmPlatformId, decimal? priceMin, decimal? priceMax, CancellationToken ct)
    {
        var prices = await pricingService.GetActiveVariantPricesAsync(firmPlatformId, ct);
        var variantIds = prices.Keys.ToList();
        var variantProductMap = await db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => new { v.Id, v.ProductId })
            .ToDictionaryAsync(v => v.Id, v => v.ProductId, ct);

        HashSet<Guid>? minSet = priceMin.HasValue
            ? prices.Where(kv => kv.Value.Price >= priceMin.Value && variantProductMap.ContainsKey(kv.Key))
                .Select(kv => variantProductMap[kv.Key]).ToHashSet()
            : null;

        HashSet<Guid>? maxSet = priceMax.HasValue
            ? prices.Where(kv => kv.Value.Price <= priceMax.Value && variantProductMap.ContainsKey(kv.Key))
                .Select(kv => variantProductMap[kv.Key]).ToHashSet()
            : null;

        if (minSet is null) return maxSet ?? new HashSet<Guid>();
        if (maxSet is null) return minSet;
        return minSet.Intersect(maxSet).ToHashSet();
    }
}
