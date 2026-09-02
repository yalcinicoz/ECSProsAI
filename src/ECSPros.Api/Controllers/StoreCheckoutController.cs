using ECSPros.Order.Application.Commands.Checkout;
using ECSPros.Promotion.Application.Queries.ValidateCoupon;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/store/checkout")]
[Authorize(Policy = "MemberOnly")]
public class StoreCheckoutController(
    IMediator mediator,
    IConfiguration configuration,
    ECSPros.Api.Services.Store.IOrderConfirmationService orderConfirmations) : ControllerBase
{
    /// <summary>C3: sepette kupon kodu doğrulama — misafir de deneyebilir (üye kuponu
    /// koşulları MemberId üzerinden değerlendirilir); kullanım kaydı checkout'ta (C10).</summary>
    [HttpPost("coupon/validate")]
    [AllowAnonymous]
    [EnableRateLimiting("store-sensitive")] // kupon kodu taramasına fren (2026-07-23)
    public async Task<IActionResult> ValidateCoupon([FromBody] StoreCouponValidateRequest req, CancellationToken ct)
    {
        Guid? memberId = null;
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (sub != null && Guid.TryParse(sub, out var mid)) memberId = mid;

        var result = await mediator.Send(new ValidateCouponQuery(req.Code, req.CartTotal, memberId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost]
    [AllowAnonymous] // 2026-07-22: misafir checkout — üye claim'i varsa bağlanır, yoksa misafir siparişi
    public async Task<IActionResult> Checkout([FromBody] StoreCheckoutRequest req, CancellationToken ct)
    {
        Guid? memberId = null;
        if (User.FindFirst("type")?.Value == "member")
        {
            var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (sub != null && Guid.TryParse(sub, out var mid)) memberId = mid;
        }

        // C7 (K9): eşik üzeri siparişte algoritma-doğrulanmış TCKN zorunlu (asıl güvence
        // burada — sayfa tarafı yalnız kullanıcıyı modala yönlendirir). Eşik config'ten.
        // Misafirin TCKN doğrulaması yoktur — eşik üzeri misafir siparişi üyelik ister.
        var tcknEsik = configuration.GetValue<decimal>("Store:TcknThreshold", 13000m);
        var araToplam = req.Items.Sum(i => i.Quantity * i.UnitPrice);
        if (araToplam >= tcknEsik)
        {
            if (memberId is null)
                return BadRequest(new { success = false, error = $"{tcknEsik.ToString("N0", new System.Globalization.CultureInfo("tr-TR"))} TL ve üzeri siparişler için üye girişi ve TCKN doğrulaması gereklidir.", tcknRequired = true });
            var uye = await mediator.Send(new ECSPros.Crm.Application.Queries.GetMemberDetail.GetMemberDetailQuery(memberId.Value), ct);
            if (uye.IsFailure || !uye.Value!.IdentityVerified)
                return BadRequest(new { success = false, error = $"{tcknEsik.ToString("N0", new System.Globalization.CultureInfo("tr-TR"))} TL ve üzeri siparişlerde TCKN doğrulaması zorunludur. Lütfen kimlik doğrulamasını tamamlayın.", tcknRequired = true });
        }

        // C8: onaylanan sözleşmelerin kabul kaydı — istemci yalnız kod gönderir; başlık ve
        // metin sürümü (ContentUpdatedAt) sunucuda CMS'ten çözülür ki kayıt oynanamaz olsun.
        List<AcceptedContract>? kabulKayitlari = null;
        if (req.AcceptedContracts is { Count: > 0 })
        {
            var sozlesmeler = await mediator.Send(
                new ECSPros.Cms.Application.Queries.GetStoreLegalPages.GetStoreLegalPagesQuery(
                    req.FirmPlatformId, req.AcceptedContracts), ct);
            if (sozlesmeler.IsSuccess)
                kabulKayitlari = sozlesmeler.Value!
                    .Select(s => new AcceptedContract(s.Code, s.Title, DateTime.UtcNow, s.ContentUpdatedAt))
                    .ToList();
        }

        var result = await mediator.Send(new CheckoutCommand(
            req.FirmPlatformId, memberId, req.CurrencyCode,
            req.ShippingRecipientName, req.ShippingRecipientPhone,
            req.ShippingCountryId, req.ShippingCityId, req.ShippingDistrictId,
            req.ShippingAddressLine, req.ShippingPostalCode, req.ShippingDeliveryNotes,
            req.ShippingNeighborhoodId,
            req.BillingSameAsShipping, req.BillingRecipientName,
            req.BillingTaxOffice, req.BillingTaxNumber, req.BillingCompanyName,
            req.BillingCountryId, req.BillingCityId, req.BillingDistrictId, req.BillingAddressLine,
            req.Items.Select(i => new CheckoutItem(i.VariantId, i.Sku, i.ProductName, i.VariantInfo ?? "", i.Quantity, i.UnitPrice)).ToList(),
            req.CustomerNotes, req.CartId, kabulKayitlari,
            req.RequestedCargoIntegrationId, req.RequestedCargoName,
            req.PaymentMethod, req.CouponDiscount, req.CouponCode), ct);

        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });

        // C10: kupon kullanım kaydı (C3'te yalnız doğrulanmıştı) — sipariş oluştuktan sonra.
        // Misafirde kayıt atlanır: CouponUsage.MemberId zorunlu (üye kuponları misafire
        // zaten doğrulanmaz; genel kuponun misafir kullanımı sayaca yazılmaz — bilinen sınır).
        // 2026-08-27 (9.4): kayıt SUNUCU doğrulamalı kupon bilgisiyle atılır (istemci
        // CouponId/CouponDiscount alanları yok sayılır — sahte kayıt/keyfî tutar kapatıldı).
        if (memberId is { } uyeKimlik && result.Value!.CouponId is { } kuponId && result.Value!.CouponDiscount > 0)
            await mediator.Send(new ECSPros.Promotion.Application.Commands.UseCoupon.UseCouponCommand(
                kuponId, uyeKimlik, result.Value!.OrderId, result.Value!.CouponDiscount), ct);

        // O2 (2026-08-04, akış değişti): onay YENİ SİTEDE alınır — kapıda siparişte
        // (politika gerektiriyorsa) onay SMS/e-postası gönderilir; sipariş onaylanınca
        // OrderConfirmedEvent eskiye "Hazırlanıyor" olarak taşır. Gönderim hata-güvenli.
        if (req.PaymentMethod is "kapida-nakit" or "kapida-kart")
            await orderConfirmations.SiparisSonrasiBaslatAsync(result.Value!.OrderId, ct);

        // 2026-07-30: orderNumber da döner — onay ekranı insan okunur numarayı doğrudan
        // gösterir (misafirde üye-listesi geri araması yoktu, GUID görünüyordu).
        // İE-2 Faz B-3: tarayıcı bağlamı + consent siparişe bağlanır (sonradan onaylanınca
        // server-side purchase bu bağlamla gider); purchaseAt=created ise event hemen yazılır.
        // Hata-güvenli — checkout yanıtını asla etkilemez.
        await TakipKaydetAsync(result.Value!.OrderId, req.FirmPlatformId, memberId, req, ct);

        return Ok(new { success = true, data = new { orderId = result.Value!.OrderId, orderNumber = result.Value.OrderNumber } });
    }

    private async Task TakipKaydetAsync(Guid orderId, Guid firmPlatformId, Guid? memberId, StoreCheckoutRequest req, CancellationToken ct)
    {
        try
        {
            var recorder = HttpContext.RequestServices.GetRequiredService<ECSPros.Api.Services.Tracking.ITrackingOrderContextRecorder>();
            await recorder.RecordAsync(orderId, firmPlatformId, HttpContext, memberId, null, req.ShippingRecipientPhone, ct);

            var builder = HttpContext.RequestServices.GetRequiredService<ECSPros.Api.Services.Tracking.IOrderTrackingEventBuilder>();
            if (await builder.PurchaseAtAsync(firmPlatformId, ct) == "created")
            {
                var ev = await builder.BuildOrderCompletedAsync(orderId, ct);
                if (ev is not null)
                    await HttpContext.RequestServices.GetRequiredService<ECSPros.Shared.Contracts.Tracking.ICommerceEventPublisher>().PublishAsync(ev, ct);
            }
        }
        catch (Exception ex)
        {
            HttpContext.RequestServices.GetRequiredService<ILogger<StoreCheckoutController>>()
                .LogWarning(ex, "Checkout takip kaydı başarısız (orderId={OrderId})", orderId);
        }
    }
}

public record StoreCheckoutRequest(
    Guid FirmPlatformId,
    string CurrencyCode,
    string ShippingRecipientName,
    string ShippingRecipientPhone,
    Guid ShippingCountryId,
    Guid ShippingCityId,
    Guid ShippingDistrictId,
    string ShippingAddressLine,
    string? ShippingPostalCode,
    string? ShippingDeliveryNotes,
    // 2026-09-02: mahalle — "Kargoya Ver" önerisinin mahalle kurallı ayağı için siparişe yazılır
    Guid? ShippingNeighborhoodId,
    bool BillingSameAsShipping,
    string? BillingRecipientName,
    string? BillingTaxOffice,
    string? BillingTaxNumber,
    string? BillingCompanyName,
    Guid? BillingCountryId,
    Guid? BillingCityId,
    Guid? BillingDistrictId,
    string? BillingAddressLine,
    List<StoreCheckoutItemRequest> Items,
    string? CustomerNotes = null,
    Guid? CartId = null,
    Guid? CouponId = null,           // C10: uygulanan kuponun kullanım kaydı için
    decimal? CouponDiscount = null,  // 2026-08-27: artık yalnız görüntü — sipariş hesabında yok sayılır
    string? CouponCode = null,       // 2026-08-27 (9.4 güvenlik): sunucu bu kodu yeniden doğrular, tutarı kendisi hesaplar
    List<string>? AcceptedContracts = null, // C8: onaylanan sözleşme kodları (kayıt sunucuda çözülür)
    Guid? RequestedCargoIntegrationId = null, // 2026-07-22: müşterinin kargo tercihi
    string? RequestedCargoName = null,
    string? PaymentMethod = null);   // 2026-07-30: kart|kapida-nakit|kapida-kart — kapıda bedeli sunucuda eklenir

public record StoreCheckoutItemRequest(
    Guid VariantId,
    string Sku,
    string ProductName,
    string? VariantInfo,     // seçeneksiz üründe null gelir — zorunlu olursa model doğrulaması 400 üretir
    int Quantity,
    decimal UnitPrice);

public record StoreCouponValidateRequest(string Code, decimal CartTotal);
