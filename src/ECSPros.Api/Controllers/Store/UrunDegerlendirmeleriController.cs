using ECSPros.Api.Models.Store;
using ECSPros.Api.Services;
using ECSPros.Catalog.Application.Queries.GetStoreProducts;
using ECSPros.Storefront.Application.Queries.GetProductReviewSummary;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// H9: Ürün değerlendirmeleri sayfası — ÇEKİRDEK PORT (K14): ürün özeti + tüm yorumlar +
/// puan filtresi + sıralama + arama + sayfalama (infinite) + kriter modalı. Tasarımın
/// üründen bağımsız demo rotası ürün bazlıya çevrildi (/urun-degerlendirmeleri/{code});
/// giriş: detaydaki "N Değerlendirme" linki. Üyeye özel durum sekmeleri bu sayfada YOK
/// (Hesabım→Yorumlarım E7'de); verisiz bloklar (AI özeti, fotoğraflı, konu/beden filtreleri)
/// partial'da @if gizli.
/// </summary>
public class UrunDegerlendirmeleriController(
    IMediator mediator, IStoreContext storeContext) : StorePageController
{
    [HttpGet("/urun-degerlendirmeleri/{code}")]
    public async Task<IActionResult> Index(string code, CancellationToken ct)
    {
        var platform = await storeContext.GetPlatformAsync(ct);
        if (platform is null) return NotFound();

        var urunSonucu = await mediator.Send(
            new GetStoreProductsQuery(platform.Id, ProductCodes: [code], PageSize: 1), ct);
        var kart = urunSonucu.IsSuccess ? urunSonucu.Value!.Items.FirstOrDefault() : null;
        if (kart is null) return NotFound();

        var ozetSonucu = await mediator.Send(new GetProductReviewSummaryQuery(platform.Id, code), ct);
        var ozet = ozetSonucu.IsSuccess
            ? ozetSonucu.Value!
            : new ProductReviewSummaryDto(0, 0, 0, Enumerable.Range(1, 5).ToDictionary(p => p, _ => 0));

        var ad = kart.NameI18n.GetValueOrDefault("tr") ?? kart.Code;
        ViewData["Title"] = $"{ad} — Ürün Değerlendirmeleri";
        ViewData["MsDegerlendirmeVm"] = new UrunDegerlendirmeleriVm(
            kart.Code, ad, kart.MinPrice, kart.CompareAtPrice, kart.MainImageUrl,
            platform.Id, ozet.Average, ozet.TotalCount, ozet.TextCount, ozet.RatingCounts);

        return View("~/Views/UrunDegerlendirmeleri/Index.cshtml");
    }
}
