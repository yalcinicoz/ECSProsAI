using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Promotion.Domain.Entities;
using ECSPros.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Services;

/// <summary>
/// F1: kampanya ürün kapsamını materyalize eder — kategori mekanizmasının (SyncChannelCategoryProducts)
/// kampanya karşılığı. FillType: all (materyalize edilmez, tüm ürünler) / manual (elle) / filter
/// (ProductFilterHelper) / mixed (filtre ∪ manuel). Dışlananlar çıkarılır. Kanal fiyatı ve stok
/// aralığı filtreleri kampanyanın FirmPlatformId'sine göre çözülür.
/// </summary>
public static class CampaignProductMaterializer
{
    public static async Task<int> SyncAsync(
        IPromotionDbContext db, ICatalogDbContext catDb,
        IChannelPricingService pricing, IStockService stock,
        Campaign campaign, List<Guid> manualProductIds, List<Guid> excludedProductIds,
        CancellationToken ct)
    {
        // Mevcut materyalize satırları temizle (idempotent — yeniden hesaplanır).
        var existing = await db.CampaignProducts
            .Where(p => p.CampaignId == campaign.Id).ToListAsync(ct);
        db.CampaignProducts.RemoveRange(existing);

        if (campaign.FillType == "all")
            return 0; // tüm ürünler — kapsam materyalize edilmez

        var ids = new HashSet<Guid>();

        if (campaign.FillType is "manual" or "mixed")
            foreach (var id in manualProductIds) ids.Add(id);

        if (campaign.FillType is "filter" or "mixed")
        {
            var rules = CategoryFilterRules.From(campaign.FilterDef);
            if (rules is not null)
            {
                HashSet<Guid>? stockIds = null;
                if (rules.StockMin.HasValue || rules.StockMax.HasValue)
                    stockIds = await ProductFilterHelper.ResolveStockRangeProductIds(
                        catDb, stock, rules.StockMin, rules.StockMax, ct);

                HashSet<Guid>? platformPriceIds = null;
                if (rules.PlatformPriceMin.HasValue || rules.PlatformPriceMax.HasValue)
                    platformPriceIds = await ProductFilterHelper.ResolvePlatformPriceRangeProductIds(
                        catDb, pricing, campaign.FirmPlatformId, rules.PlatformPriceMin, rules.PlatformPriceMax, ct);

                var filtered = await ProductFilterHelper
                    .BuildFilterQuery(catDb, rules, platformPriceIds, stockIds)
                    .Select(p => p.Id)
                    .ToListAsync(ct);
                foreach (var id in filtered) ids.Add(id);
            }
        }

        foreach (var ex in excludedProductIds) ids.Remove(ex);

        foreach (var id in ids)
            db.CampaignProducts.Add(new CampaignProduct
            {
                CampaignId = campaign.Id,
                ProductId = id,
                AddedType = campaign.FillType == "manual" ? "manual" : "filter",
            });

        return ids.Count;
    }
}
