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
    string? Slug = null,       // URL aktarımı 2b: rengin gerçek (legacy) URL slug'ı (o platform)
    bool InStock = true);      // 2026-08-14: rengin herhangi bir varyantında stok var mı — tooltip'te stoksuz renk soluk

public record ProductListingAttrDto(
    string TypeCode,
    Dictionary<string, string> TypeNameI18n,
    Guid ValueId,
    Dictionary<string, string> ValueNameI18n,
    int SortOrder = 0);

// Kartta sepete ekle (2026-08-14): kartın beden seçenekleri — kartta gösterilen rengin
// varyantları. Beden ekseni olmayan üründe tek temsilci varyant Name="" ile döner
// (site yalnız "Sepete Ekle" butonu gösterir). Price sepete yazılacak satış fiyatıdır.
public record CardSizeDto(
    string Name,
    Guid VariantId,
    decimal Price,
    bool InStock);

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
    string? VideoUrl = null,             // H5: ilk aktif videonun efektif URL'i — null ise kartta rozet yok
    Guid? MatchedColorValueId = null,    // Kabul testi 2026-07-22: aramadaki renk kelimesiyle eşleşen renk — kart o renkle gösterilir
    Guid? MainColorValueId = null,       // 2026-07-31: kartta GÖSTERİLEN ana görselin rengi (filtre_rengi) — detay linki bu renge (?color=) gitsin
    string? CampaignName = null,         // F3: kartta gösterilecek kampanya adı/rozeti (varsa)
    decimal? CampaignPrice = null,       // F3: ürün-bazlı kampanyalı fiyat (null = yok/sepette)
    List<CampaignBadge>? CampaignBadges = null,  // 2026-08-03: ürünü kapsayan TÜM kampanyalar (ad+renk) — bantta dönüşümlü
    List<CardMessageItem>? CardMessages = null,  // Ürün Kartı F2: elle kart mesajları (slot 1/2/3)
    int CartCount = 0,                           // Sosyal kanıt (2026-08-10): son 30 günde kaç farklı sepette — cache DIŞI eklenir
    int FavoriteCount = 0,                       // Sosyal kanıt: kaç farklı üyenin favorisi — cache DIŞI eklenir
    int ViewCount = 0,                           // Sosyal kanıt: kaç farklı üye baktı — cache DIŞI eklenir
    List<CardSizeDto>? Sizes = null);            // Kartta sepete ekle (2026-08-14): ana rengin beden seçenekleri

public class GetStoreProductsQueryHandler(
    ICatalogDbContext db,
    IChannelPricingService pricingService,
    IChannelProductFlagService flagService,
    IInStockProductProvider inStock,
    IDiscountedProductProvider discounted,
    IEffectivePriceProvider effectivePrices,
    IProductReviewStatsService reviewStats,
    IProductCampaignResolver campaignResolver,
    ICardMessageResolver cardMessageResolver,
    ISocialProofResolver socialProofResolver,
    IProductMetricsProvider productMetrics)
    : IRequestHandler<GetStoreProductsQuery, Result<PagedResult<StoreProductDto>>>
{
    public async Task<Result<PagedResult<StoreProductDto>>> Handle(GetStoreProductsQuery request, CancellationToken ct)
    {
        var cdnBase = await CdnHelper.BuildListUrlAsync(db, ct);
        // Faz 2 P0: kanal fiyatları artık SAYFALAMA SONRASI, yalnız sayfadaki varyantlar için çekilir
        // (önceden platformdaki TÜM aktif varyant fiyatları belleğe alınıyordu — rapor #1 darboğazı).
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

        var aramaRenkIdleri = new HashSet<Guid>();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // Kabul testi 2026-07-22: "sarı gömlek" gibi ÇOK KELİMELİ aramalar tek parça
            // contains ile hiç sonuç vermiyordu (renk ad alanında değil, varyant özelliğinde).
            // Her kelime AYRI aranır (AND): kod VEYA Türkçe ad VEYA aktif varyantın
            // özellik değeri adı (renk/beden...) içinde geçmeli.
            var aramaKelimeleri = request.Search.Trim().ToLower()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var kelime in aramaKelimeleri)
            {
                var k = kelime;
                q = q.Where(p => p.Code.ToLower().Contains(k)
                              || PgJsonFunctions.JsonText(p.NameI18n, "tr")!.ToLower().Contains(k)
                              || p.Variants.Any(v => v.IsActive && v.VariantAttributes.Any(va =>
                                     PgJsonFunctions.JsonText(va.AttributeValue.NameI18n, "tr")!
                                         .ToLower().Contains(k))));
            }
            if (aramaKelimeleri.Length > 0)
            {
                // Renk daraltması için: herhangi bir kelimeyle eşleşen özellik değeri id'leri
                // (dayanıklılık Faz 2: kelime başına ayrı sorgu yerine TEK sorgu — birleşim aynı küme)
                var eslesenDegerler = await db.AttributeValues.AsNoTracking()
                    .Where(av => aramaKelimeleri.Any(k =>
                        PgJsonFunctions.JsonText(av.NameI18n, "tr")!.ToLower().Contains(k)))
                    .Select(av => av.Id)
                    .ToListAsync(ct);
                foreach (var id in eslesenDegerler) aramaRenkIdleri.Add(id);
            }
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
        else if (ProductSortCatalog.MetrikMi(request.Sort))
        {
            // 2026-08-10: metrik sıralamaları — fiyat sıralaması kalıbı: aday id'ler çekilir,
            // host'taki sayaç sözlüğüyle (10 dk cache) bellek içinde sıralanıp sayfalanır.
            var metrikler = await productMetrics.GetAsync(request.FirmPlatformId, ct);
            (double Ana, double Ikincil) Deger(Guid pid)
            {
                var m = metrikler.GetValueOrDefault(pid) ?? ProductMetrics.Sifir;
                return request.Sort switch
                {
                    "rating_desc" => (m.Rating, m.ReviewCount),
                    "reviews_desc" => (m.ReviewCount, 0),
                    "favorites_desc" => (m.FavoriteCount, 0),
                    "cart_desc" => (m.CartCount, 0),
                    "views_desc" => (m.ViewCount, 0),
                    _ => (m.SalesCount, 0)
                };
            }
            var adaylar = await q.Select(p => new { p.Id }).ToListAsync(ct);
            var sirali = adaylar
                .OrderByDescending(a => Deger(a.Id).Ana)
                .ThenByDescending(a => Deger(a.Id).Ikincil)
                .ThenBy(a => a.Id);
            total = adaylar.Count;
            var sayfaIdleri = sirali
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(a => a.Id)
                .ToList();
            products = await q.Where(p => sayfaIdleri.Contains(p.Id)).ToListAsync(ct);
            products = products.OrderBy(p => sayfaIdleri.IndexOf(p.Id)).ToList();
        }
        else if (!string.IsNullOrWhiteSpace(request.Search) && string.IsNullOrEmpty(request.Sort))
        {
            // Arama alaka sıralaması (2026-08-14): "sarı elbise" gibi aramalarda RENGİ eşleşen
            // ve/veya ADINDA tüm kelimeler geçen ürünler öne alınır — renk yalnız varyant
            // özelliğinde eşleşen ürünler Id sırasıyla karışık geliyordu ("aranana en yakın
            // sonuçlar önce" kullanıcı isteği). Fiyat/metrik sıralaması kalıbı: aday id'ler
            // çekilir, bellek içinde puanlanıp sayfalanır. Açık sıralama tercihi bozulmaz.
            var kelimeler = request.Search.Trim().ToLower()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var adaylar = await q
                .Select(p => new { p.Id, AdTr = PgJsonFunctions.JsonText(p.NameI18n, "tr") })
                .ToListAsync(ct);

            // Renk eşleşmesi: aramadaki kelimelerle adı eşleşen özellik değerlerinden (renk/
            // filtre_rengi dahil her tip) birini taşıyan aktif varyantı olan ürünler.
            var renkEslesenler = new HashSet<Guid>();
            if (aramaRenkIdleri.Count > 0 && adaylar.Count > 0)
            {
                var adayIdler = adaylar.Select(a => a.Id).ToList();
                renkEslesenler = (await db.ProductVariantAttributes.AsNoTracking()
                    .Where(va => aramaRenkIdleri.Contains(va.AttributeValueId)
                              && va.Variant.IsActive && adayIdler.Contains(va.Variant.ProductId))
                    .Select(va => va.Variant.ProductId)
                    .ToListAsync(ct)).ToHashSet();
            }

            int Puan(Guid id, string? adTr)
            {
                var ad = (adTr ?? "").ToLower();
                var adTam = kelimeler.All(k => ad.Contains(k));
                return (renkEslesenler.Contains(id) ? 2 : 0) + (adTam ? 1 : 0);
            }

            var sirali = adaylar
                .OrderByDescending(a => Puan(a.Id, a.AdTr))
                .ThenByDescending(a => oneCikanListe.Contains(a.Id))
                .ThenBy(a => a.Id);
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
        var channelPrices = await pricingService.GetActiveVariantPricesAsync(
            request.FirmPlatformId, products.SelectMany(p => p.Variants).Select(v => v.Id).ToList(), ct);

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

        // Arama renk eşleşmesi yedeği (2026-08-14): kelime filtre_rengi'nde değil de başka
        // tipte ("renk": "Sarı" gibi — filtre_rengi eşlemesi olmayan ürünler dahil) eşleştiyse
        // eşleşen VARYANT üzerinden çözülür: varsa varyantın filtre_rengi kartı, yoksa ham
        // eksen değeri (detay ?color= eksen değerini çözer) + o varyantın ilk görseli —
        // kart aranan renge en yakın renk ve görselle gösterilir ("sarı elbise" düzeltmesi).
        var aramaVaryantEslesmeleri = aramaRenkIdleri.Count > 0
            ? otherAttrs
                .Where(oa => aramaRenkIdleri.Contains(oa.AttributeValueId)
                          && variantToProduct.ContainsKey(oa.VariantId))
                .GroupBy(oa => variantToProduct[oa.VariantId])
                .ToDictionary(g => g.Key, g =>
                {
                    var ilk = g.First();
                    return (RenkId: variantColorOf.TryGetValue(ilk.VariantId, out var filtreRengi)
                                ? filtreRengi : ilk.AttributeValueId,
                            VariantId: ilk.VariantId);
                })
            : new Dictionary<Guid, (Guid RenkId, Guid VariantId)>();
        var ilkGorselByVariant = variantImages
            .GroupBy(i => i.VariantId)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.SortOrder).First().FileName);

        // Kartta sepete ekle + stoksuz renk (2026-08-14): varyant düzeyi stok kümesi —
        // hem beden seçeneklerinin hem renk tooltip'inin stok işaretinde kullanılır.
        var inStockVariantIds = await inStock.GetInStockVariantIdsAsync(ct);
        // Renkte stok: o rengin HERHANGİ bir varyantı stoklu ise renk stokludur
        var stokluRenkler = colorAttrs
            .Where(ca => inStockVariantIds.Contains(ca.VariantId) && variantToProduct.ContainsKey(ca.VariantId))
            .Select(ca => (Pid: variantToProduct[ca.VariantId], ca.AttributeValueId))
            .ToHashSet();

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
                        : null,
                    InStock: stokluRenkler.Contains((pid, ca.AttributeValueId))));
        }

        foreach (var oa in otherAttrs)
        {
            if (!variantToProduct.TryGetValue(oa.VariantId, out var pid)) continue;
            if (!attrsByProduct.TryGetValue(pid, out var list))
                attrsByProduct[pid] = list = new();
            if (list.All(a => a.TypeCode != oa.TypeCode || a.ValueId != oa.AttributeValueId))
                list.Add(new(oa.TypeCode, oa.TypeNameI18n, oa.AttributeValueId, oa.ValueNameI18n, oa.SortOrder));
        }

        // Kartta sepete ekle (2026-08-14): varyantların renk-dışı eksen satırları (beden) —
        // kartta gösterilen rengin bedenleri DTO'ya yazılır (stok kümesi yukarıda çekildi).
        var bedenRows = otherAttrs.Where(oa => oa.TypeCode != "renk").ToLookup(oa => oa.VariantId);
        static string BedenAd(Dictionary<string, string> d) =>
            d.TryGetValue("tr", out var ad) ? ad : d.Values.FirstOrDefault() ?? "";

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
            // İndirim öncesi (çizili) fiyat: kanal CompareAtPrice'ların en yükseği (yalnız satış fiyatı üstündeyse).
            var eskiFiyatlar = activeVariants
                .Where(v => channelPrices.ContainsKey(v.Id))
                .Select(v => channelPrices[v.Id].CompareAtPrice ?? 0m)
                .Where(c => c > 0).ToList();
            decimal? eskiFiyat = eskiFiyatlar.Any() && eskiFiyatlar.Max() > minPrice ? eskiFiyatlar.Max() : null;
            firstImages.TryGetValue(p.Id, out var ilkGorsel);
            var mainImage = ilkGorsel is null ? null : cdnBase + ilkGorsel.FileName;

            // Ana görselin ait olduğu renk (filtre_rengi) — hem hover galerisi hem kartın detay
            // linkinin rengi (?color=) için. Kart bu rengi gösterir; link de bu renge gitmeli ki
            // listede görülen renk detayda da açılsın (2026-07-31 "son baktıklarım" tutarlılığı).
            Guid? anaRenkId = ilkGorsel?.VariantId is { } anaVaryantId
                && variantColorOf.TryGetValue(anaVaryantId, out var arid) ? arid : null;

            // B8 hover galerisi: ana görselin ait olduğu rengin görselleri (≤4). Renk
            // çözülemiyorsa galeri verilmez — farklı renklerin karışık havuzu "tekrarlı
            // galeri" üretir (detay handler'ındaki dersle aynı).
            List<string>? galleryUrls = null;
            if (anaRenkId is { } anaRenk
                && imagesByProductColor.TryGetValue((p.Id, anaRenk), out var galeriImgs))
            {
                galleryUrls = galeriImgs.Take(4).Select(fn2 => cdnBase + fn2).ToList();
            }

            var renkler = colorsByProduct.GetValueOrDefault(p.Id) ?? new();
            // Kart aramadaki renk kelimesiyle eşleşen renkle gösteriliyorsa bedenler de o renkten;
            // filtre_rengi'nde doğrudan eşleşme yoksa eşleşen varyant üzerinden (rengi + görseli) düşülür.
            var eslesenRenkId = aramaRenkIdleri.Count > 0
                ? renkler.FirstOrDefault(c => aramaRenkIdleri.Contains(c.ValueId))?.ValueId
                : null;
            if (eslesenRenkId is null && aramaRenkIdleri.Count > 0
                && aramaVaryantEslesmeleri.TryGetValue(p.Id, out var varyantEslesme))
            {
                eslesenRenkId = varyantEslesme.RenkId;
                // Eşleşen varyantın ilk görseli kart görseli olur — "sarı" ararken sarı görülsün
                if (ilkGorselByVariant.TryGetValue(varyantEslesme.VariantId, out var eslesenDosya))
                    mainImage = cdnBase + eslesenDosya;
            }

            // Kartta sepete ekle: kartta gösterilen rengin varyant havuzu → beden seçenekleri.
            // Fiyat çözümü detay sayfasıyla aynı sıra: kanal fiyatı → varyant fiyatı → kart fiyatı.
            var havuz = (eslesenRenkId ?? anaRenkId) is { } seciliRenk
                ? activeVariants.Where(v => variantColorOf.TryGetValue(v.Id, out var vr) && vr == seciliRenk).ToList()
                : activeVariants;
            if (havuz.Count == 0) havuz = activeVariants;
            decimal VaryantFiyati(ProductVariant v)
            {
                var kanal = channelPrices.TryGetValue(v.Id, out var kf) ? kf.Price ?? 0 : 0;
                return kanal > 0 ? kanal : v.BasePrice > 0 ? v.BasePrice : minPrice;
            }
            List<CardSizeDto>? bedenler = null;
            var bedenTipKodu = havuz.SelectMany(v => bedenRows[v.Id]).FirstOrDefault()?.TypeCode;
            if (bedenTipKodu is not null)
            {
                bedenler = havuz
                    .Select(v => (Varyant: v, Beden: bedenRows[v.Id].FirstOrDefault(r => r.TypeCode == bedenTipKodu)))
                    .Where(x => x.Beden is not null)
                    .GroupBy(x => x.Beden!.AttributeValueId).Select(g => g.First())
                    .OrderBy(x => x.Beden!.SortOrder > 0 ? 0 : 1)
                    .ThenBy(x => x.Beden!.SortOrder)
                    .Select(x => new CardSizeDto(BedenAd(x.Beden!.ValueNameI18n), x.Varyant.Id,
                        VaryantFiyati(x.Varyant), inStockVariantIds.Contains(x.Varyant.Id)))
                    .ToList();
            }
            else if (havuz.Count > 0)
            {
                // Beden ekseni yok: tek temsilci varyant (stoklu tercih) — site yalnız buton gösterir
                var temsilci = havuz.FirstOrDefault(v => inStockVariantIds.Contains(v.Id)) ?? havuz[0];
                bedenler = [new CardSizeDto("", temsilci.Id, VaryantFiyati(temsilci), inStockVariantIds.Contains(temsilci.Id))];
            }

            return new StoreProductDto(
                p.Id, p.Code, p.NameI18n, p.ShortDescriptionI18n,
                mainImage, minPrice, eskiFiyat, p.IsSaleOpen,
                renkler,
                attrsByProduct.GetValueOrDefault(p.Id) ?? new(),
                galleryUrls,
                IsFeatured: oneCikanlar.Contains(p.Id),
                VideoUrl: videolar.GetValueOrDefault(p.Id),
                MatchedColorValueId: eslesenRenkId,
                MainColorValueId: anaRenkId,
                Sizes: bedenler);
        }).ToList();

        // E7: kart puanları onaylı yorum ortalamasından (additive alanlar)
        var puanlar = await reviewStats.GetStatsAsync(
            request.FirmPlatformId, items.Select(i => i.Code).Distinct().ToList(), ct);
        for (var i = 0; i < items.Count; i++)
            if (puanlar.TryGetValue(items[i].Code, out var p))
                items[i] = items[i] with { Rating = p.Average, ReviewCount = p.Count };

        // F3: kart kampanyası — ürün başına kampanyalar (F2 resolver); kampanyalı fiyat MinPrice üzerinden.
        // 2026-08-03: tüm eşleşen kampanyalar döner (bantta dönüşümlü); fiyat yalnız kazanandan (ilk).
        var kampanyalar = await campaignResolver.ResolveAllForProductsAsync(
            request.FirmPlatformId, items.Select(i => i.Id).ToList(), ct);
        for (var i = 0; i < items.Count; i++)
            if (kampanyalar.TryGetValue(items[i].Id, out var kmpListe) && kmpListe.Count > 0)
                items[i] = items[i] with
                {
                    CampaignName = kmpListe[0].BadgeLabel ?? kmpListe[0].Name,
                    CampaignPrice = CampaignPricing.EffectivePrice(kmpListe[0], items[i].MinPrice),
                    CampaignBadges = kmpListe.Select(k => new CampaignBadge(
                        k.BadgeLabel ?? k.Name, CampaignBadgePalette.Resolve(k.BadgeColor))).ToList()
                };

        // Ürün Kartı F2: elle kart mesajları (kapsam: tümü/kategori/ürün kodu) — additive alan
        var mesajlar = await cardMessageResolver.ResolveForProductsAsync(
            request.FirmPlatformId, items.ToDictionary(i => i.Id, i => i.Code), ct);
        for (var i = 0; i < items.Count; i++)
            if (mesajlar.TryGetValue(items[i].Id, out var mesajListe))
                items[i] = items[i] with { CardMessages = mesajListe };

        // Sosyal kanıt: sepet/favori sayaçları — puan/kampanya/mesaj gibi cache DIŞI (canlı sayı)
        var sosyal = await socialProofResolver.ResolveForProductsAsync(
            request.FirmPlatformId, items.ToDictionary(i => i.Id, i => i.Code), ct);
        for (var i = 0; i < items.Count; i++)
            if (sosyal.TryGetValue(items[i].Id, out var sk))
                items[i] = items[i] with { CartCount = sk.CartCount, FavoriteCount = sk.FavoriteCount, ViewCount = sk.ViewCount };

        return Result.Success(new PagedResult<StoreProductDto>(items, total, request.Page, request.PageSize));
    }
}
