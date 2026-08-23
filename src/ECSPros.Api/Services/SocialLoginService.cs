using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECSPros.Shared.Contracts;

namespace ECSPros.Api.Services;

/// <summary>OAuth token/userinfo sonucu — StoreAuthController'a normalleştirilmiş hali.</summary>
public record SocialUserInfo(
    string ProviderUserId,
    string Email,
    string FirstName,
    string LastName,
    bool EmailVerified);

/// <summary>
/// Google/Facebook OAuth akışının HTTP ayağı: auth URL üretimi, code → access_token
/// değişimi ve kullanıcı profili çekimi. Gizli/anahtar değerleri burada saklanmaz;
/// yalnızca çağırana iletilen ayarlar ve code kullanılır. Facebook sunucu Graph
/// çağrılarında appsecret_proof (HMAC-SHA256(clientSecret, accessToken)) üretilir.
/// </summary>
public class SocialLoginService(
    IHttpClientFactory httpClientFactory,
    ILogger<SocialLoginService> logger)
{
    private const string GoogleAuth = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string GoogleToken = "https://oauth2.googleapis.com/token";
    private const string GoogleUserInfo = "https://www.googleapis.com/oauth2/v3/userinfo";
    private const string FacebookGraph = "https://graph.facebook.com";

    public string BuildAuthUrl(
        string provider, SocialLoginSettings settings, string state, string redirectUri)
    {
        var scopes = Scopes(provider, settings);
        if (provider == "facebook")
        {
            return $"{FacebookGraph}/{GraphVer(settings)}/dialog/oauth"
                   + $"?client_id={Uri.EscapeDataString(settings.ClientId)}"
                   + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                   + $"&scope={Uri.EscapeDataString(scopes)}"
                   + $"&state={Uri.EscapeDataString(state)}";
        }

        return $"{GoogleAuth}"
               + $"?client_id={Uri.EscapeDataString(settings.ClientId)}"
               + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
               + "&response_type=code"
               + $"&scope={Uri.EscapeDataString(scopes)}"
               + $"&state={Uri.EscapeDataString(state)}"
               + "&access_type=online&prompt=select_account";
    }

    public async Task<SocialUserInfo?> ExchangeAsync(
        string provider, SocialLoginSettings settings, string code, string redirectUri,
        CancellationToken ct)
    {
        return provider == "facebook"
            ? await FacebookAsync(settings, code, redirectUri, ct)
            : await GoogleAsync(settings, code, redirectUri, ct);
    }

    private async Task<SocialUserInfo> GoogleAsync(
        SocialLoginSettings settings, string code, string redirectUri, CancellationToken ct)
    {
        var http = httpClientFactory.CreateClient();

        using var tokenResp = await http.PostAsync(GoogleToken, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            }), ct);
        tokenResp.EnsureSuccessStatusCode();

        using var tokenDoc = JsonDocument.Parse(await tokenResp.Content.ReadAsStringAsync(ct));
        var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Google access_token alınamadı.");

        using var userReq = new HttpRequestMessage(HttpMethod.Get, GoogleUserInfo);
        userReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var userResp = await http.SendAsync(userReq, ct);
        userResp.EnsureSuccessStatusCode();

        using var userDoc = JsonDocument.Parse(await userResp.Content.ReadAsStringAsync(ct));
        var r = userDoc.RootElement;

        var email = r.TryGetProperty("email", out var emailEl) ? emailEl.GetString() ?? "" : "";
        var given = r.TryGetProperty("given_name", out var g) ? g.GetString() : null;
        var family = r.TryGetProperty("family_name", out var f) ? f.GetString() : null;
        var full = r.TryGetProperty("name", out var n) ? n.GetString() : null;

        return new SocialUserInfo(
            r.TryGetProperty("sub", out var sub) ? sub.GetString() ?? "" : "",
            email,
            NormalizeAd(given ?? full),
            family ?? "",
            r.TryGetProperty("email_verified", out var ev) && ev.ValueKind == JsonValueKind.True);
    }

    private async Task<SocialUserInfo> FacebookAsync(
        SocialLoginSettings settings, string code, string redirectUri, CancellationToken ct)
    {
        var http = httpClientFactory.CreateClient();
        var ver = GraphVer(settings);

        var tokenUrl = $"{FacebookGraph}/{ver}/oauth/access_token"
                       + $"?client_id={Uri.EscapeDataString(settings.ClientId)}"
                       + $"&client_secret={Uri.EscapeDataString(settings.ClientSecret)}"
                       + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                       + $"&code={Uri.EscapeDataString(code)}";
        using var tokenResp = await http.GetAsync(tokenUrl, ct);
        tokenResp.EnsureSuccessStatusCode();

        using var tokenDoc = JsonDocument.Parse(await tokenResp.Content.ReadAsStringAsync(ct));
        var accessToken = tokenDoc.RootElement.TryGetProperty("access_token", out var at)
            ? at.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("Facebook access_token alınamadı.");

        var proof = HmacSha256Hex(settings.ClientSecret, accessToken);
        var meUrl = $"{FacebookGraph}/{ver}/me"
                    + "?fields=id,name,email,first_name,last_name"
                    + $"&access_token={Uri.EscapeDataString(accessToken)}"
                    + $"&appsecret_proof={proof}";
        using var meResp = await http.GetAsync(meUrl, ct);
        meResp.EnsureSuccessStatusCode();

        using var meDoc = JsonDocument.Parse(await meResp.Content.ReadAsStringAsync(ct));
        var r = meDoc.RootElement;

        var email = r.TryGetProperty("email", out var emailEl) ? emailEl.GetString() ?? "" : "";
        var first = r.TryGetProperty("first_name", out var f1) ? f1.GetString() : null;
        var last = r.TryGetProperty("last_name", out var l1) ? l1.GetString() : null;
        var full = r.TryGetProperty("name", out var n1) ? n1.GetString() : null;

        return new SocialUserInfo(
            r.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "",
            email,
            NormalizeAd(first ?? full),
            last ?? "",
            !string.IsNullOrWhiteSpace(email));
    }

    private static string Scopes(string provider, SocialLoginSettings settings)
    {
        if (settings.Scopes is { Count: > 0 })
            return string.Join(" ", settings.Scopes);
        return provider == "facebook" ? "email public_profile" : "openid email profile";
    }

    private static string GraphVer(SocialLoginSettings settings) =>
        string.IsNullOrWhiteSpace(settings.GraphApiVersion) ? "v26.0" : settings.GraphApiVersion!;

    private static string NormalizeAd(string? value)
    {
        var ad = (value ?? string.Empty).Trim();
        return ad.Length > 0 ? ad : "Üye";
    }

    private static string HmacSha256Hex(string key, string value)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
