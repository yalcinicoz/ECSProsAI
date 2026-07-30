using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ECSPros.Api.Services.Store;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Middleware;

/// <summary>
/// Mobil cihaz istek muhafızı (2026-07-23) — iki görev:
///
/// 1) İMZA + REPLAY DENETİMİ (device token'lı her istekte, her zaman aktif):
///    X-Timestamp (unix sn, ±300 sn tolerans) + X-Nonce (tek kullanımlık, 10 dk pencere)
///    + X-Signature = HMACSHA256(signingSecret, "METOD\npath?query\ntimestamp\nnonce\nSHA256(body)")
///    Secret, attestation sırasında sunucunun ürettiği oturum secret'ıdır (jti ile bulunur).
///    Aynı nonce ikinci kez gelirse istek REDDEDİLİR — yakalanan bir isteğin curl/Postman'le
///    tekrar oynatılması burada ölür.
///
/// 2) VİTRİN KAPISI (MobileGate:EnforceStoreTokens=true olunca): /api/store/* uçları
///    kimliksiz (device/member/admin token'sız) isteklere 401 döner. VARSAYILAN KAPALI —
///    web sitesi aynı uçları tarayıcıdan token'sız çağırıyor; kapı, web istemcisine de
///    token dağıtan mekanizma (ör. Turnstile) kurulmadan açılmamalı. Cutover planı
///    docs/mobil-api-referansi.md'dedir.
/// </summary>
public class DeviceRequestGuardMiddleware(
    RequestDelegate next,
    IMemoryCache cache,
    IConfiguration config,
    ILogger<DeviceRequestGuardMiddleware> logger)
{
    private const int TimestampToleransSn = 300;
    private static readonly TimeSpan NoncePencere = TimeSpan.FromMinutes(10);

    public async Task InvokeAsync(HttpContext context, IDeviceTokenService tokenService)
    {
        var yol = context.Request.Path.Value ?? string.Empty;
        var storeYuzeyi = yol.StartsWith("/api/store/", StringComparison.OrdinalIgnoreCase);
        var deviceUcu = yol.StartsWith("/api/store/device/", StringComparison.OrdinalIgnoreCase);
        // PayTR callback'i sunucu-sunucudur, token TAŞIMAZ — vitrin kapısından muaf.
        // Güvencesi token değil HASH doğrulamasıdır (PaymentController.Callback, 2026-07-30).
        var odemeCallback = yol.StartsWith("/api/store/payment/paytr/callback", StringComparison.OrdinalIgnoreCase);

        var tip = context.User.FindFirstValue("type");

        // 1) Device token'lı istek → imza zorunlu (device uçları hariç; token orada üretiliyor)
        if (tip == "device" && !deviceUcu)
        {
            var hata = await ImzaDogrulaAsync(context, tokenService);
            if (hata is not null)
            {
                await Reddet(context, hata);
                return;
            }
        }

        // 2) Vitrin kapısı (bilinçli varsayılan: kapalı — web cutover'ı bekliyor)
        if (storeYuzeyi && !deviceUcu && !odemeCallback
            && config.GetValue("MobileGate:EnforceStoreTokens", false)
            && tip is not ("device" or "member") && context.User.Identity?.IsAuthenticated != true)
        {
            await Reddet(context, "Bu uç için istemci token'ı gereklidir.", 401);
            return;
        }

        await next(context);
    }

    private async Task<string?> ImzaDogrulaAsync(HttpContext context, IDeviceTokenService tokenService)
    {
        var jti = context.User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti);
        if (jti is null) return "Geçersiz device token.";

        var secret = tokenService.SecretGetir(jti);
        if (secret is null) return "Device oturumu bulunamadı — yeniden attestation gerekli.";

        var tsMetin = context.Request.Headers["X-Timestamp"].FirstOrDefault();
        var nonce = context.Request.Headers["X-Nonce"].FirstOrDefault();
        var imza = context.Request.Headers["X-Signature"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tsMetin) || string.IsNullOrWhiteSpace(nonce) || string.IsNullOrWhiteSpace(imza))
            return "X-Timestamp, X-Nonce ve X-Signature başlıkları zorunludur.";

        if (!long.TryParse(tsMetin, out var ts)
            || Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts) > TimestampToleransSn)
            return "İstek zaman damgası geçersiz veya süresi dolmuş.";

        // Replay reddi: nonce tek kullanımlık (jti kapsamında)
        var nonceAnahtari = $"device-nonce:{jti}:{nonce}";
        if (cache.TryGetValue(nonceAnahtari, out _))
        {
            logger.LogWarning("Replay denemesi engellendi: jti={Jti} nonce={Nonce} yol={Yol}",
                jti, nonce, context.Request.Path);
            return "Bu istek daha önce işlendi (replay).";
        }
        cache.Set(nonceAnahtari, true, NoncePencere);

        // Gövde hash'i (requestHash) — imza gövdeyi de bağlar
        string govdeHash;
        context.Request.EnableBuffering();
        using (var sha = SHA256.Create())
        {
            var hash = await sha.ComputeHashAsync(context.Request.Body);
            govdeHash = Convert.ToHexString(hash).ToLowerInvariant();
            context.Request.Body.Position = 0;
        }

        var veri = string.Join('\n',
            context.Request.Method.ToUpperInvariant(),
            context.Request.Path.Value + context.Request.QueryString.Value,
            tsMetin, nonce, govdeHash);
        var beklenen = Convert.ToHexString(
            HMACSHA256.HashData(Convert.FromBase64String(secret), Encoding.UTF8.GetBytes(veri)))
            .ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(beklenen), Encoding.UTF8.GetBytes(imza.ToLowerInvariant()))
            ? null
            : "İstek imzası doğrulanamadı.";
    }

    private static async Task Reddet(HttpContext context, string hata, int kod = 401)
    {
        context.Response.StatusCode = kod;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new { success = false, error = hata }));
    }
}
