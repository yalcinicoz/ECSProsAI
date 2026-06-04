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
        Guid? firmPlatformId = null,
        HashSet<Guid>? productIdsInStockRange = null)
    {
        var q = db.Products.AsNoTracking();
        if (rules is null) return q;

        if (rules.ProductGroupIds is { Count: > 0 })
            q = q.Where(p => rules.ProductGroupIds.Contains(p.ProductGroupId));

        if (rules.PriceMin.HasValue) q = q.Where(p => p.BasePrice >= rules.PriceMin.Value);
        if (rules.PriceMax.HasValue) q = q.Where(p => p.BasePrice <= rules.PriceMax.Value);

        if (firmPlatformId.HasValue && (rules.PlatformPriceMin.HasValue || rules.PlatformPriceMax.HasValue))
        {
            var pid = firmPlatformId.Value;
            if (rules.PlatformPriceMin.HasValue)
                q = q.Where(p => p.Variants.Any(v => v.FirmPlatformVariants
                    .Any(fpv => fpv.FirmPlatformId == pid && fpv.Price >= rules.PlatformPriceMin.Value)));
            if (rules.PlatformPriceMax.HasValue)
                q = q.Where(p => p.Variants.Any(v => v.FirmPlatformVariants
                    .Any(fpv => fpv.FirmPlatformId == pid && fpv.Price <= rules.PlatformPriceMax.Value)));
        }

        if (rules.TaxRateMin.HasValue) q = q.Where(p => p.TaxRate >= rules.TaxRateMin.Value);
        if (rules.TaxRateMax.HasValue) q = q.Where(p => p.TaxRate <= rules.TaxRateMax.Value);

        if (rules.SupplierIds is { Count: > 0 })
            q = q.Where(p => p.SupplierId.HasValue && rules.SupplierIds.Contains(p.SupplierId.Value));

        if (rules.IsActive.HasValue)
            q = q.Where(p => p.IsActive == rules.IsActive.Value);

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

        if (rules.ImageUpdatedAfterDays.HasValue)
        {
            var threshold = DateTime.UtcNow.AddDays(-rules.ImageUpdatedAfterDays.Value);
            q = q.Where(p => db.ProductImages.Any(img => img.ProductId == p.Id && img.UpdatedAt >= threshold));
        }
        else
        {
            if (rules.ImageUpdatedAfter.HasValue)
                q = q.Where(p => db.ProductImages.Any(img => img.ProductId == p.Id && img.UpdatedAt >= rules.ImageUpdatedAfter.Value));
            if (rules.ImageUpdatedBefore.HasValue)
                q = q.Where(p => db.ProductImages.Any(img => img.ProductId == p.Id && img.UpdatedAt <= rules.ImageUpdatedBefore.Value));
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
}
