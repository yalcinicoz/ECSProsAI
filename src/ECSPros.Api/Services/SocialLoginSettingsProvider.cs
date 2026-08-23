using System.Text.Json;
using ECSPros.Core.Application.Services;
using ECSPros.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Services;

/// <summary>
/// Sosyal giriş (OAuth) ayarlarını DB'deki platform servis tanımından çözer:
/// aktif social_login-tipli FirmPlatformIntegration (ServiceType=social_login,
/// Code=google_oauth/facebook_oauth). ClientSecret şifreli Credentials'ta saklanır.
/// Platforma özel kayıt (FirmPlatformId dolu) firma-geneline tercih edilir.
/// IMemoryCache 2 dk: admin değişikliği en geç 2 dk içinde etkili olur. Kayıt yoksa
/// null → vitrin ilgili butonu gizler, OAuth akışı başlatmaz.
/// </summary>
public class SocialLoginSettingsProvider(
    ICoreDbContext db,
    IMemoryCache cache,
    ILogger<SocialLoginSettingsProvider> logger) : ISocialLoginSettingsProvider
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    public async Task<SocialLoginSettings?> GetAsync(
        string provider, Guid firmPlatformId, CancellationToken ct = default)
    {
        var kod = $"{provider}_oauth";
        var cacheKey = $"social-login:{firmPlatformId}:{kod}";

        if (cache.TryGetValue(cacheKey, out SocialLoginSettings? cached))
            return cached;

        SocialLoginSettings? settings = null;
        try
        {
            var kayit = await db.FirmPlatformIntegrations
                .Where(fi => fi.IsActive
                             && fi.IntegrationService.ServiceType == "social_login"
                             && fi.IntegrationService.Code == kod
                             && (fi.FirmPlatformId == null || fi.FirmPlatformId == firmPlatformId))
                .OrderBy(fi => fi.FirmPlatformId == null ? 0 : 1)
                .ThenBy(fi => fi.CreatedAt)
                .Select(fi => new { fi.Credentials, fi.Settings })
                .FirstOrDefaultAsync(ct);

            if (kayit is not null)
            {
                // secret alanlar Credentials'ta, kalanlar Settings'te — birleşimde secret kazanır
                var degerler = new Dictionary<string, object>(kayit.Settings);
                foreach (var (k, v) in kayit.Credentials) degerler[k] = v;

                var clientId = GetString(degerler, "clientId");
                var clientSecret = GetString(degerler, "clientSecret");
                if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
                {
                    settings = new SocialLoginSettings(
                        provider, clientId, clientSecret,
                        GetString(degerler, "redirectUri"),
                        ScopesGetir(degerler, provider),
                        GetString(degerler, "graphApiVersion"));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sosyal giriş ayarları DB'den okunamadı (provider {Provider}).", provider);
        }

        cache.Set(cacheKey, settings, CacheTtl);
        return settings;
    }

    private static string? GetString(Dictionary<string, object> values, string key)
    {
        if (!values.TryGetValue(key, out var v) || v is null) return null;
        return v switch
        {
            string s => s,
            JsonElement je => je.ValueKind switch
            {
                JsonValueKind.String => je.GetString(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => je.ToString()
            },
            _ => v.ToString()
        };
    }

    /// <summary>scopes alanı text olarak saklanır (boşluk/virgül ayrımlı); boşsa
    /// sağlayıcıya göre varsayılan döner (Google: openid email profile,
    /// Facebook: email public_profile).</summary>
    private static IReadOnlyList<string> ScopesGetir(Dictionary<string, object> values, string provider)
    {
        var ham = GetString(values, "scopes");
        if (!string.IsNullOrWhiteSpace(ham))
        {
            var parcalar = ham
                .Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (parcalar.Count > 0) return parcalar;
        }

        return provider == "facebook"
            ? new[] { "email", "public_profile" }
            : new[] { "openid", "email", "profile" };
    }
}
