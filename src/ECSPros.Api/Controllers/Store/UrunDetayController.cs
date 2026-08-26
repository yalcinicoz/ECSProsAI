using ECSPros.Api.Services;
using ECSPros.Catalog.Application.Queries.GetProductIdByCode;
using ECSPros.Storefront.Application.Queries.GetChannelSlugForProduct;
using ECSPros.Storefront.Application.Queries.GetProductChannelCategoryChain;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// Ürün detay sayfası (B9): /urun/{code}?color={valueId}. Tamamen SSR — seçili renk
/// sunucuda çözülür. VM kurulumu StoreUrunDetayBuilder'da (gerçek slug URL'i de aynı render'ı
/// kullanır — UrunListesiController /{slug}). Veri süreç içi MediatR'dan gelir; mobil uygulama
/// aynı sorguyu api/store/catalog/products/{code} üzerinden kullanır (plan 3.4).
/// </summary>
public class UrunDetayController(IMediator mediator, IStoreContext storeContext, StoreUrunDetayBuilder detayBuilder) : StorePageController
{
    [HttpGet("/urun/{code}")]
    public async Task<IActionResult> Index(string code, [FromQuery] string? color, CancellationToken ct)
    {
        var platform = await storeContext.GetPlatformAsync(ct);
        if (platform is null)
            return Redirect("/");   // platform çözülemedi — 404 yerine ana sayfa

        // Aşama 2: gerçek URL tam geçiş — bu ürünün o platformdaki kanonik slug'ı varsa
        // /urun/{code} → 301 slug (seçili renge göre). Slug yoksa normal render (güvenlik ağı).
        Guid? renkId = Guid.TryParse(color, out var rg) ? rg : null;
        var slugSonuc = await mediator.Send(new GetChannelSlugForProductQuery(platform.Id, code, renkId), ct);
        if (slugSonuc.IsSuccess && slugSonuc.Value is { Length: > 0 } kanonikSlug)
            return RedirectPermanent("/" + kanonikSlug);

        // Satışa kapalı / erişilemeyen ürün: 404 yerine ürünün kategorisine, yoksa ana sayfaya 301.
        var vm = await detayBuilder.BuildAsync(code, color, platform.Id, ViewData["MsUye"] as StoreUyeKimlik, ct,
            RefererKategoriSlug(Request));
        if (vm is null)
            return await KapaliUrunYonlendir(code, platform.Id, ct);

        ViewData["MsUrunDetay"] = vm;
        ViewData["Title"] = vm.Ad;
        return View("~/Views/UrunDetay/Index.cshtml");
    }

    /// <summary>Ziyaretçinin geldiği sayfa (Referer) aynı hosttaki tek segmentli bir yol ise
    /// slug'ını döner — breadcrumb "geldiğin kategori" tercihinde kullanılır (2026-08-26).
    /// Slug bir kategoriye denk gelmiyorsa zincir sorgusu tercihi sessizce yok sayar.</summary>
    internal static string? RefererKategoriSlug(HttpRequest req)
    {
        var referer = req.Headers.Referer.ToString();
        if (string.IsNullOrEmpty(referer) || !Uri.TryCreate(referer, UriKind.Absolute, out var u))
            return null;
        if (!string.Equals(u.Host, req.Host.Host, StringComparison.OrdinalIgnoreCase))
            return null;   // yalnız kendi sitemizden gelen geçişler
        var yol = u.AbsolutePath.Trim('/');
        return yol.Length == 0 || yol.Contains('/') ? null : yol;
    }

    // Satışa kapalı/erişilemeyen ürün için 301: ürünün (satış durumundan bağımsız) kanal
    // kategorisine, tespit edilemezse ana sayfaya. 404'ten kaçınma (kullanıcı kararı).
    private async Task<IActionResult> KapaliUrunYonlendir(string code, Guid platformId, CancellationToken ct)
    {
        var idSonuc = await mediator.Send(new GetProductIdByCodeQuery(code), ct);
        if (idSonuc.IsSuccess && idSonuc.Value is { } urunId)
        {
            var zincir = await mediator.Send(new GetProductChannelCategoryChainQuery(platformId, urunId), ct);
            if (zincir.IsSuccess && zincir.Value!.Count > 0)
                return RedirectPermanent("/" + zincir.Value[^1].Slug);
        }
        return RedirectPermanent("/");
    }
}
