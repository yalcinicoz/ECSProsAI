using ECSPros.Api.Services.Store;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Controllers;

/// <summary>
/// Mobil cihaz doğrulama uçları (2026-07-23): challenge → attestation → kısa ömürlü
/// anonim device token + oturum imza secret'ı. Akış docs/mobil-api-referansi.md'de.
/// </summary>
[ApiController]
[Route("api/store/device")]
[EnableRateLimiting("store-auth")]
public class StoreDeviceController(
    IEnumerable<IDeviceAttestationVerifier> verifiers,
    IDeviceTokenService tokenService,
    IMemoryCache cache,
    ILogger<StoreDeviceController> logger) : ControllerBase
{
    /// <summary>Attestation'a gömülecek tek kullanımlık challenge (10 dk geçerli).</summary>
    [HttpGet("challenge")]
    [AllowAnonymous]
    public IActionResult Challenge()
    {
        var nonce = Guid.NewGuid().ToString("N");
        cache.Set($"device-challenge:{nonce}", true, TimeSpan.FromMinutes(10));
        return Ok(new { success = true, data = new { challenge = nonce } });
    }

    public record AttestBody(string Platform, string Attestation, string Challenge);

    [HttpPost("attest")]
    [AllowAnonymous]
    public async Task<IActionResult> Attest([FromBody] AttestBody body, CancellationToken ct)
    {
        var platform = body.Platform?.Trim().ToLowerInvariant();
        if (platform is not ("android" or "ios"))
            return BadRequest(new { success = false, error = "platform 'android' veya 'ios' olmalıdır." });
        if (string.IsNullOrWhiteSpace(body.Attestation) || string.IsNullOrWhiteSpace(body.Challenge))
            return BadRequest(new { success = false, error = "attestation ve challenge zorunludur." });

        // Challenge tek kullanımlık — bulunamadıysa süresi geçti ya da tekrar kullanılıyor
        var challengeAnahtari = $"device-challenge:{body.Challenge}";
        if (!cache.TryGetValue(challengeAnahtari, out _))
            return BadRequest(new { success = false, error = "Challenge geçersiz veya süresi doldu." });
        cache.Remove(challengeAnahtari);

        // Geliştirme köprüsü yalnız config'te secret tanımlıysa devreye girer
        var bypass = verifiers.OfType<DevBypassVerifier>().FirstOrDefault();
        AttestationSonucu sonuc;
        if (bypass is { Aktif: true } && (await bypass.VerifyAsync(body.Attestation, body.Challenge, ct)).Basarili)
        {
            sonuc = AttestationSonucu.Gecti;
        }
        else
        {
            var verifier = verifiers.FirstOrDefault(v => v.Platform == platform);
            sonuc = verifier is null
                ? AttestationSonucu.Basarisiz("Platform doğrulayıcısı bulunamadı.")
                : await verifier.VerifyAsync(body.Attestation, body.Challenge, ct);
        }

        if (!sonuc.Basarili)
        {
            logger.LogWarning("Cihaz attestation reddedildi: platform={Platform} neden={Neden}",
                platform, sonuc.Hata);
            return Unauthorized(new { success = false, error = sonuc.Hata });
        }

        var token = tokenService.TokenUret(platform!);
        return Ok(new
        {
            success = true,
            data = new
            {
                deviceToken = token.DeviceToken,
                signingSecret = token.SigningSecret,
                expiresAt = token.ExpiresAt,
            },
        });
    }

    /// <summary>Web token yenileme: sekme açık kaldıkça site JS'i 10 dk'da bir çağırır.
    /// YALNIZ elinde hâlâ geçerli bir type=web token'ı olan istemci yenileyebilir —
    /// anonim çağrı (kapı açıkken) middleware'de 401 alır, süresi geçmişse sayfa
    /// yenilenince SSR taze token gömer.</summary>
    [HttpPost("~/api/store/web-token/renew")]
    [AllowAnonymous]
    public IActionResult WebTokenYenile()
    {
        if (User.FindFirst("type")?.Value != "web")
            return Unauthorized(new { success = false, error = "Geçerli web token'ı gereklidir." });
        // Zincir sınırı: HTML'den bir kez token çekip sonsuza dek yenileyen bot'u keser.
        // 8 yenileme ≈ 2 saat kesintisiz sekme; sonrasında tarayıcı sayfayı yenileyince
        // SSR taze (rn=0) token gömer — meşru kullanıcı hiçbir şey fark etmez.
        _ = int.TryParse(User.FindFirst("rn")?.Value, out var yenileme);
        if (yenileme >= 8)
            return Unauthorized(new { success = false, error = "Yenileme sınırı aşıldı — sayfayı yenileyin." });
        var (token, bitis) = tokenService.WebTokenUret(yenileme + 1);
        return Ok(new { success = true, data = new { token, expiresAt = bitis } });
    }
}
