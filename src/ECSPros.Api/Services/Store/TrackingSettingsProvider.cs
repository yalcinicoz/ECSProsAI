using System.Text.Json;
using ECSPros.Core.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Services.Store;

/// <summary>Bir takip servisinin kanal için çözülmüş PUBLIC ayarları (secret İÇERMEZ —
/// tarayıcıya basılabilir). Boolean alanlar string "true"/"false" olarak da gelebilir; <see cref="Bool"/>
/// ile oku.</summary>
public sealed record TrackingServiceSettings(
    string Code,
    string ServiceType,
    Guid FirmPlatformIntegrationId,
    bool PlatformaOzel,
    string Ownership,                                  // customer | platform (karar §7-10)
    IReadOnlyDictionary<string, string> Settings)
{
    public string? Get(string key) => Settings.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;
    public bool Bool(string key) => Get(key) is { } v && (v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1");
}

/// <summary>Kanalın tüm takip ayarları: aktif servisler (code → ayar) + FirmPlatform.Settings."tracking".</summary>
public sealed record TrackingSettings(
    Guid FirmPlatformId,
    IReadOnlyDictionary<string, TrackingServiceSettings> Services,
    bool ConsentBanner,
    string ConsentDefault,          // deny | grant  (EU kararı: deny)
    string PurchaseAt,              // confirmed | created
    string? BannerTitle = null,     // İE-6: banner metinleri (null = partial varsayılanı)
    string? BannerText = null,
    string? PolicyUrl = null,
    string? PolicyLabel = null)
{
    public static readonly TrackingSettings Bos = new(Guid.Empty,
        new Dictionary<string, TrackingServiceSettings>(), true, "deny", "confirmed");

    public bool Any => Services.Count > 0;
    public TrackingServiceSettings? Servis(string code) => Services.TryGetValue(code, out var s) ? s : null;
}

public interface ITrackingSettingsProvider
{
    /// <summary>Kanalın aktif takip entegrasyonları (public ayarlar) — IMemoryCache 2 dk.
    /// Kayıt yoksa <see cref="TrackingSettings.Bos"/> (Any=false) → hiçbir script/event yok
    /// (Telemania varsayılan KAPALI). Asla exception fırlatmaz.</summary>
    Task<TrackingSettings> GetAsync(Guid firmPlatformId, CancellationToken ct = default);

    /// <summary>Bir servisin şifreli Credentials'ı (accessToken/apiSecret/...) — YALNIZ sunucu
    /// taraflı adapter'lar (Faz D) çağırır; tarayıcıya/log'a ASLA. Servis aktif değilse boş.</summary>
    Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(Guid firmPlatformId, string code, CancellationToken ct = default);

    /// <summary>Admin değişikliği sonrası cache'i düşür (isteğe bağlı; TTL zaten 2 dk).</summary>
    void Invalidate(Guid firmPlatformId);
}

/// <summary>
/// Takip/reklam ayar çözümleyicisi (İE-1 Faz A, 2026-08-22 — DbSmtpSettingsProvider /
/// SocialLoginSettingsProvider kalıbı): aktif takip-tipli FirmPlatformIntegration kayıtlarını
/// kanal için çözer — kanala özel kayıt (FirmPlatformId dolu) firma geneline (null) TERCİH edilir,
/// her servis kodu için tek kayıt. Public ayarlar (ID/container/label/boolean) cache'lenir;
/// secret'lar ayrı çağrıyla ve ayrı cache anahtarıyla çözülür, hiçbir zaman loglanmaz.
/// </summary>
public class TrackingSettingsProvider(
    ICoreDbContext db,
    IMemoryCache cache,
    ECSPros.Shared.Contracts.ICacheBustPublisher cacheBust,
    ILogger<TrackingSettingsProvider> logger) : ITrackingSettingsProvider
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    /// <summary>DatabaseSeeder.TakipServisTipleri ile birebir aynı küme.</summary>
    public static readonly string[] ServiceTypes =
    {
        "analytics", "tag_manager", "ads", "merchant", "search_console",
        "meta", "tiktok", "pinterest", "microsoft_ads", "clarity"
    };

    private static string PublicKey(Guid id) => $"tracking-settings:{id}";
    private static string SecretKey(Guid id, string code) => $"tracking-secrets:{id}:{code}";

    public async Task<TrackingSettings> GetAsync(Guid firmPlatformId, CancellationToken ct = default)
    {
        if (firmPlatformId == Guid.Empty) return TrackingSettings.Bos;
        if (cache.TryGetValue(PublicKey(firmPlatformId), out TrackingSettings? cached) && cached is not null)
            return cached;

        var sonuc = TrackingSettings.Bos with { FirmPlatformId = firmPlatformId };
        try
        {
            var kayitlar = await db.FirmPlatformIntegrations
                .Where(fi => fi.IsActive
                             && ServiceTypes.Contains(fi.IntegrationService.ServiceType)
                             && (fi.FirmPlatformId == null || fi.FirmPlatformId == firmPlatformId))
                .OrderBy(fi => fi.IntegrationService.Code)
                .ThenBy(fi => fi.FirmPlatformId == null ? 1 : 0)   // kanala özel ÖNCE
                .ThenBy(fi => fi.CreatedAt)
                .Select(fi => new
                {
                    fi.Id,
                    fi.FirmPlatformId,
                    fi.IntegrationService.Code,
                    fi.IntegrationService.ServiceType,
                    fi.Settings
                })
                .ToListAsync(ct);

            var servisler = new Dictionary<string, TrackingServiceSettings>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in kayitlar)
            {
                if (servisler.ContainsKey(k.Code)) continue; // ilk (kanala özel) kazanır
                var ayarlar = Duzlestir(k.Settings);
                var ownership = ayarlar.TryGetValue("ownership", out var o) && !string.IsNullOrWhiteSpace(o)
                    ? o.Trim().ToLowerInvariant() : "customer";
                servisler[k.Code] = new TrackingServiceSettings(
                    k.Code, k.ServiceType, k.Id, k.FirmPlatformId is not null, ownership, ayarlar);
            }

            // FirmPlatform.Settings."tracking" — UpdateTrackingSettingsCommand yazar; yoksa varsayılanlar
            var platformSettings = await db.FirmPlatforms
                .Where(p => p.Id == firmPlatformId)
                .Select(p => p.Settings)
                .FirstOrDefaultAsync(ct);
            var (banner, consentDefault, purchaseAt) = TrackingAyarlariOku(platformSettings);
            var tr = TrackingSozlugu(platformSettings);
            string? Metin(string k) => tr.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

            sonuc = new TrackingSettings(firmPlatformId, servisler, banner, consentDefault, purchaseAt,
                Metin("bannerTitle"), Metin("bannerText"), Metin("policyUrl"), Metin("policyLabel"));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Takip ayarları DB'den okunamadı (platform {PlatformId}) — takip kapalı sayıldı.", firmPlatformId);
        }

        cache.Set(PublicKey(firmPlatformId), sonuc, CacheTtl);
        return sonuc;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(
        Guid firmPlatformId, string code, CancellationToken ct = default)
    {
        var key = SecretKey(firmPlatformId, code);
        if (cache.TryGetValue(key, out IReadOnlyDictionary<string, string>? cached) && cached is not null)
            return cached;

        IReadOnlyDictionary<string, string> sonuc = new Dictionary<string, string>();
        try
        {
            var kayit = await db.FirmPlatformIntegrations
                .Where(fi => fi.IsActive
                             && fi.IntegrationService.Code == code
                             && ServiceTypes.Contains(fi.IntegrationService.ServiceType)
                             && (fi.FirmPlatformId == null || fi.FirmPlatformId == firmPlatformId))
                .OrderBy(fi => fi.FirmPlatformId == null ? 1 : 0)
                .ThenBy(fi => fi.CreatedAt)
                .Select(fi => new { fi.Credentials })
                .FirstOrDefaultAsync(ct);
            if (kayit is not null) sonuc = Duzlestir(kayit.Credentials);
        }
        catch (Exception ex)
        {
            // secret değer asla loglanmaz — yalnız bağlam
            logger.LogWarning(ex, "Takip servisi kimlik bilgisi okunamadı (platform {PlatformId}, servis {Code}).", firmPlatformId, code);
        }

        cache.Set(key, sonuc, CacheTtl);
        return sonuc;
    }

    // FAZ 10 / A9: yerel silme + diğer düğümlere yayın (pub/sub; Redis'siz yalnız yerel).
    public void Invalidate(Guid firmPlatformId)
    {
        cacheBust.Bust(PublicKey(firmPlatformId));
        foreach (var code in new[] { "ga4", "gtm", "google_ads", "google_merchant", "google_search_console",
                     "meta", "tiktok", "pinterest", "microsoft_ads", "microsoft_clarity" })
            cacheBust.Bust(SecretKey(firmPlatformId, code));
    }

    /// <summary>Settings."tracking" sözlüğü (string→string); yoksa boş.</summary>
    internal static Dictionary<string, string> TrackingSozlugu(Dictionary<string, object>? platformSettings)
    {
        if (platformSettings is null || !platformSettings.TryGetValue("tracking", out var trObj) || trObj is null)
            return new Dictionary<string, string>();
        try
        {
            return trObj switch
            {
                Dictionary<string, object> d => Duzlestir(d),
                JsonElement je when je.ValueKind == JsonValueKind.Object =>
                    Duzlestir(je.EnumerateObject().ToDictionary(p => p.Name, p => (object)p.Value)),
                _ => new Dictionary<string, string>()
            };
        }
        catch { return new Dictionary<string, string>(); }
    }

    /// <summary>Settings."tracking" okuma — anahtar yoksa EU varsayılanları (banner açık, deny, confirmed).</summary>
    internal static (bool Banner, string ConsentDefault, string PurchaseAt) TrackingAyarlariOku(
        Dictionary<string, object>? platformSettings)
    {
        bool banner = true; string consentDefault = "deny"; string purchaseAt = "confirmed";
        var tr = TrackingSozlugu(platformSettings);
        if (tr.Count == 0) return (banner, consentDefault, purchaseAt);

        // EU kararı: banner/deny panelden değiştirilemez — jsonb'de farklı yazılsa bile burada sabitlenir
        banner = true;
        consentDefault = "deny";
        if (tr.TryGetValue("purchaseAt", out var pa) && pa is "created" or "confirmed") purchaseAt = pa;
        return (banner, consentDefault, purchaseAt);
    }

    /// <summary>jsonb sözlüğünü string→string'e indirger (JsonElement/bool/number → metin).</summary>
    private static Dictionary<string, string> Duzlestir(Dictionary<string, object>? kaynak)
    {
        var sonuc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (kaynak is null) return sonuc;
        foreach (var (k, v) in kaynak)
        {
            var deger = v switch
            {
                null => null,
                string s => s,
                bool b => b ? "true" : "false",
                JsonElement je => je.ValueKind switch
                {
                    JsonValueKind.String => je.GetString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null or JsonValueKind.Undefined => null,
                    JsonValueKind.Array or JsonValueKind.Object => je.GetRawText(),
                    _ => je.ToString()
                },
                _ => v.ToString()
            };
            if (deger is not null) sonuc[k] = deger;
        }
        return sonuc;
    }
}
