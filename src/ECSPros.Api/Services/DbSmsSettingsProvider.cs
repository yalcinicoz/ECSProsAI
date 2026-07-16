using System.Text.Json;
using ECSPros.Core.Application.Services;
using ECSPros.Shared.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Services;

/// <summary>
/// SMS ayarlarını DB'deki platform servis tanımından çözer: aktif sms-tipli
/// FirmPlatformIntegration (Credentials şifreli saklanır, EF converter çözer).
/// Firma geneli kayıt (FirmPlatformId null) platforma özele tercih edilir — OTP/bildirim
/// SMS'leri platform bağlamı taşımaz, tek gönderen başlığı kullanılır.
/// IMemoryCache 2 dk: SMS başına DB sorgusu atılmaz; admin değişikliği en geç 2 dk
/// içinde etkili olur. Kayıt yoksa/eksikse null → GesTelekomSmsService log yedeğine düşer.
/// (DbSmtpSettingsProvider ile aynı kalıp.)
/// </summary>
public class DbSmsSettingsProvider : ISmsSettingsProvider
{
    private const string CacheKey = "sms-settings-db";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    private readonly ICoreDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DbSmsSettingsProvider> _logger;

    public DbSmsSettingsProvider(ICoreDbContext db, IMemoryCache cache, ILogger<DbSmsSettingsProvider> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SmsSettings?> GetAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out SmsSettings? cached))
            return cached;

        SmsSettings? settings = null;
        try
        {
            var kayit = await _db.FirmPlatformIntegrations
                .Where(fi => fi.IsActive && fi.IntegrationService.ServiceType == "sms")
                .OrderBy(fi => fi.FirmPlatformId == null ? 0 : 1)
                .ThenBy(fi => fi.CreatedAt)
                .Select(fi => new { fi.IntegrationService.Code, fi.Credentials, fi.Settings })
                .FirstOrDefaultAsync(ct);

            if (kayit is not null)
            {
                // secret alanlar Credentials'ta, kalanlar Settings'te — birleşimde secret kazanır
                var degerler = new Dictionary<string, object>(kayit.Settings);
                foreach (var (k, v) in kayit.Credentials) degerler[k] = v;

                var username = GetString(degerler, "username");
                var password = GetString(degerler, "password");
                var origin   = GetString(degerler, "origin");

                if (!string.IsNullOrWhiteSpace(username)
                    && !string.IsNullOrWhiteSpace(password)
                    && !string.IsNullOrWhiteSpace(origin))
                {
                    settings = new SmsSettings(
                        kayit.Code,
                        GetString(degerler, "apiUrl"),
                        username,
                        password,
                        origin,
                        GetString(degerler, "sendPassword"));
                }
            }
        }
        catch (Exception ex)
        {
            // DB/çözme hatası SMS akışını düşürmesin — log yedeğine düşülür.
            _logger.LogWarning(ex, "SMS ayarları DB'den okunamadı; log yedeğine düşülüyor.");
        }

        _cache.Set(CacheKey, settings, CacheTtl);
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
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => je.ToString()
            },
            bool b => b ? "true" : "false",
            _ => v.ToString()
        };
    }
}
