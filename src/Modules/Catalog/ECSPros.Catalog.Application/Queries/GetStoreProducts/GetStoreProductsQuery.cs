using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetStoreProducts;

// Shared listing DTOs (used by both this query and Storefront module)
public record ProductListingColorDto(
    Guid ValueId,
    Dictionary<string, string> NameI18n,
    string? HexCode);

public record ProductListingAttrDto(
    string TypeCode,
    Dictionary<string, string> TypeNameI18n,
    Guid ValueId,
    Dictionary<string, string> ValueNameI18n,
    int SortOrder = 0);

public record GetStoreProductsQuery(
    Guid FirmPlatformId,
    string? Search = null,
    int Page = 1,
    int PageSize = 24) : IRequest<Result<PagedResult<StoreProductDto>>>;

public record StoreProductDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? ShortDescriptionI18n,
    string? MainImageUrl,
    decimal MinPrice,
    decimal? CompareAtPrice,
    bool IsActive,
    List<ProductListingColorDto> Colors,
    List<ProductListingAttrDto> Attrs);

public class GetStoreProductsQueryHandler(ICatalogDbContext db, IChannelPricingService pricingService)
    : IRequestHandler<GetStoreProductsQuery, Result<PagedResult<StoreProductDto>>>
{
    public async Task<Result<PagedResult<StoreProductDto>>> Handle(GetStoreProductsQuery request, CancellationToken ct)
    {
        var cdnBase = await CdnHelper.BuildListUrlAsync(db, ct);
        var channelPrices = await pricingService.GetActiveVariantPricesAsync(request.FirmPlatformId, ct);
        var q = db.Products
            .AsNoTracking()
            .Include(p => p.Variants)
            .Where(p => p.IsActive && db.ProductImages.Any(img => img.ProductId == p.Id));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // Kod VEYA Türkçe ad eşleşmesi (NameI18n->>'tr') — B2 canlı arama önerileri
            // metinle arar; salt kod araması müşteri için sonuç üretmiyordu.
            var search = request.Search.Trim().ToLower();
            q = q.Where(p => p.Code.ToLower().Contains(search)
                          || PgJsonFunctions.JsonText(p.NameI18n, "tr")!.ToLower().Contains(search));
        }

        var total = await q.CountAsync(ct);
        var products = await q
            .OrderBy(p => p.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var productIds = products.Select(p => p.Id).ToList();

        // Main images
        var firstImages = await db.ProductImages
            .AsNoTracking()
            .Where(img => productIds.Contains(img.ProductId))
            .GroupBy(img => img.ProductId)
            .Select(g => new { ProductId = g.Key, FileName = g.OrderBy(i => i.SortOrder).First().FileName })
            .ToDictionaryAsync(x => x.ProductId, x => x.FileName, ct);

        // Variant → product mapping
        var variantData = await db.ProductVariants
            .AsNoTracking()
            .Where(v => productIds.Contains(v.ProductId) && v.IsActive)
            .Select(v => new { v.Id, v.ProductId })
            .ToListAsync(ct);

        var variantIds      = variantData.Select(v => v.Id).ToList();
        var variantToProduct = variantData.ToDictionary(v => v.Id, v => v.ProductId);

        // Color attributes (AttributeType.Code == "filtre_rengi")
        var colorAttrs = await db.ProductVariantAttributes
            .AsNoTracking()
            .Where(va => variantIds.Contains(va.VariantId) && va.AttributeType.Code == "filtre_rengi")
            .Select(va => new {
                va.VariantId,
                va.AttributeValueId,
                NameI18n = va.AttributeValue.NameI18n,
                HexCode  = va.AttributeValue.HexCode
            })
            .ToListAsync(ct);

        // Other attributes
        var otherAttrs = await db.ProductVariantAttributes
            .AsNoTracking()
            .Where(va => variantIds.Contains(va.VariantId) && va.AttributeType.Code != "filtre_rengi")
            .Select(va => new {
                va.VariantId,
                TypeCode     = va.AttributeType.Code,
                TypeNameI18n = va.AttributeType.NameI18n,
                va.AttributeValueId,
                ValueNameI18n = va.AttributeValue.NameI18n,
                SortOrder    = va.AttributeValue.SortOrder
            })
            .ToListAsync(ct);

        // Group by product
        var colorsByProduct = new Dictionary<Guid, List<ProductListingColorDto>>();
        var attrsByProduct  = new Dictionary<Guid, List<ProductListingAttrDto>>();

        foreach (var ca in colorAttrs)
        {
            if (!variantToProduct.TryGetValue(ca.VariantId, out var pid)) continue;
            if (!colorsByProduct.TryGetValue(pid, out var list))
                colorsByProduct[pid] = list = new();
            if (list.All(c => c.ValueId != ca.AttributeValueId))
                list.Add(new(ca.AttributeValueId, ca.NameI18n, ca.HexCode));
        }

        foreach (var oa in otherAttrs)
        {
            if (!variantToProduct.TryGetValue(oa.VariantId, out var pid)) continue;
            if (!attrsByProduct.TryGetValue(pid, out var list))
                attrsByProduct[pid] = list = new();
            if (list.All(a => a.TypeCode != oa.TypeCode || a.ValueId != oa.AttributeValueId))
                list.Add(new(oa.TypeCode, oa.TypeNameI18n, oa.AttributeValueId, oa.ValueNameI18n, oa.SortOrder));
        }

        // Build DTOs
        var items = products.Select(p =>
        {
            var activeVariants = p.Variants.Where(v => v.IsActive).ToList();
            var platformPrices = activeVariants
                .Where(v => channelPrices.ContainsKey(v.Id))
                .Select(v => channelPrices[v.Id].Price ?? 0)
                .Where(price => price > 0)
                .ToList();

            var variantMin = activeVariants.Any() ? activeVariants.Min(v => v.BasePrice) : 0;
            var minPrice   = platformPrices.Any() ? platformPrices.Min() : variantMin > 0 ? variantMin : p.BasePrice;
            var mainImage  = firstImages.TryGetValue(p.Id, out var fn) ? cdnBase + fn : null;

            return new StoreProductDto(
                p.Id, p.Code, p.NameI18n, p.ShortDescriptionI18n,
                mainImage, minPrice, null, p.IsActive,
                colorsByProduct.GetValueOrDefault(p.Id) ?? new(),
                attrsByProduct.GetValueOrDefault(p.Id) ?? new());
        }).ToList();

        return Result.Success(new PagedResult<StoreProductDto>(items, total, request.Page, request.PageSize));
    }
}
