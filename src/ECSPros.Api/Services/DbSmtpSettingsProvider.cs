using System.Text.Json;
using ECSPros.Core.Application.Services;
using ECSPros.Shared.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Services;

/// <summary>
/// SMTP ayarlarını DB'deki platform servis tanımından çözer: aktif email-tipli
/// FirmPlatformIntegration (Credentials şifreli saklanır, EF converter çözer).
/// Firma geneli kayıt (FirmPlatformId null) platforma özele tercih edilir — e-posta
/// gönderen bildirim akışları platform bağlamı taşımaz, tek gönderen kimliği kullanılır.
/// IMemoryCache 2 dk: e-posta başına DB sorgusu atılmaz; admin değişikliği en geç
/// 2 dk içinde etkili olur. Kayıt yoksa null → SmtpEmailService config yedeğine düşer.
/// </summary>
public class DbSmtpSettingsProvider : ISmtpSettingsProvider
{
    private const string CacheKey = "smtp-settings-db";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    private readonly ICoreDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DbSmtpSettingsProvider> _logger;

    public DbSmtpSettingsProvider(ICoreDbContext db, IMemoryCache cache, ILogger<DbSmtpSettingsProvider> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SmtpSettings?> GetAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out SmtpSettings? cached))
            return cached;

        SmtpSettings? settings = null;
        try
        {
            var kayit = await _db.FirmPlatformIntegrations
                .Where(fi => fi.IsActive && fi.IntegrationService.ServiceType == "email")
                .OrderBy(fi => fi.FirmPlatformId == null ? 0 : 1)
                .ThenBy(fi => fi.CreatedAt)
                .Select(fi => new { fi.Credentials, fi.Settings })
                .FirstOrDefaultAsync(ct);

            if (kayit is not null)
            {
                // secret alanlar Credentials'ta, kalanlar Settings'te — birleşimde secret kazanır
                var degerler = new Dictionary<string, object>(kayit.Settings);
                foreach (var (k, v) in kayit.Credentials) degerler[k] = v;

                var host = GetString(degerler, "host");
                if (!string.IsNullOrWhiteSpace(host))
                {
                    settings = new SmtpSettings(
                        host,
                        int.TryParse(GetString(degerler, "port"), out var port) ? port : 587,
                        GetString(degerler, "user"),
                        GetString(degerler, "password"),
                        GetString(degerler, "from"),
                        GetString(degerler, "fromName"),
                        !string.Equals(GetString(degerler, "useSsl"), "false", StringComparison.OrdinalIgnoreCase));
                }
            }
        }
        catch (Exception ex)
        {
            // DB/çözme hatası e-posta akışını düşürmesin — config yedeğine düşülür.
            _logger.LogWarning(ex, "SMTP ayarları DB'den okunamadı; config yedeğine düşülüyor.");
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
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => je.ToString()
            },
            _ => v.ToString()
        };
    }
}
