using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// Storefront hata sayfaları (2026-07-30). UseStatusCodePagesWithReExecute("/hata/{0}")
/// buraya yönlendirir; StorePageController tabanı sayesinde 404 sayfası tam site
/// kabuğuyla (nav + footer + duyuru barı) render edilir. Doğrudan /hata/404 ziyaretinde
/// de gerçek durum kodu döner (SEO: soft-404 oluşmaz).
/// </summary>
public class HataController : StorePageController
{
    [HttpGet("/hata/{kod:int}")]
    public IActionResult Kod(int kod)
    {
        // Yalnız anlamlı kodlar; tanımsız kod istekleri 404 muamelesi görür
        if (kod is not (404 or 403 or 410 or 500)) kod = 404;
        Response.StatusCode = kod;
        ViewData["Title"] = kod == 404 ? "Sayfa Bulunamadı" : "Bir Sorun Oluştu";
        ViewData["Robots"] = "noindex, follow";
        return View("~/Views/Shared/MsHata.cshtml", kod);
    }
}
