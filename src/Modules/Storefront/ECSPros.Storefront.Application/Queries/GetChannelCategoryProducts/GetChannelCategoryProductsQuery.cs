using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Queries.GetStoreProducts;
using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetChannelCategoryProducts;

file static class PlatformPriceFilter
{
    public static async Task<HashSet<Guid>?> ResolveAsync(
        ICatalogDbContext catDb, IChannelPricingService pricingService,
        Guid firmPlatformId, Catalog.Application.Helpers.CategoryFilterRules? rules, CancellationToken ct)
    {
        if (rules is null || (!rules.PlatformPriceMin.HasValue && !rules.PlatformPriceMax.HasValue))
            return null;

        return await ProductFilterHelper.ResolvePlatformPriceRangeProductIds(
            catDb, pricingService, firmPlatformId, rules.PlatformPriceMin, rules.PlatformPriceMax, ct);
    }
}

// B10: filtre/sıralama/arama parametreleri additive — mobil/SPA eski çağrıları etkilenmez.
// AttributeValueIds tip bazında gruplanır: grup içi OR, gruplar arası AND; eşleşme kartın
// (ürün×renk) kendi varyantları üzerinden değerlendirilir (kırmızı+M = aynı varyantta).
// Sort: "price_asc" | "price_desc" | "newest" | null. Search: kod veya Türkçe ad.
// Filtreli istekler Redis cache'ini atlar (anahtar kombinasyonu patlaması olmasın diye
// yalnız parametresiz varsayılan sayfalar cache'lenir).
public record GetChannelCategoryProductsQuery(
    Guid ChannelCategoryId,
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    List<Guid>? AttributeValueIds = null,
    decimal? PriceMin = null,
    decimal? PriceMax = null,
    string? Sort = null,
    // Kanal ayarı: stoğu biten ürünleri listede göster mi + hangi tarihten sonra açılanlar.
    bool ShowOutOfStock = false,
    DateTime? OutOfStockSince = null) : IRequest<Result<PagedResult<ChannelCategoryProductItemDto>>>
{
    public bool FiltreliMi =>
        !string.IsNullOrWhiteSpace(Search)
        || AttributeValueIds is { Count: > 0 }
        || PriceMin.HasValue || PriceMax.HasValue
        || !string.IsNullOrEmpty(Sort);
}

public record ChannelCategoryProductItemDto(
    Guid ProductId,
    string Code,
    Dictionary<string, string> NameI18n,
    string? MainImageUrl,
    decimal BasePrice,
    bool IsActive,
    int SortOrder,
    bool IsExcluded,
    Guid? ProductGroupId = null,
    List<ProductListingColorDto>? Colors = null,
    List<ProductListingAttrDto>? Attrs = null,
    Guid? SelectedColorValueId = null,
    Dictionary<string, string>? SelectedColorNameI18n = null,
    List<string>? GalleryUrls = null,               // B8: kart hover galerisi (seçili rengin ilk 4 görseli)
    List<ProductListingColorDto>? AxisColors = null, // B8: ürünün eksen (renk) kartları — tooltip linkleri buradan
    bool IsFeatured = false,                         // B11: öne çıkar penceresi içinde — kartta "Sponsorlu" rozeti
    double Rating = 0,                               // E7: onaylı yorum ortalaması (0 = yorum yok)
    int ReviewCount = 0,                             // E7: onaylı yorum sayısı
    string? Slug = null);                            // URL aktarımı 2b: kartın (seçili renk) gerçek URL slug'ı

public class GetChannelCategoryProductsQueryHandler(
    IStorefrontDbContext sfDb,
    ICatalogDbContext catDb,
    IStockService stockService,
    IChannelPricingService pricingService,
    IInStockProductProvider inStock,
    ICacheService cache)
    : IRequestHandler<GetChannelCategoryProductsQuery, Result<PagedResult<ChannelCategoryProductItemDto>>>
{
    // "filter"/"mixed" kategorilerde binlerce ürünü tarayan renk-grubu hesaplaması (bkz. HandleColorMode)
    // büyük kategorilerde (10K+ ürün) saniyeler sürüyor — aynı sayfa tüm ziyaretçiler için aynı sonucu
    // döndüğünden kısa süreli Redis cache ile tekrar hesaplama önleniyor. Cache erişimi try/catch ile
    // sarılı: Redis geçici olarak erişilemezse istek hata almak yerine DB'den taze hesaplanır.
    // v2: IsSaleOpen (global satış anahtarı) geçidi eklendi — eski binary'nin filtresiz
    // cache'lediği listeler geçersiz kılınsın diye anahtar sürümü artırıldı (TTL beklemeden).
    // v4: renk düzeyi stok görünürlüğü (stoğu biten renk kartı elenir) — eski v3 listeleri
    // stoksuz renkleri içerdiğinden sürüm artırıldı.
    // v5: kanal seçimi/durdurma (M2/M3) geçidi — kanaldan çıkarılan/durdurulan ürün elenir.
    private static string CacheKey(Guid categoryId, int page, int pageSize) =>
        $"channelcat:products:v5:{categoryId}:{page}:{pageSize}";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private async Task<PagedResult<ChannelCategoryProductItemDto>?> TryGetCacheAsync(string key, CancellationToken ct)
    {
        try { return await cache.GetAsync<PagedResult<ChannelCategoryProductItemDto>>(key, ct); }
        catch { return null; }
    }

    private async Task TrySetCacheAsync(string key, PagedResult<ChannelCategoryProductItemDto> value, CancellationToken ct)
    {
        try { await cache.SetAsync(key, value, CacheTtl, ct); }
        catch { /* cache best-effort — Redis erişilemezse sessizce yok say */ }
    }

    /// <summary>E7: puanlar (approved yorum ortalaması) dönüş öncesi eklenir — cache'lenen
    /// sonuçtan bağımsız, moderasyon sonrası taze kalır; DTO alanları additive.</summary>
    public async Task<Result<PagedResult<ChannelCategoryProductItemDto>>> Handle(
        GetChannelCategoryProductsQuery request, CancellationToken ct)
    {
        var sonuc = await HandleCore(request, ct);
        if (sonuc.IsFailure) return sonuc;
        var items = sonuc.Value!.Items.ToList();
        if (items.Count == 0) return sonuc;

        var platformId = await sfDb.ChannelCategories.AsNoTracking()
            .Where(c => c.Id == request.ChannelCategoryId)
            .Select(c => c.FirmPlatformId).FirstOrDefaultAsync(ct);
        var kodlar = items.Select(i => i.Code).Distinct().ToList();
        var puanlar = await sfDb.ProductReviews.AsNoTracking()
            .Where(r => r.FirmPlatformId == platformId && r.Status == "approved" && kodlar.Contains(r.ProductCode))
            .GroupBy(r => r.ProductCode)
            .Select(g => new { Kod = g.Key, Ortalama = g.Average(r => r.Rating), Sayi = g.Count() })
            .ToDictionaryAsync(g => g.Kod, ct);
        if (puanlar.Count == 0) return sonuc;

        for (var i = 0; i < items.Count; i++)
            if (puanlar.TryGetValue(items[i].Code, out var p))
                items[i] = items[i] with { Rating = Math.Round(p.Ortalama, 1), ReviewCount = p.Sayi };
        return Result.Success(new PagedResult<ChannelCategoryProductItemDto>(
            items, sonuc.Value.TotalCount, sonuc.Value.Page, sonuc.Value.PageSize));
    }

    private async Task<Result<PagedResult<ChannelCategoryProductItemDto>>> HandleCore(
        GetChannelCategoryProductsQuery request, CancellationToken ct)
    {
        var cacheKey = CacheKey(request.ChannelCategoryId, request.Page, request.PageSize);
        if (!request.FiltreliMi)
        {
            var cached = await TryGetCacheAsync(cacheKey, ct);
            if (cached is not null)
                return Result.Success(cached);
        }

        var cat = await sfDb.ChannelCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChannelCategoryId, ct);

        if (cat is null)
            return Result.Failure<PagedResult<ChannelCategoryProductItemDto>>("Kanal kategorisi bulunamadı.");

        var cdnBase = await CdnHelper.BuildListUrlAsync(catDb, ct);

        // ── Model Modu ────────────────────────────────────────────────────────
        if (cat.ListingMode == "model")
        {
            var modelResult = await HandleModelMode(request, cat.Id, cdnBase, ct);
            if (modelResult.IsSuccess && !request.FiltreliMi) await TrySetCacheAsync(cacheKey, modelResult.Value, ct);
            return modelResult;
        }

        // ── Renk (Ana Varyant) Modu — "color", "product" ve diğer her değer ─
        // "model" dışındaki tüm kategoriler renk modu olarak çalışır.
        if (cat.ListingMode != "model")
        {
            var colorResult = await HandleColorMode(request, cat, cdnBase, ct);
            if (colorResult.IsSuccess && !request.FiltreliMi) await TrySetCacheAsync(cacheKey, colorResult.Value, ct);
            return colorResult;
        }

        // Bu satıra artık ulaşılamaz — backward compat için bırakıldı
        IQueryable<Catalog.Domain.Entities.Product> productQuery;

        if (cat.FillType == "manual")
        {
            var manualIds = await sfDb.ChannelCategoryProducts
                .Where(p => p.ChannelCategoryId == request.ChannelCategoryId && !p.IsExcluded)
                .OrderBy(p => p.SortOrder)
                .Select(p => p.ProductId)
                .ToListAsync(ct);

            productQuery = catDb.Products
                .AsNoTracking()
                .Where(p => manualIds.Contains(p.Id));
        }
        else if (cat.FillType == "filter")
        {
            var rules = CategoryFilterRules.From(cat.FilterDef);
            HashSet<Guid>? stockRange = null;
            if (rules?.StockMin.HasValue == true || rules?.StockMax.HasValue == true)
                stockRange = await ProductFilterHelper
                    .ResolveStockRangeProductIds(catDb, stockService, rules!.StockMin, rules.StockMax, ct);
            var platformPriceIds = await PlatformPriceFilter.ResolveAsync(catDb, pricingService, cat.FirmPlatformId, rules, ct);

            var excludedIds = await sfDb.ChannelCategoryProducts
                .Where(p => p.ChannelCategoryId == request.ChannelCategoryId && p.IsExcluded)
                .Select(p => p.ProductId)
                .ToListAsync(ct);

            productQuery = ProductFilterHelper
                .BuildFilterQuery(catDb, rules, platformPriceIds, stockRange)
                .Where(p => !excludedIds.Contains(p.Id));
        }
        else // mixed
        {
            var rules = CategoryFilterRules.From(cat.FilterDef);
            HashSet<Guid>? stockRange = null;
            if (rules?.StockMin.HasValue == true || rules?.StockMax.HasValue == true)
                stockRange = await ProductFilterHelper
                    .ResolveStockRangeProductIds(catDb, stockService, rules!.StockMin, rules.StockMax, ct);
            var platformPriceIds = await PlatformPriceFilter.ResolveAsync(catDb, pricingService, cat.FirmPlatformId, rules, ct);

            var excludedIds = await sfDb.ChannelCategoryProducts
                .Where(p => p.ChannelCategoryId == request.ChannelCategoryId && p.IsExcluded)
                .Select(p => p.ProductId).ToListAsync(ct);

            var filteredIds = await ProductFilterHelper
                .BuildFilterQuery(catDb, rules, platformPriceIds, stockRange)
                .Select(p => p.Id).ToListAsync(ct);

            var pinnedIds = await sfDb.ChannelCategoryProducts
                .Where(p => p.ChannelCategoryId == request.ChannelCategoryId && !p.IsExcluded)
                .Select(p => p.ProductId).ToListAsync(ct);

            var allIds = filteredIds.Union(pinnedIds).Except(excludedIds).ToList();
            productQuery = catDb.Products.AsNoTracking().Where(p => allIds.Contains(p.Id));
        }

        // NOT: Bu blok artık ulaşılamaz (yukarıda ListingMode != "model" için HandleColorMode
        // döner). Satış geçidi asıl yol olan ResolveCategoryProductIds'de uygulanır.
        productQuery = productQuery.Where(p => p.IsSaleOpen
            && catDb.ProductImages.Any(img => img.ProductId == p.Id));

        var total = await productQuery.CountAsync(ct);

        var pagedProducts = await productQuery
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var productIds = pagedProducts.Select(p => p.Id).ToList();
        var firstImages = await catDb.ProductImages
            .AsNoTracking()
            .Where(img => productIds.Contains(img.ProductId))
            .GroupBy(img => img.ProductId)
            .Select(g => new { ProductId = g.Key, FileName = g.OrderBy(i => i.SortOrder).First().FileName })
            .ToDictionaryAsync(x => x.ProductId, x => x.FileName, ct);

        var manualProductMap = await sfDb.ChannelCategoryProducts
            .Where(p => p.ChannelCategoryId == request.ChannelCategoryId)
            .ToDictionaryAsync(p => p.ProductId, p => p, ct);

        var (colorMap, attrMap) = await LoadVariantAttrs(catDb, productIds, ct);

        var items = pagedProducts.Select(p =>
        {
            var mainImage = firstImages.TryGetValue(p.Id, out var fn) ? cdnBase + fn : null;
            manualProductMap.TryGetValue(p.Id, out var manualEntry);
            return new ChannelCategoryProductItemDto(
                p.Id, p.Code, p.NameI18n, mainImage, p.BasePrice > 0 ? p.BasePrice : 0, p.IsSaleOpen,
                manualEntry?.SortOrder ?? 0,
                manualEntry?.IsExcluded ?? false,
                Colors: colorMap.GetValueOrDefault(p.Id),
                Attrs: attrMap.GetValueOrDefault(p.Id));
        }).ToList();

        return Result.Success(new PagedResult<ChannelCategoryProductItemDto>(
            items, total, request.Page, request.PageSize));
    }

    // ── Renk Modu: Her (Ürün × PrimaryAxis değeri) = 1 kart ─────────────────
    // ProductGroupAttribute.IsPrimaryAxis=true olan attribute type → o eksenin
    // distinct değerleri kadar kart oluşturulur.
    // Örnek: 6 renk × 4 beden = 24 varyant → 6 kart
    private async Task<Result<PagedResult<ChannelCategoryProductItemDto>>> HandleColorMode(
        GetChannelCategoryProductsQuery request,
        ChannelCategory cat,
        string cdnBase,
        CancellationToken ct)
    {
        // 1. Kategorideki tüm ürün ID'leri
        var allProductIds = await ResolveCategoryProductIds(cat, request.ChannelCategoryId,
            request.ShowOutOfStock, request.OutOfStockSince, ct);

        // B10: "kategoride ara" — kategori kapsamı içinde kod veya Türkçe ad eşleşmesi
        // (GetStoreProducts aramasıyla aynı semantik). Fallback moduna da daralmış liste gider.
        if (!string.IsNullOrWhiteSpace(request.Search) && allProductIds.Count > 0)
        {
            var arama = request.Search.Trim().ToLower();
            allProductIds = await catDb.Products.AsNoTracking()
                .Where(p => allProductIds.Contains(p.Id)
                         && (p.Code.ToLower().Contains(arama)
                          || PgJsonFunctions.JsonText(p.NameI18n, "tr")!.ToLower().Contains(arama)))
                .Select(p => p.Id)
                .ToListAsync(ct);
        }

        if (allProductIds.Count == 0)
            return Result.Success(new PagedResult<ChannelCategoryProductItemDto>(
                [], 0, request.Page, request.PageSize));

        // 2. Ürün bilgi haritası (grup + B10 fiyat/tarih — filtre ve sıralama için)
        var productInfo = await catDb.Products.AsNoTracking()
            .Where(p => allProductIds.Contains(p.Id))
            .Select(p => new { p.Id, p.ProductGroupId, p.BasePrice, p.CreatedAt })
            .ToDictionaryAsync(p => p.Id, ct);
        var productGroupMap = productInfo.ToDictionary(kv => kv.Key, kv => kv.Value.ProductGroupId);

        var groupIds = productGroupMap.Values.Distinct().ToList();

        // 3. Her grup için primary axis AttributeTypeId
        var primaryAxisByGroup = await catDb.ProductGroupAttributes.AsNoTracking()
            .Where(ga => groupIds.Contains(ga.ProductGroupId) && ga.IsPrimaryAxis)
            .Select(ga => new { ga.ProductGroupId, ga.AttributeTypeId })
            .ToDictionaryAsync(ga => ga.ProductGroupId, ga => ga.AttributeTypeId, ct);

        // 4. Aktif varyantlar (fiyat dahil)
        var allVariants = await catDb.ProductVariants.AsNoTracking()
            .Where(v => allProductIds.Contains(v.ProductId) && v.IsActive)
            .Select(v => new { v.Id, v.ProductId, v.BasePrice })
            .ToListAsync(ct);

        var variantIds = allVariants.Select(v => v.Id).ToList();
        var variantToProduct = allVariants.ToDictionary(v => v.Id, v => v.ProductId);
        var variantPrice = allVariants.ToDictionary(v => v.Id, v => v.BasePrice);

        // 5. Tüm varyant attribute değerleri (primary axis tespiti için)
        // Sadece PRIMARY AXIS tipindeki satırlar çekiliyor (ör. "beden" atlanıyor) — büyük
        // kategorilerde (10K+ ürün) her varyantın TÜM attribute'larını (Name/Hex join'i dahil)
        // çekmek saniyeler sürüyordu (bkz. PROGRESS.md 2026-07-06). Renk adı/hex burada
        // kullanılmıyor, sadece sayfalanmış sonuç için adım 11'den önce ayrıca çekiliyor.
        var axisTypeIds = primaryAxisByGroup.Values.Distinct().ToList();
        var allVariantAttrs = variantIds.Count > 0 && axisTypeIds.Count > 0
            ? await catDb.ProductVariantAttributes.AsNoTracking()
                .Where(va => variantIds.Contains(va.VariantId) && axisTypeIds.Contains(va.AttributeTypeId))
                .Select(va => new { va.VariantId, va.AttributeTypeId, va.AttributeValueId })
                .ToListAsync(ct)
            : [];

        // 6. Primary axis değerlerine göre distinct (ProductId, ValueId) çiftleri
        //    Her renk için O renge ait TÜM varyant ID'leri saklanır (görsel arama için)
        var colorPairs = allVariantAttrs
            .Where(a =>
            {
                if (!variantToProduct.TryGetValue(a.VariantId, out var pid)) return false;
                if (!productGroupMap.TryGetValue(pid, out var gid)) return false;
                return primaryAxisByGroup.TryGetValue(gid, out var axisTypeId) && a.AttributeTypeId == axisTypeId;
            })
            .GroupBy(a => (ProductId: variantToProduct[a.VariantId], a.AttributeValueId))
            .Select(g => new
            {
                g.Key.ProductId,
                ColorValueId  = g.Key.AttributeValueId,
                VariantIds    = g.Select(x => x.VariantId).Distinct().ToList(),
                // Rengin en düşük varyant fiyatı (fiyatlanmamış varyantlar 0 döner, onları atla)
                Price = g.Select(x => variantPrice.GetValueOrDefault(x.VariantId))
                         .Where(p => p > 0).DefaultIfEmpty(0).Min()
            })
            .OrderBy(x => x.ProductId).ThenBy(x => x.ColorValueId)
            .ToList();

        // Primary axis tanımlı değilse fallback: 1 kart/ürün
        if (colorPairs.Count == 0)
            return await BuildFallbackProductItems(allProductIds, request, cdnBase, ct);

        // 7. Görseli olan renkleri filtrele — görselsiz renk listede çıkmaz
        var allPairVariantIds = colorPairs.SelectMany(p => p.VariantIds).Distinct().ToList();
        var variantIdsWithImage = allPairVariantIds.Count > 0
            ? (await catDb.ProductImages.AsNoTracking()
                .Where(img => img.VariantId != null
                           && allPairVariantIds.Contains(img.VariantId.Value)
                           && img.Status == Catalog.Domain.Entities.ProductImageStatus.Active)
                .Select(img => img.VariantId!.Value)
                .Distinct()
                .ToListAsync(ct))
                .ToHashSet()
            : new HashSet<Guid>();

        var visiblePairs = colorPairs
            .Where(p => p.VariantIds.Any(vid => variantIdsWithImage.Contains(vid)))
            .ToList();

        // Görseli olan renk yoksa ürün düzeyindeki fallback ile devam et
        if (visiblePairs.Count == 0)
            return await BuildFallbackProductItems(allProductIds, request, cdnBase, ct);

        // ── Stok görünürlüğü (RENK düzeyi, 2026-07-14): tekstilde ürünler renk varyantıyla
        // listelenir. Ürün düzeyi filtre (ResolveCategoryProductIds) yalnız TÜM renkleri stoksuz
        // ürünü eler; burada aynı ürünün stoğu biten RENGİ (o rengin tüm varyantları online
        // satılabilir stok kümesinde yoksa) ayrıca elenir. Stoklu renkler her zaman görünür.
        // Kanal "stoğu bitenleri göster" açıksa (+ tarih eşiği) stoksuz renk de gösterilir.
        {
            var inStockVariants = await inStock.GetInStockVariantIdsAsync(ct);
            visiblePairs = visiblePairs.Where(pair =>
            {
                if (pair.VariantIds.Any(inStockVariants.Contains)) return true;   // renkte stok var
                if (!request.ShowOutOfStock) return false;                        // stoksuz renk gizli
                if (request.OutOfStockSince is null) return true;
                return productInfo.TryGetValue(pair.ProductId, out var pi)
                    && pi.CreatedAt >= request.OutOfStockSince.Value;
            }).ToList();

            if (visiblePairs.Count == 0)
                return Result.Success(new PagedResult<ChannelCategoryProductItemDto>(
                    [], 0, request.Page, request.PageSize));
        }

        // ── B10: özellik filtresi — kart (ürün×renk) kendi varyantları üzerinden;
        // grup içi OR, gruplar arası AND aynı varyantta sağlanmalı (kırmızı+M birlikte).
        if (request.AttributeValueIds is { Count: > 0 } seciliDegerler)
        {
            var degerTipleri = await catDb.AttributeValues.AsNoTracking()
                .Where(v => seciliDegerler.Contains(v.Id))
                .Select(v => new { v.Id, v.AttributeTypeId })
                .ToListAsync(ct);
            var tipGruplari = degerTipleri
                .GroupBy(v => v.AttributeTypeId)
                .Select(g => g.Select(x => x.Id).ToHashSet())
                .ToList();

            var adayProductIds = visiblePairs.Select(p => p.ProductId).Distinct().ToList();
            var eslesenSatirlar = await catDb.ProductVariantAttributes.AsNoTracking()
                .Where(va => seciliDegerler.Contains(va.AttributeValueId)
                          && adayProductIds.Contains(va.Variant.ProductId)
                          && va.Variant.IsActive)
                .Select(va => new { va.VariantId, va.AttributeValueId })
                .ToListAsync(ct);
            var degerlerByVariant = eslesenSatirlar
                .GroupBy(r => r.VariantId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.AttributeValueId).ToHashSet());

            visiblePairs = visiblePairs
                .Where(pair => pair.VariantIds.Any(vid =>
                    degerlerByVariant.TryGetValue(vid, out var sahip)
                    && tipGruplari.All(sahip.Overlaps)))
                .ToList();
        }

        // ── B10: fiyat aralığı — kartın etkin fiyatı (renk min varyant fiyatı, 0 ise ürün fiyatı)
        if (request.PriceMin.HasValue || request.PriceMax.HasValue)
        {
            visiblePairs = visiblePairs.Where(pair =>
            {
                var fiyat = pair.Price > 0 ? pair.Price
                    : productInfo.TryGetValue(pair.ProductId, out var pi) ? pi.BasePrice : 0;
                return (!request.PriceMin.HasValue || fiyat >= request.PriceMin.Value)
                    && (!request.PriceMax.HasValue || fiyat <= request.PriceMax.Value);
            }).ToList();
        }

        // ── B10: sıralama — varsayılan mevcut sıra (ProductId, ColorValueId)
        visiblePairs = request.Sort switch
        {
            "price_asc" => visiblePairs
                .OrderBy(pair => pair.Price > 0 ? pair.Price
                    : productInfo.TryGetValue(pair.ProductId, out var pi) ? pi.BasePrice : 0)
                .ToList(),
            "price_desc" => visiblePairs
                .OrderByDescending(pair => pair.Price > 0 ? pair.Price
                    : productInfo.TryGetValue(pair.ProductId, out var pi) ? pi.BasePrice : 0)
                .ToList(),
            "newest" => visiblePairs
                .OrderByDescending(pair => productInfo.TryGetValue(pair.ProductId, out var pi)
                    ? pi.CreatedAt : default)
                .ToList(),
            _ => visiblePairs
        };

        if (visiblePairs.Count == 0)
            return Result.Success(new PagedResult<ChannelCategoryProductItemDto>(
                [], 0, request.Page, request.PageSize));

        // ── B11: öne çıkanlar (platform başına az sayıda satır — tam liste çekilir).
        // Varsayılan sırada öne alınır; kullanıcının açık sıralama tercihi bozulmaz,
        // rozet bayrağı her sıralamada verilir.
        var simdi = DateTime.UtcNow;
        var oneCikanlar = (await sfDb.ChannelProducts.AsNoTracking()
            .Where(cp => cp.FirmPlatformId == cat.FirmPlatformId
                      && cp.FeaturedFrom != null && cp.FeaturedFrom <= simdi
                      && (cp.FeaturedUntil == null || cp.FeaturedUntil >= simdi))
            .Select(cp => cp.ProductId)
            .ToListAsync(ct)).ToHashSet();

        if (oneCikanlar.Count > 0 && string.IsNullOrEmpty(request.Sort))
            visiblePairs = visiblePairs
                .OrderByDescending(pair => oneCikanlar.Contains(pair.ProductId))
                .ToList(); // OrderBy kararlı — grup içi mevcut sıra korunur

        // 8. Sayfalama
        var total = visiblePairs.Count;
        var pagedPairs = visiblePairs
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var pagedProductIds = pagedPairs.Select(p => p.ProductId).Distinct().ToList();

        // Rengin tüm varyantlarını görsel aramasına dahil et. B8: renk tooltip'i sayfadaki
        // ürünlerin TÜM renklerinin görselini ister — o ürünlerin diğer renk çiftleri de dahil.
        var pagedProductPairs = colorPairs.Where(p => pagedProductIds.Contains(p.ProductId)).ToList();
        var allColorVariantIds = pagedProductPairs.SelectMany(p => p.VariantIds).Distinct().ToList();

        // URL aktarımı 2b: bu platformdaki gerçek slug'lar — (product×renk) kartlarının doğrudan
        // slug linki bassın (301 hop'u olmadan). Slug rengin temsilci (legacy ana) varyantındadır.
        var slugByVariant = allColorVariantIds.Count > 0
            ? await sfDb.ChannelVariants.AsNoTracking()
                .Where(cv => cv.FirmPlatformId == cat.FirmPlatformId && cv.Slug != null
                          && allColorVariantIds.Contains(cv.VariantId))
                .Select(cv => new { cv.VariantId, cv.Slug })
                .ToDictionaryAsync(x => x.VariantId, x => x.Slug!, ct)
            : new Dictionary<Guid, string>();
        string? PairSlug(List<Guid> variantIds) =>
            variantIds.Select(v => slugByVariant.GetValueOrDefault(v)).FirstOrDefault(s => s != null);

        // 8. Ürün bilgileri
        var productMap = await catDb.Products.AsNoTracking()
            .Where(p => pagedProductIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        // 9. Varyant bazlı görseller: catalog_product_images.VariantId IS NOT NULL
        //    Admin paneli görselleri bu şekilde renk/varyanta bağlar.
        //    B8: varyant başına tek görsel değil tam liste — hover galerisi (renk havuzunun
        //    ilk 4 görseli) ve tooltip renk görselleri buradan kurulur.
        var variantImageRows = allColorVariantIds.Count > 0
            ? await catDb.ProductImages.AsNoTracking()
                .Where(img => img.VariantId != null
                           && allColorVariantIds.Contains(img.VariantId.Value)
                           && img.Status == Catalog.Domain.Entities.ProductImageStatus.Active)
                .Select(img => new { VariantId = img.VariantId!.Value, img.FileName, img.SortOrder })
                .ToListAsync(ct)
            : [];

        var imagesByVariant = variantImageRows
            .GroupBy(i => i.VariantId)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.SortOrder).ToList());

        // (ProductId, ColorValueId) → rengin dosya adına göre tekilleşmiş görsel havuzu
        var imagesByPair = pagedProductPairs.ToDictionary(
            pair => (pair.ProductId, pair.ColorValueId),
            pair => pair.VariantIds
                .Where(imagesByVariant.ContainsKey)
                .SelectMany(vid => imagesByVariant[vid])
                .OrderBy(i => i.SortOrder)
                .GroupBy(i => i.FileName).Select(g => g.First().FileName)
                .ToList());

        // Ürün düzeyindeki görseller (VariantId IS NULL) — fallback
        var productImageMap = await catDb.ProductImages.AsNoTracking()
            .Where(img => pagedProductIds.Contains(img.ProductId)
                       && img.VariantId == null
                       && img.Status == Catalog.Domain.Entities.ProductImageStatus.Active)
            .GroupBy(img => img.ProductId)
            .Select(g => new { g.Key, Fn = g.OrderBy(i => i.SortOrder).First().FileName })
            .ToDictionaryAsync(x => x.Key, x => x.Fn, ct);

        // 10. Swatch satırı için her ürünün tüm renkleri
        var (allColorMap, _) = await LoadVariantAttrs(catDb, pagedProductIds, ct);

        // Renk adları — B8: sadece sayfadaki kartların değil, o ürünlerin TÜM eksen
        // renklerinin adı gerekir (renk tooltip'i diğer renk kartlarına link verir).
        var pagedColorValueIds = pagedProductPairs.Select(p => p.ColorValueId).Distinct().ToList();
        var colorValueNameMap = pagedColorValueIds.Count > 0
            ? await catDb.AttributeValues.AsNoTracking()
                .Where(v => pagedColorValueIds.Contains(v.Id))
                .Select(v => new { v.Id, v.NameI18n, v.HexCode })
                .ToDictionaryAsync(v => v.Id, v => (v.NameI18n, v.HexCode), ct)
            : new Dictionary<Guid, (Dictionary<string, string>, string?)>();

        // B8: kartın "diğer renk seçenekleri" — ürünün eksen (renk) çiftleri, kendi
        // görselleriyle. Detay linki ?color={eksenDeğerId} (detay tarafı eksen değerini
        // filtre_rengi bucket'ına çözer).
        var axisColorsByProduct = pagedProductPairs
            .GroupBy(p => p.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(pair => new ProductListingColorDto(
                        pair.ColorValueId,
                        colorValueNameMap.TryGetValue(pair.ColorValueId, out var ad) ? ad.Item1 : new(),
                        colorValueNameMap.TryGetValue(pair.ColorValueId, out var hexAd) ? hexAd.Item2 : null,
                        imagesByPair.TryGetValue((pair.ProductId, pair.ColorValueId), out var imgs)
                        && imgs.Count > 0 ? cdnBase + imgs[0] : null,
                        PairSlug(pair.VariantIds)))   // 2b: rengin gerçek URL slug'ı
                    .ToList());

        // 11. DTO oluştur
        var items = pagedPairs
            .Where(pair => productMap.ContainsKey(pair.ProductId))
            .Select(pair =>
            {
                var product = productMap[pair.ProductId];

                // Rengin görsel havuzu: ilk görsel kart görseli, ilk 4'ü hover galerisi.
                // Havuz boşsa ürün görseline düş (galeri verilmez).
                imagesByPair.TryGetValue((pair.ProductId, pair.ColorValueId), out var renkGorselleri);
                string? imageUrl = null;
                List<string>? galleryUrls = null;
                if (renkGorselleri is { Count: > 0 })
                {
                    imageUrl = cdnBase + renkGorselleri[0];
                    galleryUrls = renkGorselleri.Take(4).Select(fn => cdnBase + fn).ToList();
                }
                else if (productImageMap.TryGetValue(pair.ProductId, out var pFn))
                    imageUrl = cdnBase + pFn;

                // Fiyat: varyant fiyatı önce, yoksa ürün fiyatı
                var price = pair.Price > 0 ? pair.Price : product.BasePrice;

                return new ChannelCategoryProductItemDto(
                    pair.ProductId, product.Code, product.NameI18n,
                    imageUrl, price,
                    product.IsSaleOpen, 0, false, null,
                    Colors: allColorMap.GetValueOrDefault(pair.ProductId),
                    SelectedColorValueId: pair.ColorValueId,
                    SelectedColorNameI18n: colorValueNameMap.TryGetValue(pair.ColorValueId, out var seciliAd)
                        ? seciliAd.Item1
                        : null,
                    GalleryUrls: galleryUrls,
                    AxisColors: axisColorsByProduct.GetValueOrDefault(pair.ProductId),
                    IsFeatured: oneCikanlar.Contains(pair.ProductId),
                    Slug: PairSlug(pair.VariantIds));   // 2b: kartın seçili renk slug'ı
            })
            .ToList();

        return Result.Success(new PagedResult<ChannelCategoryProductItemDto>(
            items, total, request.Page, request.PageSize));
    }

    // Renk tanımlaması olmayan ürünler için basit ürün listesi
    private async Task<Result<PagedResult<ChannelCategoryProductItemDto>>> BuildFallbackProductItems(
        List<Guid> productIds,
        GetChannelCategoryProductsQuery request,
        string cdnBase,
        CancellationToken ct)
    {
        var total = productIds.Count;
        var pagedIds = productIds
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var products = await catDb.Products.AsNoTracking()
            .Where(p => pagedIds.Contains(p.Id)).ToListAsync(ct);
        var imageMap = await catDb.ProductImages.AsNoTracking()
            .Where(i => pagedIds.Contains(i.ProductId))
            .GroupBy(i => i.ProductId)
            .Select(g => new { g.Key, fn = g.OrderBy(i => i.SortOrder).First().FileName })
            .ToDictionaryAsync(x => x.Key, x => x.fn, ct);
        var (colorMap, attrMap) = await LoadVariantAttrs(catDb, pagedIds, ct);

        var items = products.Select(p => new ChannelCategoryProductItemDto(
            p.Id, p.Code, p.NameI18n,
            imageMap.TryGetValue(p.Id, out var fn) ? cdnBase + fn : null,
            p.BasePrice > 0 ? p.BasePrice : 0, p.IsSaleOpen, 0, false, null,
            Colors: colorMap.GetValueOrDefault(p.Id),
            Attrs: attrMap.GetValueOrDefault(p.Id))).ToList();

        return Result.Success(new PagedResult<ChannelCategoryProductItemDto>(
            items, total, request.Page, request.PageSize));
    }

    // Kategorideki tüm ürün ID'lerini dolum tipine göre çözer
    private Task<List<Guid>> ResolveCategoryProductIds(
        ChannelCategory cat, Guid channelCategoryId, bool showOutOfStock, DateTime? outOfStockSince, CancellationToken ct)
        => ResolveCategoryProductIds(sfDb, catDb, stockService, pricingService, inStock,
            cat, channelCategoryId, showOutOfStock, outOfStockSince, ct);

    /// <summary>2026-07-17: liste ile facet'lerin AYNI ürün kümesini kullanması için dışa
    /// açıldı (GetChannelCategoryFacets buradan beslenir) — dolum tipi çözümü + satış
    /// anahtarı + kanal seçimi/durdurma + stok görünürlüğü geçitlerinin tek kaynağı.</summary>
    public static async Task<List<Guid>> ResolveCategoryProductIds(
        IStorefrontDbContext sfDb, ICatalogDbContext catDb, IStockService stockService,
        IChannelPricingService pricingService, IInStockProductProvider inStock,
        ChannelCategory cat, Guid channelCategoryId, bool showOutOfStock, DateTime? outOfStockSince, CancellationToken ct)
    {
        List<Guid> ids;
        if (cat.FillType == "manual")
        {
            ids = await sfDb.ChannelCategoryProducts
                .Where(p => p.ChannelCategoryId == channelCategoryId && !p.IsExcluded)
                .OrderBy(p => p.SortOrder)
                .Select(p => p.ProductId)
                .ToListAsync(ct);
        }
        else if (cat.FillType == "filter")
        {
            var rules = CategoryFilterRules.From(cat.FilterDef);
            HashSet<Guid>? stockRange = null;
            if (rules?.StockMin.HasValue == true || rules?.StockMax.HasValue == true)
                stockRange = await ProductFilterHelper
                    .ResolveStockRangeProductIds(catDb, stockService, rules!.StockMin, rules.StockMax, ct);
            var platformPriceIds = await PlatformPriceFilter.ResolveAsync(catDb, pricingService, cat.FirmPlatformId, rules, ct);

            var excludedIds = await sfDb.ChannelCategoryProducts
                .Where(p => p.ChannelCategoryId == channelCategoryId && p.IsExcluded)
                .Select(p => p.ProductId).ToListAsync(ct);

            ids = await ProductFilterHelper
                .BuildFilterQuery(catDb, rules, platformPriceIds, stockRange)
                .Where(p => !excludedIds.Contains(p.Id) && catDb.ProductImages.Any(img => img.ProductId == p.Id))
                .Select(p => p.Id).ToListAsync(ct);
        }
        else // mixed
        {
            var rules = CategoryFilterRules.From(cat.FilterDef);
            HashSet<Guid>? stockRange = null;
            if (rules?.StockMin.HasValue == true || rules?.StockMax.HasValue == true)
                stockRange = await ProductFilterHelper
                    .ResolveStockRangeProductIds(catDb, stockService, rules!.StockMin, rules.StockMax, ct);
            var platformPriceIds = await PlatformPriceFilter.ResolveAsync(catDb, pricingService, cat.FirmPlatformId, rules, ct);

            var excludedIds = await sfDb.ChannelCategoryProducts
                .Where(p => p.ChannelCategoryId == channelCategoryId && p.IsExcluded)
                .Select(p => p.ProductId).ToListAsync(ct);

            var filteredIds = await ProductFilterHelper
                .BuildFilterQuery(catDb, rules, platformPriceIds, stockRange)
                .Select(p => p.Id).ToListAsync(ct);

            var pinnedIds = await sfDb.ChannelCategoryProducts
                .Where(p => p.ChannelCategoryId == channelCategoryId && !p.IsExcluded)
                .Select(p => p.ProductId).ToListAsync(ct);

            ids = filteredIds.Union(pinnedIds).Except(excludedIds).ToList();
        }

        // Katman 1 (global satış anahtarı): satışa kapalı ürün HİÇBİR modda/kategoride
        // listelenmez — manuel/filtre/mixed hepsi bu geçitten geçer. Sıra korunur.
        // (Önceki bug: bu geçit yalnız artık ulaşılamayan eski kod yolundaydı; asıl
        // liste HandleColorMode → ResolveCategoryProductIds'den besleniyor.)
        if (ids.Count == 0) return ids;
        var acikOlanlar = (await catDb.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.IsSaleOpen)
            .Select(p => p.Id).ToListAsync(ct)).ToHashSet();
        var salesOpen = ids.Where(acikOlanlar.Contains).ToList();

        // Katman 2/3 (kanal seçimi + durdurma — M2/M3): bu kanalda çıkarılan (IsActive=false)
        // VEYA an itibarıyla durdurulmuş ürünler listeden düşer (opt-out; satır yok/seçili →
        // görünür). Sıra korunur. sorgu-zamanı: durdurma penceresi bitince otomatik geri döner.
        if (salesOpen.Count > 0)
        {
            var simdi = DateTime.UtcNow;
            var kanalDisi = (await sfDb.ChannelProducts.AsNoTracking()
                .Where(cp => cp.FirmPlatformId == cat.FirmPlatformId && salesOpen.Contains(cp.ProductId)
                          && (!cp.IsActive
                              || (cp.SaleStoppedFrom != null && cp.SaleStoppedFrom <= simdi
                                  && (cp.SaleStoppedUntil == null || cp.SaleStoppedUntil >= simdi))))
                .Select(cp => cp.ProductId).ToListAsync(ct)).ToHashSet();
            if (kanalDisi.Count > 0)
                salesOpen = salesOpen.Where(id => !kanalDisi.Contains(id)).ToList();
        }

        // Stok görünürlüğü (2026-07-14): stoğu biten (online serbest = 0) ürün, YALNIZ kanal
        // "stoğu bitenleri göster" açıksa VE (tarih kısıtı yoksa ya da Product.CreatedAt >= eşik)
        // listelenir; aksi halde gizlenir. Stoklu ürünler her zaman görünür. Sıra korunur.
        if (salesOpen.Count == 0) return salesOpen;
        var inStockSet = await inStock.GetInStockProductIdsAsync(ct);
        var gosterilecekStoksuz = new HashSet<Guid>();
        if (showOutOfStock)
        {
            var stoksuz = salesOpen.Where(id => !inStockSet.Contains(id)).ToList();
            if (stoksuz.Count > 0)
                gosterilecekStoksuz = outOfStockSince is null
                    ? stoksuz.ToHashSet()
                    : (await catDb.Products.AsNoTracking()
                        .Where(p => stoksuz.Contains(p.Id) && p.CreatedAt >= outOfStockSince.Value)
                        .Select(p => p.Id).ToListAsync(ct)).ToHashSet();
        }
        return salesOpen.Where(id => inStockSet.Contains(id) || gosterilecekStoksuz.Contains(id)).ToList();
    }

    private async Task<Result<PagedResult<ChannelCategoryProductItemDto>>> HandleModelMode(
        GetChannelCategoryProductsQuery request,
        Guid channelCategoryId,
        string cdnBase,
        CancellationToken ct)
    {
        var categoryGroups = await sfDb.ChannelCategoryGroups
            .AsNoTracking()
            .Where(g => g.ChannelCategoryId == channelCategoryId)
            .ToListAsync(ct);

        var total = categoryGroups.Count;
        var pagedGroups = categoryGroups
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        if (!pagedGroups.Any())
            return Result.Success(new PagedResult<ChannelCategoryProductItemDto>(
                [], total, request.Page, request.PageSize));

        var showcaseIds = pagedGroups
            .Where(g => g.ShowcaseProductId.HasValue)
            .Select(g => g.ShowcaseProductId!.Value)
            .ToList();

        var groupsNeedingFallback = pagedGroups
            .Where(g => !g.ShowcaseProductId.HasValue)
            .Select(g => g.ProductGroupId)
            .ToList();

        var fallbackProducts = groupsNeedingFallback.Count > 0
            ? (await catDb.Products
                .AsNoTracking()
                .Where(p => groupsNeedingFallback.Contains(p.ProductGroupId) && p.IsSaleOpen)
                .OrderBy(p => p.ProductGroupId).ThenBy(p => p.Id)
                .ToListAsync(ct))
                .GroupBy(p => p.ProductGroupId)
                .ToDictionary(g => g.Key, g => g.First())
            : new Dictionary<Guid, Catalog.Domain.Entities.Product>();

        var allProductIds = showcaseIds
            .Concat(fallbackProducts.Values.Select(p => p.Id))
            .Distinct()
            .ToList();

        var productById = allProductIds.Count > 0
            ? (await catDb.Products.AsNoTracking()
                .Where(p => allProductIds.Contains(p.Id))
                .ToListAsync(ct))
                .ToDictionary(p => p.Id)
            : new Dictionary<Guid, Catalog.Domain.Entities.Product>();

        var imageMap = allProductIds.Count > 0
            ? await catDb.ProductImages
                .AsNoTracking()
                .Where(img => allProductIds.Contains(img.ProductId))
                .GroupBy(img => img.ProductId)
                .Select(g => new { ProductId = g.Key, FileName = g.OrderBy(i => i.SortOrder).First().FileName })
                .ToDictionaryAsync(x => x.ProductId, x => x.FileName, ct)
            : new Dictionary<Guid, string>();

        var (colorMap, attrMap) = await LoadVariantAttrs(catDb, allProductIds, ct);

        var items = new List<ChannelCategoryProductItemDto>();
        foreach (var group in pagedGroups)
        {
            Catalog.Domain.Entities.Product? product = null;

            if (group.ShowcaseProductId.HasValue)
                productById.TryGetValue(group.ShowcaseProductId.Value, out product);

            if (product is null)
                fallbackProducts.TryGetValue(group.ProductGroupId, out product);

            if (product is null) continue;

            var mainImage = imageMap.TryGetValue(product.Id, out var fn) ? cdnBase + fn : null;
            items.Add(new ChannelCategoryProductItemDto(
                product.Id, product.Code, product.NameI18n, mainImage,
                product.BasePrice > 0 ? product.BasePrice : 0, product.IsSaleOpen,
                0, false, group.ProductGroupId,
                Colors: colorMap.GetValueOrDefault(product.Id),
                Attrs: attrMap.GetValueOrDefault(product.Id)));
        }

        return Result.Success(new PagedResult<ChannelCategoryProductItemDto>(
            items, total, request.Page, request.PageSize));
    }

    private static async Task<(
        Dictionary<Guid, List<ProductListingColorDto>>,
        Dictionary<Guid, List<ProductListingAttrDto>>)>
        LoadVariantAttrs(ICatalogDbContext catDb, List<Guid> productIds, CancellationToken ct)
    {
        var colorMap = new Dictionary<Guid, List<ProductListingColorDto>>();
        var attrMap  = new Dictionary<Guid, List<ProductListingAttrDto>>();

        if (productIds.Count == 0) return (colorMap, attrMap);

        var variantData = await catDb.ProductVariants
            .AsNoTracking()
            .Where(v => productIds.Contains(v.ProductId) && v.IsActive)
            .Select(v => new { v.Id, v.ProductId })
            .ToListAsync(ct);

        var variantToProduct = variantData.ToDictionary(v => v.Id, v => v.ProductId);
        var vIds = variantData.Select(v => v.Id).ToList();

        if (vIds.Count == 0) return (colorMap, attrMap);

        var colorAttrs = await catDb.ProductVariantAttributes
            .AsNoTracking()
            .Where(va => vIds.Contains(va.VariantId) && va.AttributeType.Code == "filtre_rengi")
            .Select(va => new {
                va.VariantId, va.AttributeValueId,
                NameI18n = va.AttributeValue.NameI18n,
                HexCode  = va.AttributeValue.HexCode
            })
            .ToListAsync(ct);

        var otherAttrs = await catDb.ProductVariantAttributes
            .AsNoTracking()
            .Where(va => vIds.Contains(va.VariantId) && va.AttributeType.Code != "filtre_rengi")
            .Select(va => new {
                va.VariantId,
                TypeCode      = va.AttributeType.Code,
                TypeNameI18n  = va.AttributeType.NameI18n,
                va.AttributeValueId,
                ValueNameI18n = va.AttributeValue.NameI18n,
                SortOrder     = va.AttributeValue.SortOrder
            })
            .ToListAsync(ct);

        foreach (var ca in colorAttrs)
        {
            if (!variantToProduct.TryGetValue(ca.VariantId, out var pid)) continue;
            if (!colorMap.TryGetValue(pid, out var list)) colorMap[pid] = list = new();
            if (list.All(c => c.ValueId != ca.AttributeValueId))
                list.Add(new(ca.AttributeValueId, ca.NameI18n, ca.HexCode));
        }

        foreach (var oa in otherAttrs)
        {
            if (!variantToProduct.TryGetValue(oa.VariantId, out var pid)) continue;
            if (!attrMap.TryGetValue(pid, out var list)) attrMap[pid] = list = new();
            if (list.All(a => a.TypeCode != oa.TypeCode || a.ValueId != oa.AttributeValueId))
                list.Add(new(oa.TypeCode, oa.TypeNameI18n, oa.AttributeValueId, oa.ValueNameI18n, oa.SortOrder));
        }

        return (colorMap, attrMap);
    }
}
