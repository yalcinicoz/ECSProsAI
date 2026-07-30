using System.Text.Json;
using ECSPros.Core.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// PayTR ayarlarını DB'deki platform servis tanımından çözer: aktif payment-tipli
/// FirmPlatformIntegration (Credentials şifreli saklanır, EF converter çözer).
/// DbSmtpSettingsProvider deseni: firma geneli kayıt (FirmPlatformId null) platforma
/// özele tercih edilir, 2 dk IMemoryCache. Kayıt yoksa/eksikse null.
/// GÜVENLİK: bu sağlayıcı yalnız MAĞAZA kimliklerini (merchant_id/key/salt) döner —
/// KART verisiyle ilgisi yoktur. Çözülen değerler asla loglanmaz.
/// </summary>
public class DbPaymentSettingsProvider : IPaymentSettingsProvider
{
    private const string CacheKey = "paytr-settings-db";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    private readonly ICoreDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DbPaymentSettingsProvider> _logger;

    public DbPaymentSettingsProvider(ICoreDbContext db, IMemoryCache cache, ILogger<DbPaymentSettingsProvider> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<PaymentSettings?> GetAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out PaymentSettings? cached))
            return cached;

        PaymentSettings? settings = null;
        try
        {
            var kayit = await _db.FirmPlatformIntegrations
                .Where(fi => fi.IsActive && fi.IntegrationService.ServiceType == "payment")
                .OrderBy(fi => fi.FirmPlatformId == null ? 0 : 1)
                .ThenBy(fi => fi.CreatedAt)
                .Select(fi => new { fi.Credentials, fi.Settings })
                .FirstOrDefaultAsync(ct);

            if (kayit is not null)
            {
                var degerler = new Dictionary<string, object>(kayit.Settings);
                foreach (var (k, v) in kayit.Credentials) degerler[k] = v; // secret alanlar Credentials'ta

                var merchantId = GetString(degerler, "merchantId");
                var merchantKey = GetString(degerler, "merchantKey");
                var merchantSalt = GetString(degerler, "merchantSalt");
                if (!string.IsNullOrWhiteSpace(merchantId)
                    && !string.IsNullOrWhiteSpace(merchantKey)
                    && !string.IsNullOrWhiteSpace(merchantSalt))
                {
                    // testMode ŞU AN her koşulda AÇIK — canlı ödeme için PCI-DSS + PayTR onayı
                    // gerektiğinden, ayar "false" gelse bile true'ya zorlanır (güvenlik ağı).
                    settings = new PaymentSettings(merchantId!, merchantKey!, merchantSalt!, TestMode: true);
                }
            }
        }
        catch (Exception ex)
        {
            // Kart verisi/kimlik SIZDIRMAMAK için mesajda değer YOK — yalnız durum.
            _logger.LogWarning(ex, "PayTR ayarları DB'den okunamadı; ödeme yapılandırılmamış sayılır.");
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
