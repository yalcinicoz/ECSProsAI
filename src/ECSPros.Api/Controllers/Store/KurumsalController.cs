using ECSPros.Api.Services;
using ECSPros.Cms.Application.Queries.GetStoreLegalPages;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// F1: Kurumsal sayfalar — misharix'in route şeması birebir (7 sayfa, kök URL'ler).
/// İçerik CMS'ten (PageType "corporate", kod "kurumsal-*"; rich_text section HTML'i;
/// 5 dk IMemoryCache — C8 sözleşme deseni); CMS boşsa partial'daki tasarım demo
/// içeriği render edilir. SSS (F2) ve İletişim (F3) kendi yapılarıyla bağlanana dek
/// tasarım demo içerikli kabuk olarak durur.
/// </summary>
public class KurumsalController(
    IMediator mediator, IStoreContext storeContext, IMemoryCache cache) : StorePageController
{
    [HttpGet("/hakkimizda")]
    public Task<IActionResult> Hakkimizda(CancellationToken ct) =>
        KurumsalSayfasi("Hakkımızda", "~/Views/ProjeElementleri/Kurumsal/_KurumsalHakkimizda.cshtml",
            "hakkimizda", "kurumsal-hakkimizda", ct);

    [HttpGet("/iletisim")]
    public Task<IActionResult> Iletisim(CancellationToken ct) =>
        KurumsalSayfasi("İletişim", "~/Views/ProjeElementleri/Kurumsal/_KurumsalIletisim.cshtml",
            "iletisim", null, ct); // F3: form bağlanana dek tasarım demo içeriği

    [HttpGet("/kargo-ve-teslimat")]
    public Task<IActionResult> KargoVeTeslimat(CancellationToken ct) =>
        KurumsalSayfasi("Kargo ve Teslimat", "~/Views/ProjeElementleri/Kurumsal/_KurumsalKargoTeslimat.cshtml",
            "kargo-teslimat", "kurumsal-kargo-teslimat", ct);

    [HttpGet("/iade-ve-degisim")]
    public Task<IActionResult> IadeVeDegisim(CancellationToken ct) =>
        KurumsalSayfasi("İade ve Değişim", "~/Views/ProjeElementleri/Kurumsal/_KurumsalIadeDegisim.cshtml",
            "iade-degisim", "kurumsal-iade-degisim", ct);

    [HttpGet("/sik-sorulan-sorular")]
    public Task<IActionResult> SikSorulanSorular(CancellationToken ct) =>
        KurumsalSayfasi("Sık Sorulan Sorular", "~/Views/ProjeElementleri/Kurumsal/_KurumsalSikSorulanSorular.cshtml",
            "sss", null, ct); // F2: SSS akordiyonu CMS soru/cevap yapısıyla bağlanacak

    [HttpGet("/kullanim-kosullari")]
    public Task<IActionResult> KullanimKosullari(CancellationToken ct) =>
        KurumsalSayfasi("Kullanım Koşulları", "~/Views/ProjeElementleri/Kurumsal/_KurumsalKullanimKosullari.cshtml",
            "kullanim-kosullari", "kurumsal-kullanim-kosullari", ct);

    [HttpGet("/gizlilik-ve-guvenlik")]
    public Task<IActionResult> GizlilikVeGuvenlik(CancellationToken ct) =>
        KurumsalSayfasi("Gizlilik ve Güvenlik", "~/Views/ProjeElementleri/Kurumsal/_KurumsalGizlilikGuvenlik.cshtml",
            "gizlilik-guvenlik", "kurumsal-gizlilik-guvenlik", ct);

    private async Task<IActionResult> KurumsalSayfasi(
        string baslik, string partial, string aktifMenu, string? cmsKodu, CancellationToken ct)
    {
        if (cmsKodu is not null)
        {
            var platform = await storeContext.GetPlatformAsync(ct);
            if (platform is not null)
            {
                var icerik = await cache.GetOrCreateAsync($"kurumsal:{platform.Id}:{cmsKodu}", async girdi =>
                {
                    girdi.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                    var sonuc = await mediator.Send(
                        new GetStoreLegalPagesQuery(platform.Id, new List<string> { cmsKodu }, "corporate"), ct);
                    return sonuc.IsSuccess ? sonuc.Value!.FirstOrDefault()?.BodyHtml : null;
                });
                ViewData["MsKurumsalIcerik"] = icerik;
            }
        }

        ViewData["Title"] = baslik;
        ViewData["MsKurumsalPartial"] = partial;
        ViewData["MsKurumsalAktifMenu"] = aktifMenu;
        return View("~/Views/Kurumsal/Sayfa.cshtml");
    }
}
