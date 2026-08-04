using ECSPros.Api.Services.Store;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// O3 (2026-08-04): SMS/e-posta onay linki yüzeyi. GET /o/{token} SSR sayfası —
/// geçerli token siparişi onaylar; süresi dolmuşsa yeniden gönderme sunar. Token
/// tahmini pratik değil (128-bit) ama uç yine de hız sınırlı.
/// </summary>
[AllowAnonymous]
public class SiparisOnayController(IOrderConfirmationService onay) : Controller
{
    [HttpGet("/o/{token}")]
    [EnableRateLimiting("store-sensitive")]
    public async Task<IActionResult> Onayla(string token, CancellationToken ct)
    {
        var sonuc = await onay.TokenlaOnaylaAsync(token, ct);
        ViewData["Title"] = "Sipariş Onayı";
        ViewData["Robots"] = "noindex, nofollow";
        ViewData["MsOnayDurum"] = sonuc.Durum;
        ViewData["MsOnaySiparisNo"] = sonuc.OrderNumber;
        ViewData["MsOnayToken"] = token;
        return View("~/Views/Home/SiparisOnay.cshtml");
    }

    [HttpPost("/o/{token}/yenile")]
    [EnableRateLimiting("store-sensitive")]
    public async Task<IActionResult> Yenile(string token, CancellationToken ct)
    {
        var gonderildi = await onay.YenidenGonderAsync(token, ct);
        ViewData["Title"] = "Sipariş Onayı";
        ViewData["Robots"] = "noindex, nofollow";
        ViewData["MsOnayDurum"] = gonderildi ? "yeniden-gonderildi" : "yenileme-basarisiz";
        return View("~/Views/Home/SiparisOnay.cshtml");
    }
}
