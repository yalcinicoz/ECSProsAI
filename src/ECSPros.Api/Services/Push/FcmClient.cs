using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECSPros.Core.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Services.Push;

/// <summary>Firebase servis hesabı (definition.integration_services "fcm", ServiceType "push"; firma entegrasyonu Credentials.serviceAccountJson + Settings.projectId).</summary>
public sealed record FcmSettings(string ProjectId, string ClientEmail, string PrivateKeyPem, string TokenUri);

public sealed class DbFcmSettingsProvider(ICoreDbContext db, IMemoryCache cache, ILogger<DbFcmSettingsProvider> logger)
{
    const string CacheKey = "fcm-settings-db";
    public async Task<FcmSettings?> GetAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out FcmSettings? cached)) return cached;
        FcmSettings? s = null;
        try
        {
            var kayit = await db.FirmPlatformIntegrations
                .Where(fi => fi.IsActive && fi.IntegrationService.ServiceType == "push")
                .OrderBy(fi => fi.FirmPlatformId == null ? 0 : 1).ThenBy(fi => fi.CreatedAt)
                .Select(fi => new { fi.Credentials, fi.Settings }).FirstOrDefaultAsync(ct);
            var json = kayit?.Credentials.GetValueOrDefault("serviceAccountJson")?.ToString();
            if (!string.IsNullOrWhiteSpace(json))
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                var projectId = kayit!.Settings.GetValueOrDefault("projectId")?.ToString();
                if (string.IsNullOrWhiteSpace(projectId) && r.TryGetProperty("project_id", out var pid)) projectId = pid.GetString();
                var email = r.GetProperty("client_email").GetString() ?? "";
                var key = r.GetProperty("private_key").GetString() ?? "";
                var tokenUri = r.TryGetProperty("token_uri", out var tu) ? tu.GetString() ?? "" : "https://oauth2.googleapis.com/token";
                if (!string.IsNullOrWhiteSpace(projectId) && email.Length > 0 && key.Length > 0)
                    s = new FcmSettings(projectId!, email, key, tokenUri);
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "FCM ayarları okunamadı (serviceAccountJson biçimi?)"); }
        cache.Set(CacheKey, s, TimeSpan.FromMinutes(2));
        return s;
    }
}

public sealed record FcmSonuc(bool Ok, string? MessageId, string? ErrorCode, bool Retryable, string? Detail = null);

/// <summary>
/// FCM HTTP v1 gönderici (§2): servis hesabı JWT (RS256) → OAuth2 erişim belirteci (50 dk önbellek) → messages:send.
/// Hata kodları §2.2: UNREGISTERED/INVALID_ARGUMENT → token durumu; UNAVAILABLE/INTERNAL/429 → yeniden dene.
/// </summary>
public sealed class FcmClient(IHttpClientFactory httpFactory, ILogger<FcmClient> logger)
{
    public const string HttpClientName = "fcm";
    static readonly Dictionary<string, (string Token, DateTime ExpiresUtc)> TokenCache = new();
    static readonly SemaphoreSlim TokenLock = new(1, 1);

    public async Task<FcmSonuc> GonderAsync(FcmSettings s, string token, string title, string body, string? imageUrl,
        IReadOnlyDictionary<string, string> data, string collapseKey, int ttlSeconds, bool highPriority, int badge, CancellationToken ct)
    {
        string access;
        try { access = await ErisimBelirteciAsync(s, ct); }
        catch (Exception ex) { logger.LogError(ex, "FCM OAuth belirteci alınamadı"); return new(false, null, "AUTH", true, ex.Message); }

        var notification = new Dictionary<string, object> { ["title"] = title, ["body"] = body };
        if (!string.IsNullOrWhiteSpace(imageUrl)) notification["image"] = imageUrl;
        var msg = new
        {
            message = new
            {
                token,
                notification,
                data,
                android = new
                {
                    priority = highPriority ? "HIGH" : "NORMAL",
                    ttl = $"{ttlSeconds}s",
                    collapse_key = collapseKey,
                    notification = new { channel_id = "default", sound = "default", click_action = "FLUTTER_NOTIFICATION_CLICK" },
                },
                apns = new
                {
                    headers = new Dictionary<string, string> { ["apns-priority"] = highPriority ? "10" : "5", ["apns-collapse-id"] = collapseKey },
                    payload = new { aps = new Dictionary<string, object> { ["sound"] = "default", ["badge"] = badge, ["mutable-content"] = 1 } },
                },
            },
        };
        var http = httpFactory.CreateClient(HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{s.ProjectId}/messages:send");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        req.Content = new StringContent(JsonSerializer.Serialize(msg), Encoding.UTF8, "application/json");
        HttpResponseMessage resp;
        try { resp = await http.SendAsync(req, ct); }
        catch (Exception ex) { return new(false, null, "NETWORK", true, ex.Message); }
        var text = await resp.Content.ReadAsStringAsync(ct);
        if (resp.IsSuccessStatusCode)
        {
            try { using var d = JsonDocument.Parse(text); return new(true, d.RootElement.GetProperty("name").GetString(), null, false); }
            catch { return new(true, null, null, false); }
        }
        string code = ((int)resp.StatusCode).ToString(), status = "";
        try
        {
            using var d = JsonDocument.Parse(text);
            var err = d.RootElement.GetProperty("error");
            status = err.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
            if (err.TryGetProperty("details", out var det))
                foreach (var x in det.EnumerateArray())
                    if (x.TryGetProperty("errorCode", out var ec)) { status = ec.GetString() ?? status; break; }
        }
        catch { /* gövde JSON değil */ }
        code = status.Length > 0 ? status : code;
        var retry = code is "UNAVAILABLE" or "INTERNAL" or "429" or "QUOTA_EXCEEDED" or "503" or "500";
        return new(false, null, code, retry, text.Length > 500 ? text[..500] : text);
    }

    async Task<string> ErisimBelirteciAsync(FcmSettings s, CancellationToken ct)
    {
        await TokenLock.WaitAsync(ct);
        try
        {
            if (TokenCache.TryGetValue(s.ClientEmail, out var c) && c.ExpiresUtc > DateTime.UtcNow.AddMinutes(2)) return c.Token;
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var header = B64(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
            var claims = B64(JsonSerializer.SerializeToUtf8Bytes(new
            {
                iss = s.ClientEmail, scope = "https://www.googleapis.com/auth/firebase.messaging", aud = s.TokenUri, iat = now, exp = now + 3600,
            }));
            using var rsa = RSA.Create();
            rsa.ImportFromPem(s.PrivateKeyPem);
            var imza = B64(rsa.SignData(Encoding.ASCII.GetBytes($"{header}.{claims}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            var assertion = $"{header}.{claims}.{imza}";
            var http = httpFactory.CreateClient(HttpClientName);
            using var resp = await http.PostAsync(s.TokenUri, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer", ["assertion"] = assertion,
            }), ct);
            var text = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) throw new InvalidOperationException($"OAuth {resp.StatusCode}: {text}");
            using var d = JsonDocument.Parse(text);
            var token = d.RootElement.GetProperty("access_token").GetString()!;
            var expires = d.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600;
            TokenCache[s.ClientEmail] = (token, DateTime.UtcNow.AddSeconds(expires - 300));
            return token;
        }
        finally { TokenLock.Release(); }
    }

    static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
