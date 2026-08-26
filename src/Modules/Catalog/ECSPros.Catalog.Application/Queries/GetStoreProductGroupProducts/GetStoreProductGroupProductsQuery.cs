using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetStoreProductGroupProducts;

public record GetStoreProductGroupProductsQuery(
    Guid ProductGroupId,
    Guid FirmPlatformId,
    int Page = 1,
    int PageSize = 24,
    // Stok görünürlüğü (2026-07-14) — liste ile tutarlı.
    bool ShowOutOfStock = false,
    DateTime? OutOfStockSince = null) : IRequest<Result<StoreProductGroupProductsDto>>;

public record StoreProductGroupProductsDto(
    Guid ProductGroupId,
    Dictionary<string, string> GroupNameI18n,
    PagedResult<StoreGroupProductItemDto> Products);

public record StoreGroupProductItemDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? ShortDescriptionI18n,
    string? MainImageUrl,
    decimal MinPrice,
    decimal? CompareAtPrice,
    bool IsActive);

public class GetStoreProductGroupProductsQueryHandler(
    ICatalogDbContext db, IChannelPricingService pricingService, IInStockProductProvider inStock,
    IChannelProductFlagService flagService)
    : IRequestHandler<GetStoreProductGroupProductsQuery, Result<StoreProductGroupProductsDto>>
{
    public async Task<Result<StoreProductGroupProductsDto>> Handle(
        GetStoreProductGroupProductsQuery request, CancellationToken ct)
    {
        // Kanal seçimi/durdurma (M2/M3): kanaldan çıkarılan/durdurulan ürünler gruptan da düşer.
        var kanalDisi = await flagService.GetChannelExcludedProductIdsAsync(request.FirmPlatformId, ct);
        var group = await db.ProductGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == request.ProductGroupId && g.IsActive, ct);

        if (group is null)
            return Result.Failure<StoreProductGroupProductsDto>("Ürün grubu bulunamadı.");

        // Alt gruplar dahil tüm grup ID'lerini topla
        var allGroupIds = await CollectGroupIds(db, request.ProductGroupId, ct);

        var q = db.Products
            .AsNoTracking()
            .Where(p => allGroupIds.Contains(p.ProductGroupId) && p.IsSaleOpen
                     && db.ProductImages.Any(img => img.ProductId == p.Id)
                     && !kanalDisi.Contains(p.Id));

        // Stok görünürlüğü: stoğu biteni (kanal açık VE CreatedAt>=eşik değilse) gizle.
        var inStockIds = await inStock.GetInStockProductIdsAsync(ct);
        var showOos = request.ShowOutOfStock;
        var oosSince = request.OutOfStockSince;
        q = q.Where(p => inStockIds.Contains(p.Id) || (showOos && (oosSince == null || p.CreatedAt >= oosSince)));

        var total = await q.CountAsync(ct);

        var products = await q
            .Include(p => p.Variants).ThenInclude(v => v.Images)
            .OrderBy(p => p.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        // Faz 2 P0: yalnız bu grubun ürün varyantlarının kanal fiyatları.
        var channelPrices = await pricingService.GetActiveVariantPricesAsync(
            request.FirmPlatformId, products.SelectMany(p => p.Variants).Select(v => v.Id).ToList(), ct);

        var items = products.Select(p =>
        {
            var activeVariants = p.Variants.Where(v => v.IsActive).ToList();
            var platformPrices = activeVariants
                .Where(v => channelPrices.ContainsKey(v.Id))
                .Select(v => channelPrices[v.Id].Price ?? 0)
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

            return new StoreGroupProductItemDto(
                p.Id, p.Code, p.NameI18n, p.ShortDescriptionI18n,
                mainImage, minPrice, null, p.IsSaleOpen);
        }).ToList();

        var paged = new PagedResult<StoreGroupProductItemDto>(items, total, request.Page, request.PageSize);
        return Result.Success(new StoreProductGroupProductsDto(group.Id, group.NameI18n, paged));
    }

    private static Task<List<Guid>> CollectGroupIds(
        ICatalogDbContext db, Guid rootId, CancellationToken ct)
        => Task.FromResult(new List<Guid> { rootId });
}
