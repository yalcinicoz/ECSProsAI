using ECSPros.Crm.Application.Commands.AddMemberAddress;
using ECSPros.Crm.Application.Commands.DeleteMemberAddress;
using ECSPros.Crm.Application.Commands.UpdateMemberProfile;
using ECSPros.Crm.Application.Queries.GetMemberAddresses;
using ECSPros.Crm.Application.Queries.GetMemberDetail;
using ECSPros.Crm.Application.Queries.GetMemberLoyalty;
using ECSPros.Crm.Application.Queries.GetMemberWallet;
using ECSPros.Order.Application.Queries.GetOrderDetail;
using ECSPros.Order.Application.Queries.GetOrders;
using ECSPros.Order.Application.Queries.GetReturnDetail;
using ECSPros.Order.Application.Queries.GetReturns;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/store/account")]
[Authorize(Policy = "MemberOnly")]
public class StoreAccountController(
    IMediator mediator,
    ECSPros.Api.Services.Legacy.ILegacyOrderQueue legacyOrderQueue,
    ECSPros.Api.Services.Store.IOrderConfirmationService orderConfirmations) : ControllerBase
{
    private Guid GetMemberId() =>
        Guid.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!);

    // Profile
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var result = await mediator.Send(new GetMemberDetailQuery(GetMemberId()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateMemberProfileCommand(
            GetMemberId(), req.FirstName, req.LastName, req.Phone, req.Gender, req.BirthDate, req.CityId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>E2: duyuru tercihleri (kampanya e-posta/SMS/telefon izinleri).</summary>
    [HttpPut("marketing-consents")]
    public async Task<IActionResult> UpdateMarketingConsents([FromBody] MarketingConsentsRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ECSPros.Crm.Application.Commands.UpdateMemberMarketingConsents.UpdateMemberMarketingConsentsCommand(
                GetMemberId(), req.Email, req.Sms, req.Phone), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>E2: Aktif Cihazlar + Giriş Geçmişi — üyenin son oturumları.</summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken ct)
    {
        var result = await mediator.Send(
            new ECSPros.Crm.Application.Queries.GetMemberSessions.GetMemberSessionsQuery(GetMemberId()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    // Addresses
    [HttpGet("addresses")]
    public async Task<IActionResult> GetAddresses(CancellationToken ct)
    {
        var result = await mediator.Send(new GetMemberAddressesQuery(GetMemberId()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("addresses")]
    public async Task<IActionResult> AddAddress([FromBody] AddMemberAddressCommand req, CancellationToken ct)
    {
        var cmd = req with { MemberId = GetMemberId() };
        var result = await mediator.Send(cmd, ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { addressId = result.Value } });
    }

    /// <summary>E3: adres güncelleme (C4'te ertelenmişti).</summary>
    [HttpPut("addresses/{addressId}")]
    public async Task<IActionResult> UpdateAddress(Guid addressId, [FromBody] ECSPros.Crm.Application.Commands.UpdateMemberAddress.UpdateMemberAddressCommand req, CancellationToken ct)
    {
        var cmd = req with { MemberId = GetMemberId(), AddressId = addressId };
        var result = await mediator.Send(cmd, ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>E3: adresi varsayılan yap — önceki varsayılanlar düşer.</summary>
    [HttpPost("addresses/{addressId}/default")]
    public async Task<IActionResult> SetDefaultAddress(Guid addressId, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ECSPros.Crm.Application.Commands.SetDefaultMemberAddress.SetDefaultMemberAddressCommand(GetMemberId(), addressId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpDelete("addresses/{addressId}")]
    public async Task<IActionResult> DeleteAddress(Guid addressId, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteMemberAddressCommand(GetMemberId(), addressId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>B-019: üyenin KENDİ hesabını kapatması — soft delete + tüm oturumlar iptal;
    /// SSR cookie'si de silinir. Sonrasında aynı bilgilerle giriş yapılamaz.</summary>
    [HttpDelete("")]
    public async Task<IActionResult> DeleteAccount(CancellationToken ct)
    {
        var result = await mediator.Send(
            new ECSPros.Crm.Application.Commands.DeleteMemberAccount.DeleteMemberAccountCommand(GetMemberId()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        Response.Cookies.Delete(ECSPros.Api.Services.StoreMemberSession.CookieAdi);
        return Ok(new { success = true });
    }

    // C7 (K9): TCKN kaydı — format + kontrol basamağı algoritması sunucuda doğrulanır
    [HttpPost("identity")]
    public async Task<IActionResult> SetIdentity([FromBody] SetIdentityRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new ECSPros.Crm.Application.Commands.SetMemberIdentity.SetMemberIdentityCommand(
            GetMemberId(), req.IdentityNumber, req.BirthDate), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // Orders
    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders([FromQuery] string? status, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetOrdersQuery(status, GetMemberId(), null, page, 20), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("orders/{orderId}")]
    public async Task<IActionResult> GetOrder(Guid orderId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetOrderDetailQuery(orderId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        if (result.Value!.MemberId != GetMemberId()) // E8: sahiplik denetimi — başkasının siparişi sızmaz
            return NotFound(new { success = false, error = "Sipariş bulunamadı." });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>O3 (2026-08-04): üye kendi bekleyen siparişini Siparişlerim'den onaylar —
    /// onay eskiye 'Hazırlanıyor' olarak taşınır (OrderConfirmedEvent → legacy kuyruk).</summary>
    [HttpPost("orders/{orderId}/confirm")]
    public async Task<IActionResult> ConfirmOrder(Guid orderId, CancellationToken ct)
    {
        var detay = await mediator.Send(new GetOrderDetailQuery(orderId), ct);
        if (detay.IsFailure || detay.Value!.MemberId != GetMemberId())
            return NotFound(new { success = false, error = "Sipariş bulunamadı." });

        var sonuc = await orderConfirmations.SiteOnaylaAsync(orderId, ct);
        if (sonuc.Durum != "onaylandi" && sonuc.Durum != "zaten-onayli")
            return BadRequest(new { success = false, error = "Sipariş onaylanamadı (durumu uygun değil)." });
        return Ok(new { success = true, data = sonuc.Durum });
    }

    /// <summary>F3 (2026-08-04): müşteri sipariş iptali — yalnız kendi siparişi ve
    /// pending/confirmed durumda (durum makinesi Cancel kuralı). İptal eski sisteme
    /// outbox 'cancel' işiyle yansıtılır (kanal eskiye bağlıysa); rezervasyonlar
    /// OrderCancelledEvent ile serbest kalır.</summary>
    [HttpPost("orders/{orderId}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid orderId, CancellationToken ct)
    {
        var detay = await mediator.Send(new GetOrderDetailQuery(orderId), ct);
        if (detay.IsFailure || detay.Value!.MemberId != GetMemberId())
            return NotFound(new { success = false, error = "Sipariş bulunamadı." });

        var sonuc = await mediator.Send(new ECSPros.Order.Application.Commands.CancelOrder.CancelOrderCommand(
            orderId, GetMemberId(), "Müşteri iptali (site)"), ct);
        if (sonuc.IsFailure) return BadRequest(new { success = false, error = sonuc.Error });

        await legacyOrderQueue.EnqueueAsync(orderId, detay.Value.FirmPlatformId, "cancel", ct);
        return Ok(new { success = true, data = true });
    }

    /// <summary>H1: Siparişin faturaları — entegratör URL'i sızmaz, yalnız hasIntegratorPdf
    /// bayrağı döner (PDF aşağıdaki proxy'den alınır; mobil app aynı endpoint'leri kullanır).</summary>
    [HttpGet("orders/{orderId}/invoices")]
    public async Task<IActionResult> GetOrderInvoices(Guid orderId, CancellationToken ct)
    {
        var siparis = await mediator.Send(new GetOrderDetailQuery(orderId), ct);
        if (siparis.IsFailure || siparis.Value!.MemberId != GetMemberId())
            return NotFound(new { success = false, error = "Sipariş bulunamadı." });

        var result = await mediator.Send(
            new ECSPros.Order.Application.Queries.GetInvoices.GetInvoicesQuery(orderId, null, 1, 20), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });

        return Ok(new
        {
            success = true,
            data = result.Value!.Items
                .Where(i => i.Status != "cancelled")
                .Select(i => new { i.Id, i.InvoiceNumber, i.InvoiceType, i.InvoiceDate, i.HasIntegratorPdf })
        });
    }

    /// <summary>H1: Fatura PDF proxy — sahiplik + URL çözümü sunucu tarafında
    /// (GetMemberInvoicePdfSource), allowlist denetimi FaturaPdfProxy'de. Bearer'lı
    /// istemciler (mobil) için; web iframe'i cookie kimlikli /hesabim/fatura rotasını kullanır.</summary>
    [HttpGet("orders/{orderId}/invoices/{invoiceId}/pdf")]
    public async Task<IActionResult> GetOrderInvoicePdf(
        Guid orderId, Guid invoiceId, [FromQuery] bool indir,
        [FromServices] Services.Store.IFaturaPdfProxy faturaPdfProxy, CancellationToken ct)
    {
        var kaynak = await mediator.Send(
            new ECSPros.Order.Application.Queries.GetMemberInvoicePdfSource.GetMemberInvoicePdfSourceQuery(
                invoiceId, orderId, GetMemberId()), ct);
        if (kaynak.IsFailure) return NotFound(new { success = false, error = kaynak.Error });

        var sonuc = await faturaPdfProxy.GetirAsync(kaynak.Value!, ct);
        if (!sonuc.Basarili)
            return StatusCode(sonuc.HataKodu, new { success = false, error = sonuc.HataMesaji });

        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.ContentDisposition = indir
            ? "attachment; filename=\"fatura.pdf\""
            : "inline; filename=\"fatura.pdf\"";
        return File(sonuc.Pdf!, "application/pdf", enableRangeProcessing: true);
    }

    // Returns
    [HttpGet("returns")]
    public async Task<IActionResult> GetReturns([FromQuery] string? status, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetReturnsQuery(null, GetMemberId(), status, page, 20), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("returns/{returnId}")]
    public async Task<IActionResult> GetReturn(Guid returnId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetReturnDetailQuery(returnId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        if (result.Value!.MemberId != GetMemberId()) // E8: sahiplik denetimi
            return NotFound(new { success = false, error = "İade talebi bulunamadı." });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>E8: mağazadan iade talebi — kalemler farklı teslim edilmiş siparişlerden
    /// olabilir, sipariş başına bir Return açılır. Doğrulanmış telefon şarttır (SMS
    /// doğrulama modalı); değilse istemcinin OTP akışını başlatması için özel kod döner.</summary>
    [HttpPost("returns")]
    public async Task<IActionResult> CreateReturn([FromBody] StoreCreateReturnRequest req, CancellationToken ct)
    {
        var memberId = GetMemberId();

        var uye = await mediator.Send(new GetMemberDetailQuery(memberId), ct);
        if (uye.IsFailure) return BadRequest(new { success = false, error = uye.Error });
        if (!uye.Value!.IsPhoneVerified)
            return BadRequest(new { success = false, error = "İade kodu alabilmek için telefon numaranızı SMS ile doğrulamalısınız.", code = "phone_verification_required" });

        var result = await mediator.Send(new ECSPros.Order.Application.Commands.CreateStoreReturn.CreateStoreReturnCommand(
            memberId, req.Items, req.ImageUrls), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>E8: iade talebi görselleri — /media/returns altına yazılır (nginx sunar).
    /// En çok 5 dosya × 5 MB; içerik tipine göre uzantı verilir (istemci adı kullanılmaz).</summary>
    [HttpPost("returns/images")]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> UploadReturnImages(
        [FromForm] List<IFormFile> files,
        [FromServices] ECSPros.Api.Services.Storage.IFileStorage storage,
        CancellationToken ct)
    {
        var uzantilar = new Dictionary<string, string>
        {
            ["image/jpeg"] = ".jpg", ["image/png"] = ".png", ["image/webp"] = ".webp", ["image/gif"] = ".gif"
        };

        if (files.Count == 0)
            return BadRequest(new { success = false, error = "Yüklenecek görsel bulunamadı." });
        if (files.Count > 5)
            return BadRequest(new { success = false, error = "En fazla 5 görsel yükleyebilirsiniz." });
        if (files.Any(f => f.Length > 5_000_000))
            return BadRequest(new { success = false, error = "Her görsel en fazla 5 MB olabilir." });
        if (files.Any(f => !uzantilar.ContainsKey(f.ContentType)))
            return BadRequest(new { success = false, error = "Yalnızca JPEG, PNG, WebP veya GIF görselleri yükleyebilirsiniz." });

        var altDizin = $"returns/{DateTime.UtcNow:yyyyMM}";

        var urls = new List<string>();
        foreach (var dosya in files)
        {
            var ad = $"{Guid.NewGuid():N}{uzantilar[dosya.ContentType]}";
            await using var stream = dosya.OpenReadStream();
            var stored = await storage.SavePublicAsync(altDizin, ad, stream, dosya.ContentType, ct);
            urls.Add(stored.PublicUrl);
        }

        return Ok(new { success = true, data = new { urls } });
    }

    /// <summary>E8: telefon doğrulama SMS'i — kod üyenin KAYITLI telefonuna gider.</summary>
    [HttpPost("phone-verification/send")]
    public async Task<IActionResult> SendPhoneVerification(CancellationToken ct)
    {
        var result = await mediator.Send(
            new ECSPros.Crm.Application.Commands.SendPhoneVerificationOtp.SendPhoneVerificationOtpCommand(GetMemberId()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>E8: telefon doğrulama kodu kontrolü — başarıda IsPhoneVerified işaretlenir.</summary>
    [HttpPost("phone-verification/verify")]
    public async Task<IActionResult> VerifyPhoneVerification([FromBody] VerifyPhoneRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ECSPros.Crm.Application.Commands.VerifyPhoneVerificationOtp.VerifyPhoneVerificationOtpCommand(GetMemberId(), req.Code), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>E9: üyenin kullanabileceği kuponlar — yalnız üyeye/üye grubuna tanımlı
    /// olanlar (genel pazarlama kodları listelenmez). Grup kimliği CRM'den çözülür.</summary>
    [HttpGet("coupons")]
    public async Task<IActionResult> GetCoupons(CancellationToken ct)
    {
        var memberId = GetMemberId();
        var uye = await mediator.Send(new GetMemberDetailQuery(memberId), ct);
        var grupId = uye.IsSuccess ? uye.Value!.MemberGroupId : (Guid?)null;

        var result = await mediator.Send(
            new ECSPros.Promotion.Application.Queries.GetMemberCoupons.GetMemberCouponsQuery(memberId, grupId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    // Wallet
    [HttpGet("wallet")]
    public async Task<IActionResult> GetWallet(CancellationToken ct)
    {
        var result = await mediator.Send(new GetMemberWalletQuery(GetMemberId()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    // Loyalty
    [HttpGet("loyalty")]
    public async Task<IActionResult> GetLoyalty(CancellationToken ct)
    {
        var result = await mediator.Send(new GetMemberLoyaltyQuery(GetMemberId()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }
}

public record UpdateProfileRequest(
    string FirstName,
    string LastName,
    string? Phone,
    string? Gender,
    DateOnly? BirthDate,
    Guid? CityId = null);   // E2: yaşadığı şehir (G9 segmenti)

public record MarketingConsentsRequest(bool Email, bool Sms, bool Phone);

/// <summary>E8: iade talebi isteği — kalem + neden seçimleri + yüklenen görsel URL'leri.</summary>
public record StoreCreateReturnRequest(
    List<ECSPros.Order.Application.Commands.CreateStoreReturn.StoreReturnItemRequest> Items,
    List<string>? ImageUrls = null);

public record VerifyPhoneRequest(string Code);

public record SetIdentityRequest(string IdentityNumber, DateOnly? BirthDate = null);
