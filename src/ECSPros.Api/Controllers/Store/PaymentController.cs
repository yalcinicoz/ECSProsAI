using ECSPros.Api.Services.Store;
using ECSPros.Order.Application.Commands.PayTrPayment;
using ECSPros.Order.Application.Queries.GetOrderForPayment;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// PayTR Direct API ödeme uçları (2026-07-30). test_mode panel ayarından gelir
/// (Settings "testMode"; eksikse güvenli varsayılan test=açık). CANLI (test_mode=0) Direct API
/// ile gerçek kart bu sunucudan geçer → PCI-DSS SAQ D + PayTR Direct API onayı MÜŞTERİ
/// SORUMLULUĞUNDADIR (docs/paytr-entegrasyon-plani.md).
///
/// ★ KART VERİSİ: init gövdesindeki tam kart no/CVV YALNIZ PayTR'a iletmek için kullanılır;
///   loglanmaz, DB'ye/diske yazılmaz. Siparişe yalnız MASKELİ PAN geçer. CVV hiçbir yere yazılmaz.
/// </summary>
[ApiController]
[Route("api/store/payment/paytr")]
public class PaymentController(
    IMediator mediator,
    IPaymentSettingsProvider settingsProvider,
    PayTrDirectService paytr,
    ILogger<PaymentController> logger) : ControllerBase
{
    /// <summary>Adım 1: sipariş + kart → PayTR /odeme → 3D HTML. Kart alanları burada bırakılır.</summary>
    [HttpPost("init")]
    [AllowAnonymous] // misafir de ödeyebilir; siparişin sahipliği orderId + oturum akışıyla kurulur
    public async Task<IActionResult> Init([FromBody] PayTrInitRequest req, CancellationToken ct)
    {
        var ayar = await settingsProvider.GetAsync(ct);
        if (ayar is null)
            return BadRequest(new { success = false, error = "Ödeme sağlayıcı yapılandırılmamış (PayTR)." });

        var siparisSonuc = await mediator.Send(new GetOrderForPaymentQuery(req.OrderId), ct);
        if (siparisSonuc.IsFailure) return BadRequest(new { success = false, error = siparisSonuc.Error });
        var siparis = siparisSonuc.Value!;
        if (siparis.PaymentStatus == "paid")
            return BadRequest(new { success = false, error = "Bu sipariş zaten ödenmiş." });

        // Maskeli PAN'ı çıkar ve HEMEN siparişe yaz (tam PAN/CVV asla saklanmaz)
        var maskeli = PayTrDirectService.MaskePan(req.CardNumber ?? "");
        await mediator.Send(new PayTrPaymentBaslatCommand(siparis.OrderId, maskeli, TestMode: ayar.TestMode), ct);

        var ip = IstemciIp();
        // PayTR e-posta: üye e-postası yoksa test için türetilir (Direct API zorunlu alan)
        var email = string.IsNullOrWhiteSpace(req.Email)
            ? $"guest+{siparis.OrderNumber}@misharitalia.com"
            : req.Email!.Trim();
        const string paymentType = "card";
        const string installment = "0";
        var currency = siparis.CurrencyCode == "TRY" ? "TL" : siparis.CurrencyCode;
        // test_mode panel ayarından (DbPaymentSettingsProvider). Canlı=0 (gerçek kart), test=1.
        // Token hash'i test_mode'u içerir → hash ve form AYNI değeri kullanmalı.
        var testMode = ayar.TestMode ? "1" : "0";
        const string non3d = "0";      // 3D Secure akışı
        // PayTR Direct API (/odeme) payment_amount = TL ONDALIK (nokta, 2 hane) — örn "24.99".
        // (iFrame API kuruş/integer ister; Direct API DEĞİL — resmi postman koleksiyonuyla doğrulandı
        // 2026-07-30. Kuruş göndermek PayTR'de 100× yüksek tutar gösterip tahsil ediyordu.)
        // Token hash de bu ondalık değeri kullanır; basket zaten aynı TL tutarında.
        var amount = (siparis.TutarKurus / 100m)
            .ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        var token = PayTrDirectService.Adim1Token(
            ayar.MerchantId, ip, siparis.OrderNumber, email, amount,
            paymentType, installment, currency, testMode, non3d,
            ayar.MerchantKey, ayar.MerchantSalt);

        // Kök URL: merchant_ok/fail_url'e PayTR form POST'lar → HTTP olursa tarayıcı "not secure"
        // uyarısı verir (Cloudflare Flexible'da origin'e HTTP gelir, Request.Scheme=http). Genel
        // site her zaman HTTPS sunulduğundan localhost dışı host'larda https'e zorlanır.
        var scheme = Request.Host.Host is "localhost" or "127.0.0.1" ? Request.Scheme : "https";
        var kok = $"{scheme}://{Request.Host}";
        var sepet = PayTrDirectService.SepetBase64(new[]
        {
            ($"Siparis {siparis.OrderNumber}", siparis.TutarKurus / 100m, 1)
        });

        var form = new Dictionary<string, string>
        {
            ["merchant_id"] = ayar.MerchantId,
            ["user_ip"] = ip,
            ["merchant_oid"] = siparis.OrderNumber,
            ["email"] = email,
            ["payment_type"] = paymentType,
            ["payment_amount"] = amount,
            ["installment_count"] = installment,
            ["currency"] = currency,
            ["test_mode"] = testMode,
            ["non_3d"] = non3d,
            ["client_lang"] = "tr",
            ["paytr_token"] = token,
            // Kart alanları — YALNIZ bu istekte, sonra bellekten düşer
            ["cc_owner"] = req.CardOwner ?? "",
            ["card_number"] = new string((req.CardNumber ?? "").Where(char.IsDigit).ToArray()),
            ["expiry_month"] = (req.ExpiryMonth ?? "").PadLeft(2, '0'),
            ["expiry_year"] = req.ExpiryYear ?? "",
            ["cvv"] = req.Cvv ?? "",
            // Başarıda doğrudan sipariş özetine (ara sayfa yok); ?odeme=basarili özet
            // sayfasına "ödeme alındı" bilgisini bastırır (2026-07-30 kullanıcı kararı).
            ["merchant_ok_url"] = $"{kok}/siparis-tamamlandi?odeme=basarili",
            ["merchant_fail_url"] = $"{kok}/odeme-sonuc/basarisiz",
            ["user_name"] = siparis.AliciAd,
            ["user_address"] = "-",
            ["user_phone"] = siparis.AliciTelefon,
            ["user_basket"] = sepet,
            ["debug_on"] = testMode,   // yalnız test modunda ayrıntılı PayTR hata mesajı
        };

        var sonuc = await paytr.OdemeBaslatAsync(form, ct);
        if (!sonuc.Basarili || sonuc.Icerik is null)
            return StatusCode(502, new { success = false, error = "Ödeme sağlayıcıya ulaşılamadı. Lütfen tekrar deneyin." });

        // PayTR 3D akışında HTML döner (tarayıcıya basılıp bankaya yönlenir);
        // JSON dönerse (hata) aynen iletilir. İçerik kart verisi taşımaz.
        return Ok(new { success = true, html = sonuc.Icerik });
    }

    /// <summary>Adım 2: PayTR sunucu-sunucu bildirimi. Hash doğrulanır, sipariş sonucu
    /// uygulanır, düz "OK" dönülür (PayTR sözleşmesi). Kimlik doğrulaması YOK (PayTR'dan gelir),
    /// güvence hash'tir. Form-encoded gelir.</summary>
    [HttpPost("callback")]
    // 2026-07-30: mevcut canlı sitenin PayTR panelinde tanımlı bildirim URL'i korunuyor —
    // kullanıcı paneli değiştirmek istemiyor. PayTR bu absolut yola POST eder; aynı handler.
    // Bu yol /api/store/ altında OLMADIĞINDAN vitrin kapısına zaten takılmaz. Tek template
    // yeterli — ASP.NET Core trailing slash'ı ('.../PayTrBildirim/') otomatik eşler (iki
    // varyant yazmak AmbiguousMatchException üretiyordu).
    [HttpPost("/CheckoutPayment/PayTrBildirim")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Callback([FromForm] PayTrCallbackForm form, CancellationToken ct)
    {
        var ayar = await settingsProvider.GetAsync(ct);
        if (ayar is null) { logger.LogWarning("PayTR callback: ayar yok."); return Content("OK"); }

        var gecerli = PayTrDirectService.CallbackHashGecerli(
            form.merchant_oid ?? "", form.status ?? "", form.total_amount ?? "",
            form.hash ?? "", ayar.MerchantKey, ayar.MerchantSalt);
        if (!gecerli)
        {
            logger.LogWarning("PayTR callback: hash doğrulanamadı (oid gövdede).");
            return Content("PAYTR notification failed: bad hash");
        }

        var basarili = form.status == "success";
        await mediator.Send(new PayTrCallbackUygulaCommand(
            form.merchant_oid ?? "", basarili, form.failed_reason_msg), ct);
        // Sonuç ne olursa olsun PayTR'a "OK" — aksi halde PayTR tekrar dener (idempotent handler)
        return Content("OK");
    }

    private string IstemciIp()
    {
        var cf = Request.Headers["CF-Connecting-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(cf)) return cf.Trim();
        var xff = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(xff)) return xff.Split(',')[0].Trim();
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }
}

/// <summary>Init gövdesi. Kart alanları YALNIZ PayTR'a iletmek için — hiçbir yerde saklanmaz.</summary>
public record PayTrInitRequest(
    Guid OrderId,
    string? Email,
    string? CardOwner,
    string? CardNumber,
    string? ExpiryMonth,
    string? ExpiryYear,
    string? Cvv);

public record PayTrCallbackForm
{
    public string? merchant_oid { get; init; }
    public string? status { get; init; }
    public string? total_amount { get; init; }
    public string? hash { get; init; }
    public string? failed_reason_msg { get; init; }
    public string? payment_type { get; init; }
}
