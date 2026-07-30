using ECSPros.Api.Services;
using ECSPros.Api.Services.Store;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// Sepet sayfası (C1). Sepet istemci-durumludur (localStorage ecspros_sid/ecspros_cart) —
/// satırlar sayfa içi script'le GET /api/store/cart'tan render edilir (B5 mini sepet deseni);
/// SSR yalnız kabuğu verir. Teslimat/ödeme sayfaları C4-C5'te gelecek.
/// </summary>
public class SepetController(
    IConfiguration configuration, IPaymentSettingsProvider paymentSettings) : StorePageController
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

    /// <summary>PayTR 3D dönüş sayfaları (2026-07-30). Sonucun DOĞRUSU callback'te belirlenir;
    /// bu sayfalar yalnız kullanıcıya bilgi verir (başarıda /siparis-tamamlandi'ya köprüler,
    /// başarısızda /odeme'ye döndürür). PayTR bu URL'lere POST ile döner.</summary>
    [HttpGet("/odeme-sonuc/basarili")]
    [HttpPost("/odeme-sonuc/basarili")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> OdemeSonucBasarili(CancellationToken ct)
        => await OdemeSonucGoster(true, ct);

    [HttpGet("/odeme-sonuc/basarisiz")]
    [HttpPost("/odeme-sonuc/basarisiz")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> OdemeSonucBasarisiz(CancellationToken ct)
        => await OdemeSonucGoster(false, ct);

    // "Test modu — gerçek tahsilat yapılmaz." ibaresi YALNIZ gerçek test modunda gösterilsin
    // (canlıda yanıltıcıydı) — PayTR ayarındaki TestMode'a bağlı.
    private async Task<IActionResult> OdemeSonucGoster(bool basarili, CancellationToken ct)
    {
        var ayar = await paymentSettings.GetAsync(ct);
        ViewData["MsPayTrTestModu"] = ayar?.TestMode == true;
        return View("~/Views/Sepet/OdemeSonuc.cshtml", basarili);
    }

    /// <summary>C10+H2: sipariş tamamlandı — içerik sessionStorage msSiparisSonucu'ndan;
    /// Kargo Bilgisi bölümü H2'de açıldı (firma adı platformun aktif kargo anlaşmasından
    /// SSR — gönderi henüz yokken durum "Hazırlanıyor"; anlaşma yoksa firma satırı gizli).</summary>
    // PayTR başarı dönüşü bu sayfaya POST ile gelir (merchant_ok_url) — GET+POST kabul,
    // antiforgery muaf (2026-07-30). Normal akış GET; içerik sessionStorage'dan render edilir.
    [HttpGet("/siparis-tamamlandi")]
    [HttpPost("/siparis-tamamlandi")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SiparisTamamlandi(
        [FromServices] IMediator mediator, [FromServices] IStoreContext storeContext, CancellationToken ct)
    {
        var platform = await storeContext.GetPlatformAsync(ct);
        if (platform is not null)
        {
            var firma = await mediator.Send(
                new ECSPros.Core.Application.Queries.GetPlatformActiveCargoCarrier
                    .GetPlatformActiveCargoCarrierQuery(platform.Id), ct);
            ViewData["MsKargoFirmaAdi"] = firma.IsSuccess ? firma.Value?.Name : null;
        }

        return View("~/Views/Sepet/SiparisTamamlandi.cshtml");
    }
}
