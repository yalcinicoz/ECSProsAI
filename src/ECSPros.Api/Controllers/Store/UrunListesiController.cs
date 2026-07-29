using ECSPros.Api.Models.Store;
using ECSPros.Api.Services;
using ECSPros.Catalog.Application.Queries.GetStoreFacets;
using ECSPros.Catalog.Application.Queries.GetStoreProducts;
using ECSPros.Storefront.Application.Queries.GetChannelCategoryFacets;
using ECSPros.Storefront.Application.Queries.GetChannelCategoryProducts;
using ECSPros.Storefront.Application.Queries.GetProductByChannelSlug;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// Ürün listesi sayfaları (B7): kategori (/{slug}), arama (/urunler?search=) ve
/// tüm ürünler (/urun-listesi). İlk sayfa + facet'ler sunucudan (MediatR süreç içi,
/// plan 3.4); devam sayfaları partial config script'i üzerinden api/store/* JSON.
/// </summary>
public class UrunListesiController(IMediator mediator, IStoreContext storeContext, StoreUrunDetayBuilder detayBuilder) : StorePageController
{
    private const int SayfaBoyu = 24;

    // B10: sayfa query parametreleri api/store parametreleriyle AYNI adları kullanır
    // (attrs=virgüllü valueId, priceMin, priceMax, sort, search) — DevamApiUrl'e birebir taşınır.
    public sealed record ListeFiltre(string? Attrs, decimal? PriceMin, decimal? PriceMax, string? Sort)
    {
        public List<Guid>? DegerIdler =>
            string.IsNullOrWhiteSpace(Attrs)
                ? null
                : Attrs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                    .Where(g => g != Guid.Empty)
                    .ToList() is { Count: > 0 } ids ? ids : null;

        public string QueryEki()
        {
            var parcalar = new List<string>();
            if (DegerIdler is { } ids) parcalar.Add("attrs=" + string.Join(",", ids));
            if (PriceMin.HasValue) parcalar.Add("priceMin=" + PriceMin.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (PriceMax.HasValue) parcalar.Add("priceMax=" + PriceMax.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(Sort)) parcalar.Add("sort=" + Uri.EscapeDataString(Sort));
            return parcalar.Count > 0 ? "&" + string.Join("&", parcalar) : "";
        }
    }

    // 2026-07-17: ?page=N — paylaşılan/izlenen sayfa numarası; SSR o sayfadan başlar.
    private static int SayfaNo(int? page) => Math.Clamp(page ?? 1, 1, 10000);

    [HttpGet("/urun-listesi")]
    public Task<IActionResult> Index([FromQuery] ListeFiltre filtre, [FromQuery] int? page, CancellationToken ct)
        => GenelListeAsync(null, filtre, SayfaNo(page), ct);

    [HttpGet("/urunler")]
    public Task<IActionResult> Arama([FromQuery] string? search, [FromQuery] ListeFiltre filtre, [FromQuery] int? page, CancellationToken ct)
        => GenelListeAsync(string.IsNullOrWhiteSpace(search) ? null : search.Trim(), filtre, SayfaNo(page), ct);

    // Tek segmentli kategori sayfası. Literal route'lar (/urunler, /sepet...) ASP.NET
    // route önceliğiyle her zaman bundan önce eşleşir; slug kategori değilse 404.
    // Regex kısıtı şart: kısıtsız {slug}, /favicon.ico gibi kök statik dosyaları da
    // endpoint olarak eşleştirir ve StaticFileMiddleware devre dışı kalır (örtük
    // UseRouting pipeline'ın başında koşar).
    // İkinci alternatif: eski sistemden birebir taşınan ürün URL'leri nokta/virgül/
    // ünlem/iki nokta içerir ama İSTİSNASIZ "-rakam" ile biter — dosya adları
    // (favicon.ico, *.js) bu kalıba uymadığından statikler korunur.
    // \u002C=virgül (route parser'ın kısıt argümanını virgülde bölmemesi için escape).
    [HttpGet("/{slug:regex(^[[a-z0-9-]]+$|^[[a-z0-9.!:\\u002C-]]+-[[0-9]]+$)}")]
    public async Task<IActionResult> Kategori(
        string slug, [FromQuery] string? search, [FromQuery] ListeFiltre filtre, [FromQuery] int? page, CancellationToken ct)
    {
        var sayfa = SayfaNo(page);
        var nav = ViewData["MsNavigasyon"] as NavigasyonVm ?? NavigasyonVm.Bos;
        // TumKokler: bos kategoriler menuden gizlense de dogrudan URL ile gelen
        // ziyaretci 404 degil "henuz urun yuklenmedi" sayfasi gormeli.
        var kategori = KategoriBul(nav.TumKokler, slug);
        if (kategori is null)
            return await UrunSlugDeneVeyaNotFound(slug, ct);

        // B10: nav arama paneli "kategoride ara" kapsam butonunu bu bağlamla gösterir
        ViewData["MsAktifKategori"] = kategori;

        var platform = await storeContext.GetPlatformAsync(ct);
        var arama = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var urunler = await mediator.Send(new GetChannelCategoryProductsQuery(
            kategori.Id, sayfa, SayfaBoyu,
            arama, filtre.DegerIdler, filtre.PriceMin, filtre.PriceMax, filtre.Sort,
            platform?.StokBitenGoster ?? false, platform?.StokBitenGosterTarih), ct);
        if (urunler.IsFailure)
            return NotFound();

        var facets = await mediator.Send(new GetChannelCategoryFacetsQuery(
            kategori.Id, platform?.StokBitenGoster ?? false, platform?.StokBitenGosterTarih,
            // 2026-07-17: seçim-duyarlı facet — aktif filtre/fiyat/arama bağlamı
            filtre.DegerIdler, filtre.PriceMin, filtre.PriceMax, arama), ct);

        var devamUrl = $"/api/store/catalog/channel-categories/{kategori.Id}/products?pageSize={SayfaBoyu}"
                       + (arama is null ? "" : "&search=" + Uri.EscapeDataString(arama))
                       + filtre.QueryEki();

        var vm = new UrunListesiVm(
            Baslik: kategori.Ad,
            ToplamUrun: urunler.Value!.TotalCount,
            SayfaBoyu: SayfaBoyu,
            IlkSayfa: urunler.Value.Items.Select(KartaCevir).ToList(),
            DevamApiUrl: devamUrl,
            FiltreGruplari: FacetleriCevir(facets.IsSuccess ? facets.Value : null),
            FiyatMin: facets.IsSuccess ? facets.Value!.PriceMin : 0,
            FiyatMax: facets.IsSuccess ? facets.Value!.PriceMax : 0,
            KategoriSecenekleri: kategori.Cocuklar,
            SeciliDegerler: filtre.DegerIdler,
            SeciliFiyatMin: filtre.PriceMin,
            SeciliFiyatMax: filtre.PriceMax,
            SeciliSiralama: filtre.Sort,
            KategorideArama: arama,
            BaslangicSayfa: sayfa,
            BosDurumMesaji: urunler.Value.TotalCount > 0 ? null
                : arama is not null
                    ? $"Bu kategoride \"{arama}\" aramasıyla eşleşen ürün bulunamadı."
                    : (filtre.DegerIdler is not null || filtre.PriceMin.HasValue || filtre.PriceMax.HasValue)
                        ? "Seçtiğiniz filtrelerle eşleşen ürün bulunamadı."
                        : "Bu kategoriye henüz ürün yüklenmedi.");

        return ListeGoster(vm);
    }

    private async Task<IActionResult> GenelListeAsync(string? arama, ListeFiltre filtre, int sayfa, CancellationToken ct)
    {
        var platform = await storeContext.GetPlatformAsync(ct);
        if (platform is null)
            return NotFound();

        var urunler = await mediator.Send(new GetStoreProductsQuery(
            platform.Id, arama, sayfa, SayfaBoyu,
            filtre.DegerIdler, filtre.PriceMin, filtre.PriceMax, filtre.Sort,
            ApplyStockFilter: true, ShowOutOfStock: platform.StokBitenGoster, OutOfStockSince: platform.StokBitenGosterTarih), ct);
        if (urunler.IsFailure)
            return NotFound();

        var facets = await mediator.Send(new GetStoreFacetsQuery(
            platform.Id, arama, platform.StokBitenGoster, platform.StokBitenGosterTarih,
            // 2026-07-17: seçim-duyarlı facet — aktif filtre/fiyat bağlamı
            filtre.DegerIdler, filtre.PriceMin, filtre.PriceMax), ct);

        var nav = ViewData["MsNavigasyon"] as NavigasyonVm ?? NavigasyonVm.Bos;
        var devamUrl = $"/api/store/catalog/products?firmPlatformId={platform.Id}&pageSize={SayfaBoyu}"
                       + (arama is null ? "" : "&search=" + Uri.EscapeDataString(arama))
                       + filtre.QueryEki();

        var vm = new UrunListesiVm(
            Baslik: arama is null ? "Tüm Ürünler" : $"\"{arama}\" araması",
            ToplamUrun: urunler.Value!.TotalCount,
            SayfaBoyu: SayfaBoyu,
            IlkSayfa: urunler.Value.Items.Select(KartaCevir).ToList(),
            DevamApiUrl: devamUrl,
            FiltreGruplari: FacetleriCevir(facets.IsSuccess ? facets.Value : null),
            FiyatMin: facets.IsSuccess ? facets.Value!.PriceMin : 0,
            FiyatMax: facets.IsSuccess ? facets.Value!.PriceMax : 0,
            KategoriSecenekleri: nav.Kokler,
            SeciliDegerler: filtre.DegerIdler,
            SeciliFiyatMin: filtre.PriceMin,
            SeciliFiyatMax: filtre.PriceMax,
            SeciliSiralama: filtre.Sort,
            BaslangicSayfa: sayfa,
            BosDurumMesaji: urunler.Value.TotalCount > 0 ? null
                : arama is not null
                    ? $"\"{arama}\" aramasıyla eşleşen ürün bulunamadı."
                    : "Gösterilecek ürün bulunamadı.");

        return ListeGoster(vm);
    }

    private IActionResult ListeGoster(UrunListesiVm vm)
    {
        ViewData["MsUrunListesi"] = vm;
        ViewData["Title"] = vm.Baslik;
        return View("~/Views/UrunListesi/Index.cshtml");
    }

    // Kök /{slug} kategori değilse: gerçek (legacy) ÜRÜN URL'i mi? (URL aktarımı 2026-07-15)
    // Bulunursa ürün detayı slug URL'inde in-place render edilir (301 yok). Slug bir ürüne
    // ait ama ürün satışa KAPALI/erişilemezse: 404 yerine ürünün kategorisine 301 (kullanıcı
    // kararı — /urun/{code} ile tutarlı). Slug hiçbir ürüne ait değilse gerçek 404.
    private async Task<IActionResult> UrunSlugDeneVeyaNotFound(string slug, CancellationToken ct)
    {
        var platform = await storeContext.GetPlatformAsync(ct);
        if (platform is null)
            return NotFound();

        var sonuc = await mediator.Send(new GetProductByChannelSlugQuery(platform.Id, slug), ct);
        if (sonuc.IsFailure || sonuc.Value is not { } bulunan)
            return NotFound();   // slug bir ürüne ait değil — bilinmeyen URL

        var vm = await detayBuilder.BuildAsync(
            bulunan.ProductCode, bulunan.ColorValueId?.ToString(),
            platform.Id, ViewData["MsUye"] as StoreUyeKimlik, ct);
        if (vm is not null)
        {
            ViewData["MsUrunDetay"] = vm;
            ViewData["Title"] = vm.Ad;
            return View("~/Views/UrunDetay/Index.cshtml");
        }

        // Slug bir ürüne ait ama render edilemedi (satışa kapalı) → kategorisine 301.
        return await KapaliUrunKategoriyeYonlendir(bulunan.ProductCode, platform.Id, ct);
    }

    // /urun/{code}'daki KapaliUrunYonlendir ile aynı: kapalı/erişilemez ürünü kategorisine
    // (yoksa ana sayfaya) 301'ler — 404'ten kaçınma (kullanıcı kararı).
    private async Task<IActionResult> KapaliUrunKategoriyeYonlendir(string code, Guid platformId, CancellationToken ct)
    {
        var idSonuc = await mediator.Send(
            new ECSPros.Catalog.Application.Queries.GetProductIdByCode.GetProductIdByCodeQuery(code), ct);
        if (idSonuc.IsSuccess && idSonuc.Value is { } urunId)
        {
            var zincir = await mediator.Send(
                new ECSPros.Storefront.Application.Queries.GetProductChannelCategoryChain
                    .GetProductChannelCategoryChainQuery(platformId, urunId), ct);
            if (zincir.IsSuccess && zincir.Value!.Count > 0)
                return RedirectPermanent("/" + zincir.Value[^1].Slug);
        }
        return RedirectPermanent("/");
    }

    private static NavKategori? KategoriBul(IReadOnlyList<NavKategori> dallar, string slug)
    {
        foreach (var dal in dallar)
        {
            if (string.Equals(dal.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return dal;
            if (KategoriBul(dal.Cocuklar, slug) is { } bulunan)
                return bulunan;
        }
        return null;
    }

    private static UrunKartVm KartaCevir(ChannelCategoryProductItemDto p) => UrunKartMap.KartaCevir(p);

    private static UrunKartVm KartaCevir(StoreProductDto p) => UrunKartMap.KartaCevir(p);

    private static List<FiltreGrupVm> FacetleriCevir(StoreFacetsDto? facets)
    {
        if (facets is null)
            return [];

        return facets.Attributes
            .Select(a => new FiltreGrupVm(
                a.TypeCode,
                a.TypeNameI18n.TryGetValue("tr", out var ad) ? ad : a.TypeCode,
                a.IsColorType,
                a.Values.Select(v => new FiltreDegerVm(
                    v.ValueId,
                    v.NameI18n.TryGetValue("tr", out var vAd) ? vAd : "",
                    v.HexCode,
                    v.ProductCount)).ToList()))
            .Where(g => g.Degerler.Count > 0)
            .ToList();
    }
}
