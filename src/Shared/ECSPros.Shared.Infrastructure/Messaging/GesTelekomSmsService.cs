using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ECSPros.Shared.Infrastructure.Messaging;

/// <summary>
/// ISmsService'in gerçek implementasyonu — GES Telekom (TT Mesaj) REST API.
/// Ayar kaynağı DB'deki sms servis tanımıdır (ISmsSettingsProvider — admin firma
/// detayından girilir, Credentials şifreli); kayıt yoksa SMS gönderilmez, LogSmsService
/// biçiminde loglanır (site SMS'siz de çalışır — SMTP ile aynı güvenlik ağı).
/// Akış: POST /api/Login/TokenJson (userName/password → bearer, API ömrü 24 saat;
/// 23 saat cache'lenir) → POST /api/SendSms/SendSingle. Yanıtta message/sonuc "*OK*"
/// ile başlamıyorsa hata. 401'de token cache'i düşürülüp bir kez yenilenir.
/// Gönderim hatası FIRLATILIR — SMTP ile aynı çağıran sözleşmesi (OTP akışı kullanıcıya
/// hata döndürmelidir; sessizce yutulursa "kod gelmiyor" olarak yaşanır).
/// </summary>
public class GesTelekomSmsService : ISmsService
{
    public const string ProviderCode = "gestelekom";
    private const string DefaultApiUrl = "https://restapi.ttmesaj.com";
    private static readonly TimeSpan TokenCacheTtl = TimeSpan.FromHours(23);

    private readonly IHttpClientFactory _httpFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GesTelekomSmsService> _logger;
    private readonly ISmsSettingsProvider? _settingsProvider;

    public GesTelekomSmsService(
        IHttpClientFactory httpFactory,
        IMemoryCache cache,
        ILogger<GesTelekomSmsService> logger,
        ISmsSettingsProvider? settingsProvider = null)
    {
        _httpFactory = httpFactory;
        _cache = cache;
        _logger = logger;
        _settingsProvider = settingsProvider;
    }

    public async Task SendAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        var settings = _settingsProvider is null ? null : await _settingsProvider.GetAsync(ct);

        if (settings is null || !string.Equals(settings.ProviderCode, ProviderCode, StringComparison.OrdinalIgnoreCase))
        {
            if (settings is not null)
                _logger.LogWarning(
                    "Aktif SMS servis kaydı '{Code}' bu implementasyonla eşleşmiyor (beklenen: {Expected}); log yedeğine düşülüyor.",
                    settings.ProviderCode, ProviderCode);

            // SMS yapılandırılmamış — eski LogSmsService davranışı (log biçimi aynı).
            _logger.LogInformation("[SMS] To: {Phone} | Message: {Message}", phoneNumber, message);
            return;
        }

        var numara = NumaraNormalize(phoneNumber);
        var apiUrl = string.IsNullOrWhiteSpace(settings.ApiUrl)
            ? DefaultApiUrl
            : settings.ApiUrl.TrimEnd('/');
        // Yol parçaları burada eklenir ("/api/Login/..."); admin'e taban adres "/api" ekiyle
        // girilirse çift "/api/api/" oluşup 404 dönüyordu (2026-07-15 canlıda yaşandı) — ek atılır.
        if (apiUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
            apiUrl = apiUrl[..^4].TrimEnd('/');

        try
        {
            var yanit = await GonderAsync(apiUrl, settings, numara, message, tokenYenile: false, ct);

            // Süresi dolan/geçersiz token: cache düşürülüp tek sefer yeni token'la denenir.
            if (yanit.StatusCode == HttpStatusCode.Unauthorized)
                yanit = await GonderAsync(apiUrl, settings, numara, message, tokenYenile: true, ct);

            yanit.EnsureSuccessStatusCode();

            var govde = await yanit.Content.ReadFromJsonAsync<SendSmsResponse>(cancellationToken: ct);
            var basarili = govde?.Message?.StartsWith("*OK*", StringComparison.OrdinalIgnoreCase) == true
                        || govde?.Sonuc?.StartsWith("*OK*", StringComparison.OrdinalIgnoreCase) == true;

            if (!basarili)
                throw new InvalidOperationException(
                    $"GES Telekom SMS gönderimi reddedildi: sonuc='{govde?.Sonuc}', message='{govde?.Message}'");

            _logger.LogInformation("[SMS] To: {Phone} (GES Telekom gönderildi, sonuc: {Sonuc})",
                numara, govde!.Sonuc);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogWarning(ex, "GES Telekom SMS gönderimi başarısız: {Phone}", numara);
            throw;
        }
    }

    private async Task<HttpResponseMessage> GonderAsync(
        string apiUrl, SmsSettings settings, string numara, string message, bool tokenYenile, CancellationToken ct)
    {
        var token = await TokenAlAsync(apiUrl, settings, tokenYenile, ct);

        var client = _httpFactory.CreateClient(nameof(GesTelekomSmsService));
        using var istek = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/api/SendSms/SendSingle");
        istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // ed/recipentType/brandCode dokümanda opsiyonel yazsa da API validasyonu zorunlu tutuyor
        // (2026-07-15 canlı testte doğrulandı: eksikken 400 "field is required").
        istek.Content = JsonContent.Create(new
        {
            username = settings.Username,
            password = settings.Password,
            numbers = numara,
            message,
            origin = settings.Origin,
            sd = "0",                                                  // hemen gönder
            ed = DateTime.Now.AddHours(24).ToString("yyyyMMddHHmm"),   // son teslim: 24 saat
            isNotification = settings.IsNotification,
            recipentType = "BIREYSEL",
            brandCode = ""
        });

        return await client.SendAsync(istek, ct);
    }

    private async Task<string> TokenAlAsync(string apiUrl, SmsSettings settings, bool yenile, CancellationToken ct)
    {
        var cacheKey = $"gestelekom-token:{apiUrl}:{settings.Username}";
        if (yenile)
            _cache.Remove(cacheKey);
        else if (_cache.TryGetValue(cacheKey, out string? mevcut) && mevcut is not null)
            return mevcut;

        var client = _httpFactory.CreateClient(nameof(GesTelekomSmsService));
        using var yanit = await client.PostAsJsonAsync($"{apiUrl}/api/Login/TokenJson",
            new { userName = settings.Username, password = settings.Password }, ct);
        yanit.EnsureSuccessStatusCode();

        var govde = await yanit.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
        if (string.IsNullOrWhiteSpace(govde?.AccessToken))
            throw new InvalidOperationException("GES Telekom token yanıtı boş — kullanıcı adı/şifre hatalı olabilir.");

        // API token ömrünü saniye döner (86400 = 24 saat); pay bırakılarak cache'lenir.
        var ttl = govde.ExpiresIn > 3600
            ? TimeSpan.FromSeconds(govde.ExpiresIn - 3600)
            : TokenCacheTtl;
        _cache.Set(cacheKey, govde.AccessToken, ttl);
        return govde.AccessToken;
    }

    /// <summary>TT Mesaj numara biçimi: yurtiçi 905XXXXXXXXX, yurtdışı 00 önekli (min 12 hane).</summary>
    private static string NumaraNormalize(string telefon)
    {
        var rakamlar = new string(telefon.Where(char.IsDigit).ToArray());
        if (telefon.TrimStart().StartsWith('+'))
            rakamlar = rakamlar.StartsWith("90") ? rakamlar : "00" + rakamlar;
        return rakamlar switch
        {
            { Length: 10 } r when r.StartsWith('5') => "90" + r,   // 5XXXXXXXXX
            { Length: 11 } r when r.StartsWith("05") => "9" + r,   // 05XXXXXXXXX
            _ => rakamlar                                          // 905... veya 00 önekli yurtdışı
        };
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("expires_in")] long ExpiresIn);

    private sealed record SendSmsResponse(
        [property: JsonPropertyName("sonuc")] string? Sonuc,
        [property: JsonPropertyName("kontor")] string? Kontor,
        [property: JsonPropertyName("message")] string? Message);
}
