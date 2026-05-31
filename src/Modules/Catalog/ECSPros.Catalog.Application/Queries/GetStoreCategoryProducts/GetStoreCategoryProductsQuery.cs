using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetStoreCategoryProducts;

public record GetStoreCategoryProductsQuery(
    Guid CategoryId,
    Guid FirmPlatformId,
    int Page = 1,
    int PageSize = 24) : IRequest<Result<StoreCategoryProductsDto>>;

public record StoreCategoryProductsDto(
    Guid CategoryId,
    Dictionary<string, string> CategoryNameI18n,
    string FillType,
    PagedResult<StoreProductListItemDto> Products);

public record StoreProductListItemDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? ShortDescriptionI18n,
    string? MainImageUrl,
    decimal MinPrice,
    decimal? CompareAtPrice,
    bool IsActive);

public class GetStoreCategoryProductsQueryHandler(ICatalogDbContext db, IStockService stockService)
    : IRequestHandler<GetStoreCategoryProductsQuery, Result<StoreCategoryProductsDto>>
{
    public async Task<Result<StoreCategoryProductsDto>> Handle(
        GetStoreCategoryProductsQuery request, CancellationToken ct)
    {
        var category = await db.Categories
            .AsNoTracking()
            .Include(c => c.FilterPreset)
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.IsActive, ct);

        if (category is null)
            return Result.Failure<StoreCategoryProductsDto>("Kategori bulunamadı.");

        var effectiveFilterDef = ResolveFilterDef(category);
        var rules = CategoryFilterRules.From(effectiveFilterDef);

        // Stok filtresi için önceden variant ID'lerini topla
        HashSet<Guid>? inStockVariantIds = null;
        if (rules?.HasStock.HasValue == true)
            inStockVariantIds = await stockService.GetInStockVariantIdsAsync(ct);

        IQueryable<Domain.Entities.Product> productQuery;

        if (category.FillType == "manual")
        {
            productQuery = db.CategoryProducts
                .Where(cp => cp.CategoryId == request.CategoryId)
                .OrderBy(cp => cp.SortOrder)
                .Select(cp => cp.Product)
                .Where(p => p.IsActive);
        }
        else if (category.FillType == "filter")
        {
            productQuery = BuildFilterQuery(db, rules, request.FirmPlatformId, inStockVariantIds)
                .Where(p => p.IsActive);
        }
        else // mixed
        {
            var filteredIds = await BuildFilterQuery(db, rules, request.FirmPlatformId, inStockVariantIds)
                .Select(p => p.Id).ToListAsync(ct);

            var pinnedIds = await db.CategoryProducts
                .Where(cp => cp.CategoryId == request.CategoryId && cp.IsPinned)
                .Select(cp => cp.ProductId)
                .ToListAsync(ct);

            var allIds = filteredIds.Union(pinnedIds).Distinct().ToList();
            productQuery = db.Products.Where(p => allIds.Contains(p.Id) && p.IsActive);
        }

        var total = await productQuery.CountAsync(ct);

        var products = await productQuery
            .Include(p => p.Variants).ThenInclude(v => v.Images)
            .Include(p => p.Variants).ThenInclude(v => v.FirmPlatformVariants)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var items = products.Select(p =>
        {
            var activeVariants = p.Variants.Where(v => v.IsActive).ToList();
            var platformPrices = activeVariants
                .SelectMany(v => v.FirmPlatformVariants
                    .Where(fpv => fpv.FirmPlatformId == request.FirmPlatformId && fpv.IsActive))
                .Select(fpv => fpv.Price ?? 0)
                .Where(price => price > 0)
                .ToList();

            var minPrice = platformPrices.Count > 0
                ? platformPrices.Min()
                : activeVariants.MinBy(v => v.BasePrice)?.BasePrice ?? 0;

            var mainImage = activeVariants
                .SelectMany(v => v.Images)
                .Where(i => i.IsMain)
                .OrderBy(i => i.SortOrder)
                .FirstOrDefault()?.ImageUrl;

            return new StoreProductListItemDto(
                p.Id, p.Code, p.NameI18n, p.ShortDescriptionI18n,
                mainImage, minPrice, null, p.IsActive);
        }).ToList();

        var paged = new PagedResult<StoreProductListItemDto>(items, total, request.Page, request.PageSize);
        return Result.Success(new StoreCategoryProductsDto(
            category.Id, category.NameI18n, category.FillType, paged));
    }

    private static Dictionary<string, object>? ResolveFilterDef(Domain.Entities.Category category)
    {
        if (category.FilterPreset is null)
            return category.FilterRules;
        if (category.FilterRules is null or { Count: 0 })
            return category.FilterPreset.FilterDef;
        var merged = new Dictionary<string, object>(category.FilterPreset.FilterDef);
        foreach (var kv in category.FilterRules)
            merged[kv.Key] = kv.Value;
        return merged;
    }

    /// <summary>Tüm filtre kurallarını uygular. isActive filtresi burada uygulanmaz (caller halleder).</summary>
    internal static IQueryable<Domain.Entities.Product> BuildFilterQuery(
        ICatalogDbContext db,
        CategoryFilterRules? rules,
        Guid? firmPlatformId = null,
        HashSet<Guid>? inStockVariantIds = null)
    {
        var q = db.Products.AsNoTracking();
        if (rules is null) return q;

        // Ürün grubu
        if (rules.ProductGroupIds is { Count: > 0 })
            q = q.Where(p => rules.ProductGroupIds.Contains(p.ProductGroupId));

        // Temel fiyat aralığı
        if (rules.PriceMin.HasValue)
            q = q.Where(p => p.BasePrice >= rules.PriceMin.Value);
        if (rules.PriceMax.HasValue)
            q = q.Where(p => p.BasePrice <= rules.PriceMax.Value);

        // Platform fiyatı (sadece firmPlatformId biliniyorsa)
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

        // KDV oranı
        if (rules.TaxRateMin.HasValue)
            q = q.Where(p => p.TaxRate >= rules.TaxRateMin.Value);
        if (rules.TaxRateMax.HasValue)
            q = q.Where(p => p.TaxRate <= rules.TaxRateMax.Value);

        // Tedarikçi
        if (rules.SupplierIds is { Count: > 0 })
            q = q.Where(p => p.SupplierId.HasValue && rules.SupplierIds.Contains(p.SupplierId.Value));

        // Kategori — ürün şu kategorilerde bulunmalı
        if (rules.CategoryIds is { Count: > 0 })
            q = q.Where(p => db.CategoryProducts
                .Any(cp => cp.ProductId == p.Id && rules.CategoryIds.Contains(cp.CategoryId)));

        // Durum
        if (rules.IsActive.HasValue)
            q = q.Where(p => p.IsActive == rules.IsActive.Value);

        // Stok durumu
        if (rules.HasStock.HasValue && inStockVariantIds is not null)
        {
            if (rules.HasStock.Value)
                q = q.Where(p => p.Variants.Any(v => inStockVariantIds.Contains(v.Id)));
            else
                q = q.Where(p => !p.Variants.Any(v => inStockVariantIds.Contains(v.Id)));
        }

        // Oluşturma tarihi
        if (rules.CreatedAfter.HasValue)
            q = q.Where(p => p.CreatedAt >= rules.CreatedAfter.Value);
        if (rules.CreatedBefore.HasValue)
            q = q.Where(p => p.CreatedAt <= rules.CreatedBefore.Value);

        // Özellik filtreleri
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
}
