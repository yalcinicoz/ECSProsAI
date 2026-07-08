using ECSPros.Api.Models.Store;
using ECSPros.Api.Services;
using ECSPros.Catalog.Application.Queries.GetStoreFacets;
using ECSPros.Catalog.Application.Queries.GetStoreProducts;
using ECSPros.Storefront.Application.Queries.GetChannelCategoryFacets;
using ECSPros.Storefront.Application.Queries.GetChannelCategoryProducts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// Ürün listesi sayfaları (B7): kategori (/{slug}), arama (/urunler?search=) ve
/// tüm ürünler (/urun-listesi). İlk sayfa + facet'ler sunucudan (MediatR süreç içi,
/// plan 3.4); devam sayfaları partial config script'i üzerinden api/store/* JSON.
/// </summary>
public class UrunListesiController(IMediator mediator, IStoreContext storeContext) : StorePageController
{
    private const int SayfaBoyu = 24;

    [HttpGet("/urun-listesi")]
    public Task<IActionResult> Index(CancellationToken ct) => GenelListeAsync(null, ct);

    [HttpGet("/urunler")]
    public Task<IActionResult> Arama([FromQuery] string? search, CancellationToken ct)
        => GenelListeAsync(string.IsNullOrWhiteSpace(search) ? null : search.Trim(), ct);

    // Tek segmentli kategori sayfası. Literal route'lar (/urunler, /sepet...) ASP.NET
    // route önceliğiyle her zaman bundan önce eşleşir; slug kategori değilse 404.
    // Regex kısıtı şart: kısıtsız {slug}, /favicon.ico gibi kök statik dosyaları da
    // endpoint olarak eşleştirir ve StaticFileMiddleware devre dışı kalır (örtük
    // UseRouting pipeline'ın başında koşar).
    [HttpGet("/{slug:regex(^[[a-z0-9-]]+$)}")]
    public async Task<IActionResult> Kategori(string slug, CancellationToken ct)
    {
        var nav = ViewData["MsNavigasyon"] as NavigasyonVm ?? NavigasyonVm.Bos;
        var kategori = KategoriBul(nav.Kokler, slug);
        if (kategori is null)
            return NotFound();

        var urunler = await mediator.Send(
            new GetChannelCategoryProductsQuery(kategori.Id, 1, SayfaBoyu), ct);
        if (urunler.IsFailure)
            return NotFound();

        var facets = await mediator.Send(new GetChannelCategoryFacetsQuery(kategori.Id), ct);

        var vm = new UrunListesiVm(
            Baslik: kategori.Ad,
            ToplamUrun: urunler.Value!.TotalCount,
            SayfaBoyu: SayfaBoyu,
            IlkSayfa: urunler.Value.Items.Select(KartaCevir).ToList(),
            DevamApiUrl: $"/api/store/catalog/channel-categories/{kategori.Id}/products?pageSize={SayfaBoyu}",
            FiltreGruplari: FacetleriCevir(facets.IsSuccess ? facets.Value : null),
            FiyatMin: facets.IsSuccess ? facets.Value!.PriceMin : 0,
            FiyatMax: facets.IsSuccess ? facets.Value!.PriceMax : 0,
            KategoriSecenekleri: kategori.Cocuklar);

        return ListeGoster(vm);
    }

    private async Task<IActionResult> GenelListeAsync(string? arama, CancellationToken ct)
    {
        var platform = await storeContext.GetPlatformAsync(ct);
        if (platform is null)
            return NotFound();

        var urunler = await mediator.Send(
            new GetStoreProductsQuery(platform.Id, arama, 1, SayfaBoyu), ct);
        if (urunler.IsFailure)
            return NotFound();

        var facets = await mediator.Send(new GetStoreFacetsQuery(platform.Id, arama), ct);

        var nav = ViewData["MsNavigasyon"] as NavigasyonVm ?? NavigasyonVm.Bos;
        var devamUrl = $"/api/store/catalog/products?firmPlatformId={platform.Id}&pageSize={SayfaBoyu}"
                       + (arama is null ? "" : "&search=" + Uri.EscapeDataString(arama));

        var vm = new UrunListesiVm(
            Baslik: arama is null ? "Tüm Ürünler" : $"\"{arama}\" araması",
            ToplamUrun: urunler.Value!.TotalCount,
            SayfaBoyu: SayfaBoyu,
            IlkSayfa: urunler.Value.Items.Select(KartaCevir).ToList(),
            DevamApiUrl: devamUrl,
            FiltreGruplari: FacetleriCevir(facets.IsSuccess ? facets.Value : null),
            FiyatMin: facets.IsSuccess ? facets.Value!.PriceMin : 0,
            FiyatMax: facets.IsSuccess ? facets.Value!.PriceMax : 0,
            KategoriSecenekleri: nav.Kokler);

        return ListeGoster(vm);
    }

    private IActionResult ListeGoster(UrunListesiVm vm)
    {
        ViewData["MsUrunListesi"] = vm;
        ViewData["Title"] = vm.Baslik;
        return View("~/Views/UrunListesi/Index.cshtml");
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

    private static UrunKartVm KartaCevir(ChannelCategoryProductItemDto p)
    {
        var renkler = p.Colors ?? [];
        var degerIdler = renkler.Select(c => c.ValueId)
            .Concat((p.Attrs ?? []).Select(a => a.ValueId))
            .Distinct().ToList();
        return new UrunKartVm(
            p.Code, TrAd(p.NameI18n), p.MainImageUrl, p.BasePrice,
            renkler.Where(c => c.HexCode is not null).Select(c => c.HexCode!).Take(2).ToList(),
            renkler.Count, degerIdler);
    }

    private static UrunKartVm KartaCevir(StoreProductDto p)
    {
        var degerIdler = p.Colors.Select(c => c.ValueId)
            .Concat(p.Attrs.Select(a => a.ValueId))
            .Distinct().ToList();
        return new UrunKartVm(
            p.Code, TrAd(p.NameI18n), p.MainImageUrl, p.MinPrice,
            p.Colors.Where(c => c.HexCode is not null).Select(c => c.HexCode!).Take(2).ToList(),
            p.Colors.Count, degerIdler);
    }

    private static string TrAd(Dictionary<string, string> nameI18n) =>
        nameI18n.TryGetValue("tr", out var ad) ? ad : nameI18n.Values.FirstOrDefault() ?? "";

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
