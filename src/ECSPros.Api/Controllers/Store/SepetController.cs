using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// Sepet sayfası (C1). Sepet istemci-durumludur (localStorage ecspros_sid/ecspros_cart) —
/// satırlar sayfa içi script'le GET /api/store/cart'tan render edilir (B5 mini sepet deseni);
/// SSR yalnız kabuğu verir. Teslimat/ödeme sayfaları C4-C5'te gelecek.
/// </summary>
public class SepetController : StorePageController
{
    [HttpGet("/sepet")]
    public IActionResult Index() => View("~/Views/Sepet/Index.cshtml");

    /// <summary>C4-b: teslimat adımı — adres seçimi üye gerektirir (sayfa script'i yönetir).</summary>
    [HttpGet("/teslimat")]
    public IActionResult Teslimat() => View("~/Views/Sepet/Teslimat.cshtml");
}
