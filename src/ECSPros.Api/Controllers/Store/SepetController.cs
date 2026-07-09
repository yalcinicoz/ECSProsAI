using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// Sepet sayfası (C1). Sepet istemci-durumludur (localStorage ecspros_sid/ecspros_cart) —
/// satırlar sayfa içi script'le GET /api/store/cart'tan render edilir (B5 mini sepet deseni);
/// SSR yalnız kabuğu verir. Teslimat/ödeme sayfaları C4-C5'te gelecek.
/// </summary>
public class SepetController(IConfiguration configuration) : StorePageController
{
    // C7: TCKN eşiği — sayfa script'leri banner/guard için okur (asıl güvence checkout'ta).
    // Sözleşmeler (ViewData["MsSozlesmeler"]) D3'ten beri tabanda yüklenir (nav belge modalı
    // her sayfada onlara muhtaç) — buradaki C8 yüklemesi oraya taşındı.
    public override async Task OnActionExecutionAsync(
        Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context,
        Microsoft.AspNetCore.Mvc.Filters.ActionExecutionDelegate next)
    {
        ViewData["MsTcknEsik"] = configuration.GetValue<decimal>("Store:TcknThreshold", 13000m);
        await base.OnActionExecutionAsync(context, next);
    }

    [HttpGet("/sepet")]
    public IActionResult Index() => View("~/Views/Sepet/Index.cshtml");

    /// <summary>C4-b: teslimat adımı — adres seçimi üye gerektirir (sayfa script'i yönetir).</summary>
    [HttpGet("/teslimat")]
    public IActionResult Teslimat() => View("~/Views/Sepet/Teslimat.cshtml");

    /// <summary>C5: ödeme adımı (test modu — K2; tahsilat mock, sipariş C10 checkout'uyla oluşur).</summary>
    [HttpGet("/odeme")]
    public IActionResult Odeme() => View("~/Views/Sepet/Odeme.cshtml");

    /// <summary>C10: sipariş tamamlandı — içerik sessionStorage msSiparisSonucu'ndan.</summary>
    [HttpGet("/siparis-tamamlandi")]
    public IActionResult SiparisTamamlandi() => View("~/Views/Sepet/SiparisTamamlandi.cshtml");
}
