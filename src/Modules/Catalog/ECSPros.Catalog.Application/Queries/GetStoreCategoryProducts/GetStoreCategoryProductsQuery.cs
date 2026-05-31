using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
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

public class GetStoreCategoryProductsQueryHandler(ICatalogDbContext db)
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

        // Filtre kurallarını çöz: preset varsa onu kullan, yoksa doğrudan FilterRules
        var effectiveFilterDef = ResolveFilterDef(category);

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
            productQuery = BuildFilterQuery(db, effectiveFilterDef).Where(p => p.IsActive);
        }
        else // mixed
        {
            var filteredIds = await BuildFilterQuery(db, effectiveFilterDef)
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

    /// <summary>
    /// Preset varsa preset'in FilterDef'ini döner.
    /// Kategori kendi FilterRules'ına da sahipse bunlar merge edilir (kategori override eder).
    /// </summary>
    private static Dictionary<string, object>? ResolveFilterDef(Domain.Entities.Category category)
    {
        if (category.FilterPreset is null)
            return category.FilterRules;

        if (category.FilterRules is null or { Count: 0 })
            return category.FilterPreset.FilterDef;

        // Merge: preset taban, category.FilterRules override
        var merged = new Dictionary<string, object>(category.FilterPreset.FilterDef);
        foreach (var kv in category.FilterRules)
            merged[kv.Key] = kv.Value;
        return merged;
    }

    private static IQueryable<Domain.Entities.Product> BuildFilterQuery(
        ICatalogDbContext db, Dictionary<string, object>? rawRules)
    {
        var q = db.Products.AsNoTracking();
        var rules = CategoryFilterRules.From(rawRules);
        if (rules is null) return q;

        if (rules.ProductGroupIds is { Count: > 0 })
            q = q.Where(p => rules.ProductGroupIds.Contains(p.ProductGroupId));

        if (rules.PriceMin.HasValue)
            q = q.Where(p => p.BasePrice >= rules.PriceMin.Value);

        if (rules.PriceMax.HasValue)
            q = q.Where(p => p.BasePrice <= rules.PriceMax.Value);

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
