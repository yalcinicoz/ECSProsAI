using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Queries.GetStoreFacets;
using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetChannelCategoryFacets;

public record GetChannelCategoryFacetsQuery(
    Guid ChannelCategoryId,
    // Stok görünürlüğü — kategori listesiyle facet sayıları tutarlı (2026-07-14).
    bool ShowOutOfStock = false,
    DateTime? OutOfStockSince = null,
    // 2026-07-17: seçim-duyarlı facet — aktif filtre/fiyat/arama listeyi daraltırken
    // her grubun seçenekleri "diğer grupların seçimleri uygulanmış" kümeden hesaplanır.
    List<Guid>? SelectedValueIds = null,
    decimal? PriceMin = null,
    decimal? PriceMax = null,
    string? Search = null,
    // 2026-08-15: kategori sanal facet grubu (bkz. GetStoreFacetsQuery.ProductCategoryMap)
    IReadOnlyDictionary<Guid, Guid>? ProductCategoryMap = null,
    List<Guid>? SelectedCategoryIds = null) : IRequest<Result<StoreFacetsDto>>;

public class GetChannelCategoryFacetsQueryHandler(
    IStorefrontDbContext sfDb,
    ICatalogDbContext catDb,
    IStockService stockService,
    IChannelPricingService pricingService,
    IInStockProductProvider inStock,
    ICacheService cache)
    : IRequestHandler<GetChannelCategoryFacetsQuery, Result<StoreFacetsDto>>
{
    public async Task<Result<StoreFacetsDto>> Handle(
        GetChannelCategoryFacetsQuery request, CancellationToken ct)
    {
        // v2 + stok paramları: stok filtresi eklendiğinden eski/ayar-farklı girdiler ayrışsın.
        // v3: kanal seçimi/durdurma (M2/M3) geçidi eklendi.
        // v5: filter/mixed/manual facet kümesi listeyle AYNI çözümleyiciden (önceden filter/
        //     mixed "tüm satışa açık ilk 2000 ürün"dü — filtrede kategoride olmayan
        //     beden/renk seçenekleri görünüyordu).
        // Seçim/fiyat/arama bağlamlı istekler cache'lenmez (kombinasyon uzayı geniş).
        var secimliMi = request.SelectedValueIds is { Count: > 0 }
            || request.PriceMin.HasValue || request.PriceMax.HasValue
            || !string.IsNullOrWhiteSpace(request.Search)
            || request.SelectedCategoryIds is { Count: > 0 };
        // v6: ürün seviyesi özellikler facet'e dahil oldu (2026-07-17)
        // v7: facet'e girecek tipler AttributeType.UseInFilter bayrağından seçilir (2026-07-17)
        // v8: tek seçenekli filtre grupları panelden düşürülür (2026-07-17)
        // v9: kategori sanal grubu sayımı (CategoryCounts) DTO'ya girdi (2026-08-15) — harita
        //     yokken cache'lenmez ki sayımsız girdi 10 dk kalmasın.
        // v10: renk modunda sayı birimi ÜRÜN→KART (2026-08-26 kullanıcı kararı — liste başlığı,
        //      filtre sayıları ve seçim sonrası başlık aynı birimde, birebir tutarlı).
        var cacheKey = $"channelcat:facets:v10:{request.ChannelCategoryId}:{request.ShowOutOfStock}:{request.OutOfStockSince:yyyyMMdd}";
        StoreFacetsDto? cached = null;
        if (!secimliMi)
        {
            try { cached = await cache.GetAsync<StoreFacetsDto>(cacheKey, ct); } catch { /* Redis erişilemezse taze hesapla */ }
        }
        if (cached is not null)
            return Result.Success(cached);

        var cat = await sfDb.ChannelCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChannelCategoryId, ct);

        if (cat is null)
            return Result.Failure<StoreFacetsDto>("Kanal kategorisi bulunamadı.");

        List<Guid> productIds;

        if (cat.ListingMode != "model")
        {
            // v10 (2026-08-26): renk modunda sayım listenin KART EVRENİ üzerinden — liste
            // başlığı (kart) ile filtre sayıları aynı birim ve seçme kuralı birebir aynı.
            // "Kategoride ara" da evren kurucusuna gider (liste arama semantiğiyle birebir).
            var evren = await Queries.GetChannelCategoryProducts.GetChannelCategoryProductsQueryHandler
                .KartEvreniKurAsync(
                    catDb, sfDb, stockService, pricingService, inStock,
                    cat, request.ChannelCategoryId, request.ShowOutOfStock, request.OutOfStockSince,
                    request.Search, null, ct);
            if (!evren.FallbackGerekli)
            {
                var dto = await KanalKategoriKartFacetleri.KurAsync(
                    catDb, evren.VisiblePairs, evren.ProductInfo,
                    request.SelectedValueIds, request.PriceMin, request.PriceMax,
                    request.ProductCategoryMap, request.SelectedCategoryIds, ct);
                if (!secimliMi && request.ProductCategoryMap is not null)
                {
                    try { await cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10), ct); } catch { /* best-effort */ }
                }
                return Result.Success(dto);
            }

            // Fallback (renk ekseni yok → 1 kart/ürün): kart=ürün olduğundan eski ürün-tabanlı
            // sayım zaten aynı birimdedir.
            productIds = await Queries.GetChannelCategoryProducts.GetChannelCategoryProductsQueryHandler
                .ResolveCategoryProductIds(
                    sfDb, catDb, stockService, pricingService, inStock,
                    cat, request.ChannelCategoryId, request.ShowOutOfStock, request.OutOfStockSince, ct);

            return await SecimliFacetleriKurVeCachele(productIds, request, secimliMi, cacheKey, ct);
        }

        {
            // Model modunda: vitrin ürünleri + fallback ilk ürünler
            var groups = await sfDb.ChannelCategoryGroups
                .AsNoTracking()
                .Where(g => g.ChannelCategoryId == request.ChannelCategoryId)
                .Select(g => new { g.ShowcaseProductId, g.ProductGroupId })
                .ToListAsync(ct);

            var showcaseIds = groups
                .Where(g => g.ShowcaseProductId.HasValue)
                .Select(g => g.ShowcaseProductId!.Value)
                .ToList();

            var groupsNeedingFallback = groups
                .Where(g => !g.ShowcaseProductId.HasValue)
                .Select(g => g.ProductGroupId)
                .ToList();

            var fallbackIds = groupsNeedingFallback.Count > 0
                ? (await catDb.Products
                    .AsNoTracking()
                    .Where(p => groupsNeedingFallback.Contains(p.ProductGroupId) && p.IsSaleOpen)
                    .OrderBy(p => p.ProductGroupId).ThenBy(p => p.Id)
                    .Select(p => new { p.Id, p.ProductGroupId })
                    .ToListAsync(ct))
                    .GroupBy(p => p.ProductGroupId)
                    .Select(g => g.First().Id)
                    .ToList()
                : new List<Guid>();

            productIds = showcaseIds.Concat(fallbackIds).Distinct().ToList();
        }

        // Stok görünürlüğü: stoğu biteni (kanal açık VE CreatedAt>=eşik değilse) facet'ten çıkar.
        if (productIds.Count > 0)
        {
            var inStockIds = await inStock.GetInStockProductIdsAsync(ct);
            var showOos = request.ShowOutOfStock;
            var oosSince = request.OutOfStockSince;
            var gorunur = (await catDb.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id)
                            && (inStockIds.Contains(p.Id) || (showOos && (oosSince == null || p.CreatedAt >= oosSince))))
                .Select(p => p.Id).ToListAsync(ct)).ToHashSet();
            productIds = productIds.Where(gorunur.Contains).ToList();
        }

        // Kanal seçimi/durdurma (M2/M3): kanaldan çıkarılan/durdurulan ürünü facet'ten de çıkar
        // (liste ile tutarlı sayım). Opt-out: satır yok/seçili → dahil.
        if (productIds.Count > 0)
        {
            var simdi = DateTime.UtcNow;
            var kanalDisi = (await sfDb.ChannelProducts.AsNoTracking()
                .Where(cp => cp.FirmPlatformId == cat.FirmPlatformId && productIds.Contains(cp.ProductId)
                          && (!cp.IsActive
                              || (cp.SaleStoppedFrom != null && cp.SaleStoppedFrom <= simdi
                                  && (cp.SaleStoppedUntil == null || cp.SaleStoppedUntil >= simdi))))
                .Select(cp => cp.ProductId).ToListAsync(ct)).ToHashSet();
            if (kanalDisi.Count > 0)
                productIds = productIds.Where(id => !kanalDisi.Contains(id)).ToList();
        }

        return await SecimliFacetleriKurVeCachele(productIds, request, secimliMi, cacheKey, ct);
    }

    /// <summary>Seçim yoksa düz facet (10 dk cache); seçim/fiyat/arama varsa "kategoride ara"
    /// daraltması + seçim-duyarlı hesap (kategori listesi grupları AYNI varyantta arar).</summary>
    private async Task<Result<StoreFacetsDto>> SecimliFacetleriKurVeCachele(
        List<Guid> productIds, GetChannelCategoryFacetsQuery request, bool secimliMi,
        string cacheKey, CancellationToken ct)
    {
        if (!secimliMi)
        {
            var sonuc = await GetStoreFacetsQueryHandler.BuildFacets(catDb, productIds, ct);
            if (sonuc.IsSuccess)
            {
                // Tek seçenekli grup panele konmaz (2026-07-17 kuralı; seçim yok → istisnasız)
                var dto = GetStoreFacetsQueryHandler.TekSecenekliGruplariAyikla(sonuc.Value!);
                if (request.ProductCategoryMap is not null)
                {
                    dto = dto with { CategoryCounts = GetStoreFacetsQueryHandler.KategoriSayimi(productIds, request.ProductCategoryMap) };
                    try { await cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10), ct); } catch { /* best-effort */ }
                }
                sonuc = Result.Success(dto);
            }
            return sonuc;
        }

        // "Kategoride ara" — liste sorgusuyla aynı eşleşme (kod veya Türkçe ad)
        if (!string.IsNullOrWhiteSpace(request.Search) && productIds.Count > 0)
        {
            var arama = request.Search.Trim().ToLower();
            productIds = await catDb.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id)
                         && (p.Code.ToLower().Contains(arama)
                          || PgJsonFunctions.JsonText(p.NameI18n, "tr")!.ToLower().Contains(arama)))
                .Select(p => p.Id)
                .ToListAsync(ct);
        }

        return await GetStoreFacetsQueryHandler.BuildFacetsWithSelections(
            catDb, productIds, request.SelectedValueIds, request.PriceMin, request.PriceMax,
            sameVariant: true, ct, request.ProductCategoryMap, request.SelectedCategoryIds);
    }
}
