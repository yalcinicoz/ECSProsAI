using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetStoreCategoryProducts;

public record GetStoreCategoryProductsQuery(
    Guid CategoryId,
    int Page = 1,
    int PageSize = 24) : IRequest<Result<StoreCategoryProductsDto>>;

public record StoreCategoryProductsDto(
    Guid CategoryId,
    Dictionary<string, string> CategoryNameI18n,
    PagedResult<StoreProductListItemDto> Products);

public record StoreProductListItemDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? ShortDescriptionI18n,
    string? MainImageUrl,
    decimal MinPrice,
    bool IsActive);

public class GetStoreCategoryProductsQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetStoreCategoryProductsQuery, Result<StoreCategoryProductsDto>>
{
    public async Task<Result<StoreCategoryProductsDto>> Handle(
        GetStoreCategoryProductsQuery request, CancellationToken ct)
    {
        var category = await db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.IsActive, ct);

        if (category is null)
            return Result.Failure<StoreCategoryProductsDto>("Kategori bulunamadı.");

        var productQuery = db.CategoryProducts
            .Where(cp => cp.CategoryId == request.CategoryId)
            .OrderBy(cp => cp.SortOrder)
            .Select(cp => cp.Product)
            .Where(p => p.IsActive);

        var total = await productQuery.CountAsync(ct);

        var products = await productQuery
            .Include(p => p.Variants).ThenInclude(v => v.Images)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var items = products.Select(p =>
        {
            var activeVariants = p.Variants.Where(v => v.IsActive).ToList();
            var minPrice = activeVariants.MinBy(v => v.BasePrice)?.BasePrice ?? 0;
            var mainImage = activeVariants
                .SelectMany(v => v.Images)
                .Where(i => i.IsMain)
                .OrderBy(i => i.SortOrder)
                .FirstOrDefault()?.ImageUrl;

            return new StoreProductListItemDto(
                p.Id, p.Code, p.NameI18n, p.ShortDescriptionI18n, mainImage, minPrice, p.IsActive);
        }).ToList();

        var paged = new PagedResult<StoreProductListItemDto>(items, total, request.Page, request.PageSize);
        return Result.Success(new StoreCategoryProductsDto(category.Id, category.NameI18n, paged));
    }

    /// <summary>Filtre kurallarını ürün sorgusuna uygular. ChannelCategory handler'ları da kullanır.</summary>
    public static IQueryable<Domain.Entities.Product> BuildFilterQuery(
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
