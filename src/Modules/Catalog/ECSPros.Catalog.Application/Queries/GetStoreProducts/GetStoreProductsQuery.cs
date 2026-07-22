using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetStoreProducts;

// Shared listing DTOs (used by both this query and Storefront module)
public record ProductListingColorDto(
    Guid ValueId,
    Dictionary<string, string> NameI18n,
    string? HexCode,
    string? ImageUrl = null,   // B8: renk tooltip görseli (rengin ilk varyant görseli)
    string? Slug = null);      // URL aktarımı 2b: rengin gerçek (legacy) URL slug'ı (o platform)

public record ProductListingAttrDto(
    string TypeCode,
    Dictionary<string, string> TypeNameI18n,
    Guid ValueId,
    Dictionary<string, string> ValueNameI18n,
    int SortOrder = 0);

// B10: filtre/sıralama parametreleri additive — mobil/SPA eski çağrıları etkilenmez.
// AttributeValueIds tip bazında gruplanır: aynı tipin değerleri OR, tipler arası AND.
// Sort: "price_asc" | "price_desc" | "newest" | null (varsayılan sıra).
// Fiyat filtresi varyant BasePrice üzerindendir (kategori kartlarının fiyat kaynağıyla aynı;
// kanal fiyat override'ı yalnız gösterimde — fiyat mimarisi Faz G'de netleşince revize edilir).
public record GetStoreProductsQuery(
    Guid FirmPlatformId,
    string? Search = null,
    int Page = 1,
    int PageSize = 24,
    List<Guid>? AttributeValueIds = null,
    decimal? PriceMin = null,
    decimal? PriceMax = null,
    string? Sort = null,
    List<string>? ProductCodes = null,   // E5: Favorilerim — kod listesiyle kart verisi
    List<Guid>? ProductIds = null,       // G3: kampanya kaynağı — id listesiyle kart verisi
    DateTime? CreatedSince = null,       // H8: favori arama bildirimi — yalnız bu tarihten sonra eklenen ürünler
    // Stok görünürlüğü (2026-07-14): yalnız arama/liste çağrılarında true — vitrin/bildirim etkilenmez.
    bool ApplyStockFilter = false,
    bool ShowOutOfStock = false,
    DateTime? OutOfStockSince = null,
    // H10 vitrin kaynak bayrakları (additive): etiket eşleşmesi (en az biri) + yalnız indirimli
    List<string>? Tags = null,
    bool DiscountedOnly = false) : IRequest<Result<PagedResult<StoreProductDto>>>;

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
    List<ProductListingAttrDto> Attrs,
    List<string>? GalleryUrls = null,    // B8: kart hover galerisi (ana görselin rengine ait ilk 4 görsel)
    bool IsFeatured = false,             // B11: öne çıkar penceresi içinde — kartta "Sponsorlu" rozeti
    double Rating = 0,                   // E7: onaylı yorum ortalaması (0 = yorum yok)
    int ReviewCount = 0,                 // E7: onaylı yorum sayısı
    string? VideoUrl = null);            // H5: ilk aktif videonun efektif URL'i — null ise kartta rozet yok

public class GetStoreProductsQueryHandler(
    ICatalogDbContext db,
    IChannelPricingService pricingService,
    IChannelProductFlagService flagService,
    IInStockProductProvider inStock,
    IDiscountedProductProvider discounted,
    IEffectivePriceProvider effectivePrices,
    IProductReviewStatsService reviewStats)
    : IRequestHandler<GetStoreProductsQuery, Result<PagedResult<StoreProductDto>>>
{
    public async Task<Result<PagedResult<StoreProductDto>>> Handle(GetStoreProductsQuery request, CancellationToken ct)
    {
        var cdnBase = await CdnHelper.BuildListUrlAsync(db, ct);
        var channelPrices = await pricingService.GetActiveVariantPricesAsync(request.FirmPlatformId, ct);
        // B11: öne çıkanlar (az sayıda) — varsayılan sırada öne alınır, rozet bayrağına yazılır
        var oneCikanlar = await flagService.GetFeaturedProductIdsAsync(request.FirmPlatformId, ct);
        // Kanal seçimi/durdurma (M2/M3): bu kanalda çıkarılan/durdurulan ürünler — IsSaleOpen
        // gibi sert satılabilirlik kuralı, her zaman uygulanır (opt-out: satır yok/seçili → dahil).
        var kanalDisi = await flagService.GetChannelExcludedProductIdsAsync(request.FirmPlatformId, ct);
        var q = db.Products
            .AsNoTracking()
            .Include(p => p.Variants)
            .Where(p => p.IsSaleOpen && db.ProductImages.Any(img => img.ProductId == p.Id)
                     && !kanalDisi.Contains(p.Id));

        // Stok görünürlüğü (yalnız arama/liste): stoğu biten (in-stock kümesinde yok) ürün,
        // kanal açık VE CreatedAt >= eşik değilse gizlenir. inStock kümesi = ANY(array) (verimli).
        if (request.ApplyStockFilter)
        {
            var inStockIds = await inStock.GetInStockProductIdsAsync(ct);
            var showOos = request.ShowOutOfStock;
            var oosSince = request.OutOfStockSince;
            q = q.Where(p => inStockIds.Contains(p.Id)
                             || (showOos && (oosSince == null || p.CreatedAt >= oosSince)));
        }

        // E5: Favorilerim — yalnız verilen kodlar (canlı katalogla birleşim: silinen/pasif
        // ürünün favorisi listelenmez)
        if (request.ProductCodes is { Count: > 0 } kodlar)
            q = q.Where(p => kodlar.Contains(p.Code));

        // G3: kampanya kaynağı — yalnız verilen id'ler (kod listesiyle aynı davranış)
        if (request.ProductIds is { Count: > 0 } idler)
            q = q.Where(p => idler.Contains(p.Id));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // Kod VEYA Türkçe ad eşleşmesi (NameI18n->>'tr') — B2 canlı arama önerileri
            // metinle arar; salt kod araması müşteri için sonuç üretmiyordu.
            var search = request.Search.Trim().ToLower();
            q = q.Where(p => p.Code.ToLower().Contains(search)
                          || PgJsonFunctions.JsonText(p.NameI18n, "tr")!.ToLower().Contains(search));
        }

        if (request.CreatedSince.HasValue)
            q = q.Where(p => p.CreatedAt >= request.CreatedSince.Value);

        // H10: etiket bayrağı — en az bir etiket eşleşmeli (jsonb ?| — jsonb_exists_any)
        if (request.Tags is { Count: > 0 } etiketler)
        {
            var etiketDizisi = etiketler.ToArray();
            q = q.Where(p => PgJsonFunctions.JsonExistsAny(p.Tags, etiketDizisi));
        }

        // H10: indirim bayrağı — kanalda CompareAtPrice > Price olan ürünler (küme cache'li)
        if (request.DiscountedOnly)
        {
            var indirimliIds = await discounted.GetDiscountedProductIdsAsync(request.FirmPlatformId, ct);
            q = q.Where(p => indirimliIds.Contains(p.Id));
        }

        // B10: özellik filtresi — seçili değerler tipine göre gruplanır; grup içi OR
        // (herhangi bir aktif varyantta değer), gruplar arası AND (ürün seviyesinde).
        if (request.AttributeValueIds is { Count: > 0 } seciliDegerler)
        {
            var degerTipleri = await db.AttributeValues.AsNoTracking()
                .Where(v => seciliDegerler.Contains(v.Id))
                .Select(v => new { v.Id, v.AttributeTypeId })
                .ToListAsync(ct);

            foreach (var grup in degerTipleri.GroupBy(v => v.AttributeTypeId))
            {
                var grupDegerleri = grup.Select(g => g.Id).ToList();
                // 2026-07-17: ürün SEVİYESİ özellik değerleri de eşleşir (kumaş türü, kalıp…
                // product_attributes'ta yaşar) — grup varyantta VEYA üründe sağlanabilir.
                q = q.Where(p => p.Variants.Any(v => v.IsActive
                        && db.ProductVariantAttributes.Any(va =>
                            va.VariantId == v.Id && grupDegerleri.Contains(va.AttributeValueId)))
                    || db.ProductAttributes.Any(pa =>
                        pa.ProductId == p.Id && pa.AttributeValueId != null
                        && grupDegerleri.Contains(pa.AttributeValueId.Value)));
            }
        }

        // B10: fiyat aralığı — kartın gösterdiği fiyatın kaynağı olan varyant BasePrice'ı
        // aralıkta olan en az bir aktif varyant.
        if (request.PriceMin.HasValue)
            q = q.Where(p => p.Variants.Any(v => v.IsActive && v.BasePrice >= request.PriceMin.Value
                && (!request.PriceMax.HasValue || v.BasePrice <= request.PriceMax.Value)));
        else if (request.PriceMax.HasValue)
            q = q.Where(p => p.Variants.Any(v => v.IsActive && v.BasePrice > 0 && v.BasePrice <= request.PriceMax.Value));

        // B10 revizyonu (B-005/B-006, kabul testi 2026-07-22): fiyat sıralaması GÖSTERİLEN
        // (efektif) fiyattan yapılır — kanal override'ı varken BasePrice ile sıralamak kartta
        // görünen fiyatla çelişen sıra üretiyordu (kategori sayfası zaten kanal fiyatından
        // sıralıyor; genel liste onunla eşitlendi). Efektif fiyat kümesi cache'li provider'dan;
        // sıralama+sayfalama id listesi üzerinde, sayfa ürünleri ikinci sorguyla yüklenir.
        // B11: varsayılan sırada öne çıkanlar önce (kullanıcının açık tercihi bozulmaz).
        var oneCikanListe = oneCikanlar.ToList();
        int total;
        List<Product> products;
        if (request.Sort is "price_asc" or "price_desc")
        {
            var fiyatlar = await effectivePrices.GetMinEffectivePricesAsync(request.FirmPlatformId, ct);
            var adaylar = await q.Select(p => new { p.Id, p.BasePrice }).ToListAsync(ct);
            var sirali = request.Sort == "price_asc"
                ? adaylar.OrderBy(a => fiyatlar.GetValueOrDefault(a.Id, a.BasePrice)).ThenBy(a => a.Id)
                : adaylar.OrderByDescending(a => fiyatlar.GetValueOrDefault(a.Id, a.BasePrice)).ThenBy(a => a.Id);
            total = adaylar.Count;
            var sayfaIdleri = sirali
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(a => a.Id)
                .ToList();
            products = await q.Where(p => sayfaIdleri.Contains(p.Id)).ToListAsync(ct);
            products = products.OrderBy(p => sayfaIdleri.IndexOf(p.Id)).ToList();
        }
        else
        {
            q = request.Sort switch
            {
                "newest" => q.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
                _ when oneCikanListe.Count > 0 =>
                    q.OrderByDescending(p => oneCikanListe.Contains(p.Id)).ThenBy(p => p.Id),
                _ => q.OrderBy(p => p.Id)
            };

            total = await q.CountAsync(ct);
            products = await q
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(ct);
        }

        var productIds = products.Select(p => p.Id).ToList();

        // Main images (VariantId ile — B8 hover galerisi ana görselin RENGİNE ait görsellerden kurulur)
        var firstImages = await db.ProductImages
            .AsNoTracking()
            .Where(img => productIds.Contains(img.ProductId))
            .GroupBy(img => img.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                FileName = g.OrderBy(i => i.SortOrder).First().FileName,
                VariantId = g.OrderBy(i => i.SortOrder).First().VariantId
            })
            .ToDictionaryAsync(x => x.ProductId, x => new { x.FileName, x.VariantId }, ct);

        // H5: kart video rozeti — ürün başına ilk aktif videonun efektif URL'i
        // (VideoUrl ?? video CDN tabanı + FileName; taban ayarı yoksa dosya kayıtları atlanır).
        var videoBase = await CdnHelper.BuildVideoBaseAsync(db, ct);
        var videolar = (await db.ProductVideos
            .AsNoTracking()
            .Where(v => productIds.Contains(v.ProductId) && v.Status == Domain.Entities.ProductImageStatus.Active)
            .OrderBy(v => v.SortOrder)
            .Select(v => new { v.ProductId, v.VideoUrl, v.FileName })
            .ToListAsync(ct))
            .Select(v => new { v.ProductId, Url = v.VideoUrl ?? (videoBase != null && v.FileName != "" ? videoBase + "/" + v.FileName : null) })
            .Where(v => v.Url != null)
            .GroupBy(v => v.ProductId)
            .ToDictionary(g => g.Key, g => g.First().Url);

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

        // B8: varyant görselleri — renk tooltip görseli + kart hover galerisi için.
        // Renk havuzu = o renkteki tüm varyantların görsellerinin dosya adına göre tekilleşmiş
        // birleşimi (detay handler'ıyla aynı yaklaşım).
        var variantColorOf = colorAttrs
            .GroupBy(ca => ca.VariantId)
            .ToDictionary(g => g.Key, g => g.First().AttributeValueId);

        var variantImages = variantIds.Count > 0
            ? await db.ProductImages.AsNoTracking()
                .Where(img => img.VariantId != null
                           && variantIds.Contains(img.VariantId.Value)
                           && img.Status == ProductImageStatus.Active)
                .Select(img => new { VariantId = img.VariantId!.Value, img.FileName, img.SortOrder })
                .ToListAsync(ct)
            : [];

        var imagesByProductColor = variantImages
            .Where(i => variantColorOf.ContainsKey(i.VariantId) && variantToProduct.ContainsKey(i.VariantId))
            .GroupBy(i => (ProductId: variantToProduct[i.VariantId], ColorId: variantColorOf[i.VariantId]))
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(i => i.SortOrder)
                      .GroupBy(i => i.FileName).Select(x => x.First().FileName)
                      .ToList());

        // Group by product
        var colorsByProduct = new Dictionary<Guid, List<ProductListingColorDto>>();
        var attrsByProduct  = new Dictionary<Guid, List<ProductListingAttrDto>>();

        foreach (var ca in colorAttrs)
        {
            if (!variantToProduct.TryGetValue(ca.VariantId, out var pid)) continue;
            if (!colorsByProduct.TryGetValue(pid, out var list))
                colorsByProduct[pid] = list = new();
            if (list.All(c => c.ValueId != ca.AttributeValueId))
                list.Add(new(ca.AttributeValueId, ca.NameI18n, ca.HexCode,
                    imagesByProductColor.TryGetValue((pid, ca.AttributeValueId), out var renkImgs)
                        ? cdnBase + renkImgs[0]
                        : null));
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
            firstImages.TryGetValue(p.Id, out var ilkGorsel);
            var mainImage = ilkGorsel is null ? null : cdnBase + ilkGorsel.FileName;

            // B8 hover galerisi: ana görselin ait olduğu rengin görselleri (≤4). Renk
            // çözülemiyorsa galeri verilmez — farklı renklerin karışık havuzu "tekrarlı
            // galeri" üretir (detay handler'ındaki dersle aynı).
            List<string>? galleryUrls = null;
            if (ilkGorsel?.VariantId is { } anaVaryantId
                && variantColorOf.TryGetValue(anaVaryantId, out var anaRenkId)
                && imagesByProductColor.TryGetValue((p.Id, anaRenkId), out var galeriImgs))
            {
                galleryUrls = galeriImgs.Take(4).Select(fn2 => cdnBase + fn2).ToList();
            }

            return new StoreProductDto(
                p.Id, p.Code, p.NameI18n, p.ShortDescriptionI18n,
                mainImage, minPrice, null, p.IsSaleOpen,
                colorsByProduct.GetValueOrDefault(p.Id) ?? new(),
                attrsByProduct.GetValueOrDefault(p.Id) ?? new(),
                galleryUrls,
                IsFeatured: oneCikanlar.Contains(p.Id),
                VideoUrl: videolar.GetValueOrDefault(p.Id));
        }).ToList();

        // E7: kart puanları onaylı yorum ortalamasından (additive alanlar)
        var puanlar = await reviewStats.GetStatsAsync(
            request.FirmPlatformId, items.Select(i => i.Code).Distinct().ToList(), ct);
        for (var i = 0; i < items.Count; i++)
            if (puanlar.TryGetValue(items[i].Code, out var p))
                items[i] = items[i] with { Rating = p.Average, ReviewCount = p.Count };

        return Result.Success(new PagedResult<StoreProductDto>(items, total, request.Page, request.PageSize));
    }
}
