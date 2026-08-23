using ECSPros.Api.Services;
using ECSPros.Crm.Application.Commands.ExternalLoginMember;
using ECSPros.Crm.Application.Commands.LoginMember;
using ECSPros.Crm.Application.Commands.RefreshMemberToken;
using ECSPros.Crm.Application.Commands.RegisterMember;
using ECSPros.Crm.Application.Commands.RevokeMemberSession;
using ECSPros.Crm.Application.Commands.SendLoginOtp;
using ECSPros.Crm.Application.Commands.VerifyLoginOtp;
using ECSPros.Crm.Application.Queries.GetMemberDetail;
using ECSPros.Shared.Contracts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Cryptography;
using System.Text.Json;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/store/auth")]
[EnableRateLimiting("store-auth")] // brute-force freni — IP başına dk'da 60 istek (2026-07-23)
public class StoreAuthController(
    IMediator mediator,
    ISocialLoginSettingsProvider socialLoginSettings,
    IStoreContext storeContext,
    SocialLoginService socialLoginService,
    ILogger<StoreAuthController> logger) : ControllerBase
{
    /// <summary>D1: access token'ı SSR kimliği için HttpOnly cookie'ye de yazar.
    /// Secure yalnız HTTPS istekte (origin Cloudflare Flexible arkasında HTTP çalışır;
    /// localhost testleri de HTTP). JS localStorage akışı değişmez.</summary>
    private void UyeCerezYaz(MemberLoginResponse veri) =>
        Response.Cookies.Append(StoreMemberSession.CookieAdi, veri.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Expires = veri.ExpiresAt,
            Path = "/"
        });

    private void UyeCerezSil() =>
        Response.Cookies.Delete(StoreMemberSession.CookieAdi, new CookieOptions { Path = "/" });

    // E2: Aktif Cihazlar / Giriş Geçmişi için oturuma cihaz bilgisi yazılır.
    private string? IstemciIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? IstemciUa()
    {
        var ua = Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(ua) ? null : ua[..Math.Min(ua.Length, 500)];
    }

    /// <summary>İE-2 Faz B: sign_up/login commerce event'i (outbox) — hata-güvenli, kanal host'tan çözülür.</summary>
    private async Task TakipYayinlaAsync(string eventName, Guid? firmPlatformId, Guid? memberId, string? email, string? phone, CancellationToken ct)
    {
        try
        {
            var platformId = firmPlatformId ?? (await storeContext.GetPlatformAsync(ct))?.Id;
            if (platformId is null || platformId == Guid.Empty) return;
            var publisher = HttpContext.RequestServices.GetRequiredService<ECSPros.Shared.Contracts.Tracking.ICommerceEventPublisher>();
            var client = ECSPros.Api.Services.Tracking.TrackingHttpContextReader.ReadClient(HttpContext, email, phone, memberId);
            var consent = ECSPros.Api.Services.Tracking.TrackingHttpContextReader.ReadConsent(HttpContext);
            await publisher.PublishAsync(new ECSPros.Shared.Contracts.Tracking.CommerceEvent(
                eventName, DateTime.UtcNow, platformId.Value, Guid.NewGuid().ToString("D"), "web", memberId,
                "TRY", null, null, Array.Empty<ECSPros.Shared.Contracts.Tracking.CommerceItem>(), client, consent,
                new Dictionary<string, string> { ["method"] = phone is not null && email is null ? "phone" : "email" }), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Takip {Event} event'i yazılamadı", eventName);
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterMemberRequest req, CancellationToken ct)
    {
        // D3: onaylanan belgelerin kaydı — istemci yalnız kod gönderir; başlık ve metin
        // sürümü sunucuda CMS'ten çözülür (C8 checkout kabul kaydıyla aynı desen).
        List<ECSPros.Crm.Application.Commands.RegisterMember.MemberConsent>? onaylar = null;
        if (req.AcceptedContracts is { Count: > 0 } && req.FirmPlatformId is { } platformId)
        {
            var belgeler = await mediator.Send(
                new ECSPros.Cms.Application.Queries.GetStoreLegalPages.GetStoreLegalPagesQuery(
                    platformId, req.AcceptedContracts), ct);
            if (belgeler.IsSuccess)
                onaylar = belgeler.Value!
                    .Select(b => new ECSPros.Crm.Application.Commands.RegisterMember.MemberConsent(
                        b.Code, b.Title, DateTime.UtcNow, b.ContentUpdatedAt))
                    .ToList();
        }

        var result = await mediator.Send(new RegisterMemberCommand(
            req.Email, req.Password, req.FirstName, req.LastName, req.Phone, onaylar,
            req.Gender, req.BirthDate), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        await TakipYayinlaAsync(ECSPros.Shared.Contracts.Tracking.CommerceEventNames.SignUp, req.FirmPlatformId, result.Value, req.Email, req.Phone, ct);
        return Ok(new { success = true, data = new { memberId = result.Value } });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginMemberRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new LoginMemberCommand(req.Email ?? string.Empty, req.Password, IstemciIp(), IstemciUa(), req.Phone), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        UyeCerezYaz(result.Value!);
        await TakipYayinlaAsync(ECSPros.Shared.Contracts.Tracking.CommerceEventNames.Login, null, null, req.Email, req.Phone, ct);
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshMemberRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new RefreshMemberTokenCommand(req.RefreshToken), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        UyeCerezYaz(result.Value!);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>D4: SMS ile giriş — 1. adım, kayıtlı üyenin telefonuna tek kullanımlık
    /// kod gönderilir (120 sn geçerli; yeniden gönderim ve saatlik sınırlar komutta).</summary>
    [HttpPost("otp/send")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new SendLoginOtpCommand(req.Phone), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>D4: SMS ile giriş — 2. adım, kod doğruysa şifresiz oturum açılır
    /// (login ile aynı yanıt + SSR cookie'si).</summary>
    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new VerifyLoginOtpCommand(req.Phone, req.Code, IstemciIp(), IstemciUa()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        UyeCerezYaz(result.Value!);
        await TakipYayinlaAsync(ECSPros.Shared.Contracts.Tracking.CommerceEventNames.Login, null, null, null, req.Phone, ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>D1/D6: çıkış — refresh oturumu iptal edilir (varsa) + SSR cookie'si silinir.
    /// Anonim erişilebilir: access token süresi dolmuş olsa da çıkış tamamlanabilmeli.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutMemberRequest? req, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(req?.RefreshToken))
            await mediator.Send(new RevokeMemberSessionCommand(req.RefreshToken), ct);
        UyeCerezSil();
        return Ok(new { success = true });
    }

    [HttpGet("me")]
    [Authorize(Policy = "MemberOnly")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());
        var result = await mediator.Send(new GetMemberDetailQuery(memberId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    // ── OAuth sosyal giriş (Google/Facebook) ──────────────────────────────────────
    private const string OAuthStateCookie = "ecspros_oauth_state";
    private const string OAuthReturnCookie = "ecspros_oauth_return";
    private static readonly string[] SosyalSaglayicilar = ["google", "facebook"];

    /// <summary>Bu kanalda etkin olan sosyal giriş sağlayıcılarını döndürür
    /// (login modal butonları buna göre gösterilir/gizlenir).</summary>
    [HttpGet("external/providers")]
    public async Task<IActionResult> ExternalProviders(CancellationToken ct)
    {
        var platform = await storeContext.GetPlatformAsync(ct);
        if (platform is null)
            return Ok(new { success = true, data = Array.Empty<object>() });

        var sonuc = new List<object>();
        foreach (var p in SosyalSaglayicilar)
        {
            if (await socialLoginSettings.GetAsync(p, platform.Id, ct) is not null)
                sonuc.Add(new { provider = p });
        }
        return Ok(new { success = true, data = sonuc });
    }

    /// <summary>OAuth akışını başlatır: state üretip cookie'ye yazar, sağlayıcı
    /// auth URL'sine yönlendirir. Dönüş adresi ayrı cookie'de tutulur.</summary>
    [HttpGet("external/{provider}/start")]
    public async Task<IActionResult> ExternalStart([FromRoute] string provider, [FromQuery] string? returnUrl, CancellationToken ct)
    {
        provider = provider?.ToLowerInvariant() ?? string.Empty;
        if (Array.IndexOf(SosyalSaglayicilar, provider) < 0)
            return NotFound(new { success = false, error = "Bilinmeyen giriş sağlayıcı." });

        var platform = await storeContext.GetPlatformAsync(ct);
        if (platform is null)
            return NotFound(new { success = false, error = "Aktif satış kanalı bulunamadı." });

        var settings = await socialLoginSettings.GetAsync(provider, platform.Id, ct);
        if (settings is null)
            return NotFound(new { success = false, error = "Bu giriş yöntemi bu kanalda etkin değil." });

        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var redirectUri = CozRedirectUri(provider, settings, platform);
        var authUrl = socialLoginService.BuildAuthUrl(provider, settings, state, redirectUri);

        Response.Cookies.Append(OAuthStateCookie, state, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Expires = DateTime.UtcNow.AddMinutes(10),
            Path = "/"
        });
        Response.Cookies.Append(OAuthReturnCookie, GuvenliDonus(returnUrl), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Expires = DateTime.UtcNow.AddMinutes(10),
            Path = "/"
        });

        return Redirect(authUrl);
    }

    /// <summary>OAuth callback: state doğrula → code değiş → kullanıcı profilini çek
    /// → üye bul/oluştur/bağla → session+token üret → cookie yaz + localStorage'a
    /// token yazan kısa bir sayfayla vitrine yönlendir.</summary>
    [HttpGet("external/{provider}/callback")]
    public async Task<IActionResult> ExternalCallback(
        [FromRoute] string provider,
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken ct)
    {
        provider = provider?.ToLowerInvariant() ?? string.Empty;
        if (Array.IndexOf(SosyalSaglayicilar, provider) < 0)
            return HataSayfasi("Bilinmeyen giriş sağlayıcı.");

        var stateCookie = Request.Cookies[OAuthStateCookie];
        var donus = Request.Cookies[OAuthReturnCookie] is { Length: > 0 } d ? d : "/";
        Response.Cookies.Delete(OAuthStateCookie, new CookieOptions { Path = "/" });
        Response.Cookies.Delete(OAuthReturnCookie, new CookieOptions { Path = "/" });

        if (!string.IsNullOrWhiteSpace(error))
            return HataSayfasi("Giriş sağlayıcı tarafından iptal edildi.");

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state)
            || !string.Equals(state, stateCookie, StringComparison.Ordinal))
            return HataSayfasi("Oturum doğrulaması başarısız. Lütfen girişi tekrar başlatın.");

        var platform = await storeContext.GetPlatformAsync(ct);
        if (platform is null)
            return HataSayfasi("Aktif satış kanalı bulunamadı.");

        var settings = await socialLoginSettings.GetAsync(provider, platform.Id, ct);
        if (settings is null)
            return HataSayfasi("Bu giriş yöntemi bu kanalda etkin değil.");

        SocialUserInfo? user;
        try
        {
            user = await socialLoginService.ExchangeAsync(
                provider, settings, code, CozRedirectUri(provider, settings, platform), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sosyal giriş token değişimi başarısız (provider {Provider}).", provider);
            return HataSayfasi("Giriş sağlayıcıdan doğrulama alınamadı. Lütfen tekrar deneyin.");
        }

        if (user is null || string.IsNullOrWhiteSpace(user.Email))
            return HataSayfasi("Giriş sağlayıcı e-posta adresi paylaşmadı; giriş tamamlanamadı.");

        var result = await mediator.Send(new ExternalLoginMemberCommand(
            provider, user.ProviderUserId, user.Email, user.FirstName, user.LastName,
            user.EmailVerified, IstemciIp(), IstemciUa()), ct);

        if (result.IsFailure)
            return HataSayfasi(result.Error ?? "Giriş tamamlanamadı.");

        UyeCerezYaz(result.Value!);
        return BasariliYonlendirme(result.Value!, donus);
    }

    private string CozRedirectUri(string provider, SocialLoginSettings settings, StorePlatformBilgisi platform)
    {
        if (!string.IsNullOrWhiteSpace(settings.RedirectUri)) return settings.RedirectUri!;
        var kok = platform.CanonicalDomain.TrimEnd('/');
        return $"{kok}/api/store/auth/external/{provider}/callback";
    }

    private static string GuvenliDonus(string? donus)
    {
        if (string.IsNullOrWhiteSpace(donus)) return "/";
        if (donus.StartsWith("//") || donus.Contains('\\') || !donus.StartsWith('/')) return "/";
        return donus;
    }

    private static IActionResult HataSayfasi(string mesaj)
    {
        var m = System.Net.WebUtility.HtmlEncode(mesaj);
        var html = $@"<!doctype html>
<html lang='tr'>
<head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Giriş başarısız</title></head>
<body style='font-family:sans-serif;text-align:center;padding:40px;color:#333'>
<p style='font-size:18px'>{m}</p>
<p><a href='/' style='color:#c2410c'>Ana sayfaya dön</a></p>
</body>
</html>";
        return new ContentResult { Content = html, ContentType = "text/html" };
    }

    private static IActionResult BasariliYonlendirme(MemberLoginResponse veri, string donus)
    {
        var hedef = JsonSerializer.Serialize(GuvenliDonus(donus));
        var access = JsonSerializer.Serialize(veri.AccessToken);
        var refresh = JsonSerializer.Serialize(veri.RefreshToken);
        var html = $@"<!doctype html>
<html lang='tr'>
<head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Giriş tamamlanıyor</title></head>
<body style='font-family:sans-serif;text-align:center;padding:40px;color:#333'>
<p style='font-size:18px'>Giriş tamamlanıyor, lütfen bekleyin…</p>
<script>
try {{
    localStorage.setItem('ecspros_member_token', {access});
    localStorage.setItem('ecspros_member_refresh', {refresh});
}} catch (e) {{ }}
location.replace({hedef});
</script>
</body>
</html>";
        return new ContentResult { Content = html, ContentType = "text/html" };
    }
}

public record RegisterMemberRequest(
    string Email, string Password, string FirstName, string LastName, string? Phone = null,
    Guid? FirmPlatformId = null,               // D3: onay kodlarının hangi platformun CMS'inden çözüleceği
    List<string>? AcceptedContracts = null,    // D3: onaylanan belge kodları
    string? Gender = null,                     // kayıt formundaki cinsiyet (female/male/null)
    DateOnly? BirthDate = null);               // kayıt formundaki doğum tarihi (yyyy-MM-dd, opsiyonel)
public record LoginMemberRequest(string? Email, string Password, string? Phone = null);
public record RefreshMemberRequest(string RefreshToken);
public record LogoutMemberRequest(string? RefreshToken = null);
public record SendOtpRequest(string Phone);
public record VerifyOtpRequest(string Phone, string Code);
