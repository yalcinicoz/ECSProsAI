using System.Text.Json;
using ECSPros.Core.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Services;

public sealed record VisualSearchSettings(string ApiUrl, string ApiKey);

public interface IVisualSearchSettingsProvider
{
    /// <summary>Aktif görsel arama ayarı — platforma özel kayıt tercih edilir; yoksa firma
    /// geneli; DB'de hiç yoksa config (`VisualSearch:ApiUrl/ApiKey`); o da yoksa null (özellik kapalı).</summary>
    Task<VisualSearchSettings?> GetAsync(Guid? firmPlatformId, CancellationToken ct = default);
}

/// <summary>
/// Görsel arama ayarlarını DB'deki platform servis tanımından çözer (DbSmtpSettingsProvider
/// kalıbı): aktif `visual_search` tipli FirmPlatformIntegration — apiUrl Settings'te, apiKey
/// şifreli Credentials'ta (EF converter çözer). IMemoryCache 2 dk; admin değişikliği en geç
/// 2 dk içinde etkili olur.
/// </summary>
public class DbVisualSearchSettingsProvider : IVisualSearchSettingsProvider
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    private readonly ICoreDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;
    private readonly ILogger<DbVisualSearchSettingsProvider> _logger;

    public DbVisualSearchSettingsProvider(
        ICoreDbContext db, IMemoryCache cache, IConfiguration config,
        ILogger<DbVisualSearchSettingsProvider> logger)
    {
        _db = db;
        _cache = cache;
        _config = config;
        _logger = logger;
    }

    public async Task<VisualSearchSettings?> GetAsync(Guid? firmPlatformId, CancellationToken ct = default)
    {
        var cacheKey = $"visual-search-settings-{firmPlatformId?.ToString() ?? "none"}";
        if (_cache.TryGetValue(cacheKey, out VisualSearchSettings? cached))
            return cached;

        VisualSearchSettings? settings = null;
        try
        {
            var kayit = await _db.FirmPlatformIntegrations
                .Where(fi => fi.IsActive && fi.IntegrationService.ServiceType == "visual_search"
                    && (fi.FirmPlatformId == null || fi.FirmPlatformId == firmPlatformId))
                // Platforma özel kayıt firma geneline tercih edilir
                .OrderBy(fi => fi.FirmPlatformId == null ? 1 : 0)
                .ThenBy(fi => fi.CreatedAt)
                .Select(fi => new { fi.Credentials, fi.Settings })
                .FirstOrDefaultAsync(ct);

            if (kayit is not null)
            {
                var degerler = new Dictionary<string, object>(kayit.Settings);
                foreach (var (k, v) in kayit.Credentials) degerler[k] = v;

                var apiUrl = GetString(degerler, "apiUrl");
                var apiKey = GetString(degerler, "apiKey");
                if (!string.IsNullOrWhiteSpace(apiUrl) && !string.IsNullOrWhiteSpace(apiKey))
                    settings = new VisualSearchSettings(apiUrl, apiKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Görsel arama ayarları DB'den okunamadı; config yedeğine düşülüyor.");
        }

        // Config yedeği — DB kaydı yoksa (SMTP kalıbıyla aynı sıra)
        if (settings is null)
        {
            var cfgUrl = _config["VisualSearch:ApiUrl"];
            var cfgKey = _config["VisualSearch:ApiKey"];
            if (!string.IsNullOrWhiteSpace(cfgUrl) && !string.IsNullOrWhiteSpace(cfgKey))
                settings = new VisualSearchSettings(cfgUrl, cfgKey);
        }

        _cache.Set(cacheKey, settings, CacheTtl);
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
}
