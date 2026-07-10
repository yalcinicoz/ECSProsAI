using ECSPros.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// Hesabım çerçevesi (E1) — misharix'in çift route şeması (/Hesabim/... + kebab-case
/// kısa yol) ve tek "Sayfa" view'ına partial adı geçiren kalıbı birebir. Sayfalar
/// üye-özel: SSR kimlik (D1 cookie) yoksa köke yönlendirilir (canlıda cookie'siz
/// oturum kalmadı — üyelik B4'te bu akışla açıldı). Partial'lar E2-E13'te teker teker
/// gerçek veriye bağlanır; o güne dek tasarımın demo içeriği render olur.
/// </summary>
public class HesabimController : StorePageController
{
    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var uye = await context.HttpContext.RequestServices
            .GetRequiredService<IStoreMemberSession>()
            .MevcutUyeAsync(context.HttpContext);
        if (uye is null)
        {
            context.Result = Redirect("/");
            return;
        }

        await base.OnActionExecutionAsync(context, next);
    }

    [HttpGet("/Hesabim")]
    [HttpGet("/hesabim-varsayilan")]
    public IActionResult Index() =>
        HesabimSayfasi("Hesabım Varsayılan", "~/Views/ProjeElementleri/Hesabim/_HesabimVarsayilan.cshtml");

    [HttpGet("/Hesabim/UyelikBilgilerim")]
    [HttpGet("/uyelik-bilgilerim")]
    public IActionResult UyelikBilgilerim() =>
        HesabimSayfasi("Üyelik Bilgilerim", "~/Views/ProjeElementleri/Hesabim/_HesabimUyelikBilgilerim.cshtml");

    [HttpGet("/Hesabim/Adreslerim")]
    [HttpGet("/adreslerim")]
    public IActionResult Adreslerim() =>
        HesabimSayfasi("Adreslerim", "~/Views/ProjeElementleri/Hesabim/_HesabimAdreslerim.cshtml");

    [HttpGet("/Hesabim/Siparislerim")]
    [HttpGet("/siparislerim")]
    public IActionResult Siparislerim() =>
        HesabimSayfasi("Siparişlerim", "~/Views/ProjeElementleri/Hesabim/_HesabimSiparislerim.cshtml");

    [HttpGet("/Hesabim/TekrarSatinAl")]
    [HttpGet("/tekrar-satin-al")]
    public IActionResult TekrarSatinAl() =>
        HesabimSayfasi("Tekrar Satın Al", "~/Views/ProjeElementleri/Hesabim/_HesabimTekrarSatinAl.cshtml");

    [HttpGet("/Hesabim/OncedenGezdiklerim")]
    [HttpGet("/onceden-gezdiklerim")]
    public IActionResult OncedenGezdiklerim() =>
        HesabimSayfasi("Önceden Gezdiklerim", "~/Views/ProjeElementleri/Hesabim/_HesabimOncedenGezdiklerim.cshtml");

    [HttpGet("/Hesabim/Iadelerim")]
    [HttpGet("/iadelerim")]
    public IActionResult Iadelerim() =>
        HesabimSayfasi("İadelerim", "~/Views/ProjeElementleri/Hesabim/_HesabimIadelerim.cshtml");

    [HttpGet("/Hesabim/Yorumlarim")]
    [HttpGet("/yorumlarim")]
    public IActionResult Yorumlarim() =>
        HesabimSayfasi("Yorumlarım", "~/Views/ProjeElementleri/Hesabim/_HesabimYorumlarim.cshtml");

    [HttpGet("/Favorilerim")]
    [HttpGet("/Hesabim/Favorilerim")]
    public IActionResult Favorilerim() =>
        HesabimSayfasi("Favorilerim", "~/Views/ProjeElementleri/Hesabim/_HesabimFavorilerim.cshtml");

    [HttpGet("/Hesabim/Koleksiyonlarim")]
    [HttpGet("/koleksiyonlarim")]
    public IActionResult Koleksiyonlarim() =>
        HesabimSayfasi("Koleksiyonlarım", "~/Views/ProjeElementleri/Hesabim/_HesabimKoleksiyonlarim.cshtml");

    [HttpGet("/Hesabim/IndirimKuponlarim")]
    [HttpGet("/indirim-kuponlarim")]
    public IActionResult IndirimKuponlarim() =>
        HesabimSayfasi("İndirim Kuponlarım", "~/Views/ProjeElementleri/Hesabim/_HesabimIndirimKuponlarim.cshtml");

    [HttpGet("/Hesabim/FavoriAramalarim")]
    [HttpGet("/favori-aramalarim")]
    public IActionResult FavoriAramalarim() =>
        HesabimSayfasi("Favori Aramalarım", "~/Views/ProjeElementleri/Hesabim/_HesabimFavoriAramalarim.cshtml");

    private IActionResult HesabimSayfasi(string baslik, string partial)
    {
        ViewData["Title"] = baslik;
        ViewData["MsHesabimPartial"] = partial;
        return View("~/Views/Hesabim/Sayfa.cshtml");
    }
}
