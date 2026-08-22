using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ECSPros.Api.Services.Tracking;
using ECSPros.Shared.Contracts.Tracking;

namespace ECSPros.Api.Services.Store;

/// <summary>Head'e basılacak takip yapılandırması — YALNIZ public değerler (ID/container/label);
/// secret ASLA girmez. JSON olarak `window.ecspros.cfg`'ye yazılır.</summary>
public sealed record TrackingHeadModel(
    Guid FirmPlatformId,
    string? Gtm,
    bool GtmManagesGa4, bool GtmManagesAds, bool GtmManagesPixels,
    string? Ga4,
    string? AdsId, string? AdsPurchaseLabel, string? AdsAddToCartLabel, string? AdsBeginCheckoutLabel, bool AdsEnhanced,
    string? MetaPixel,
    string? TikTokPixel,
    string? UetTag,
    string? Clarity,
    string? PinterestTag,
    string? SearchConsoleMeta,
    bool ConsentBanner,
    string ConsentDefault,
    ConsentState? Consent,       // ms_consent çerezinden; yoksa üyenin son kaydı (member); yoksa null (banner gösterilir)
    string[] ServerEvents,       // tarayıcının /api/store/events'e de göndereceği event adları
    string? ConsentSource = null) // cookie | member (İE-6: üye senkronu — tarayıcı çerezi sessizce yazar)
{
    /// <summary>Baskıya değer bir şey var mı (en az bir ID)?</summary>
    public bool Any => Gtm is not null || Ga4 is not null || AdsId is not null || MetaPixel is not null
                       || TikTokPixel is not null || UetTag is not null || Clarity is not null || PinterestTag is not null;

    public string ToCfgJson() => JsonSerializer.Serialize(new
    {
        platform = FirmPlatformId,
        gtm = Gtm, gtmManages = new { ga4 = GtmManagesGa4, ads = GtmManagesAds, pixels = GtmManagesPixels },
        ga4 = Ga4,
        ads = AdsId is null ? null : new { id = AdsId, purchase = AdsPurchaseLabel, addToCart = AdsAddToCartLabel, beginCheckout = AdsBeginCheckoutLabel, enhanced = AdsEnhanced },
        meta = MetaPixel, tiktok = TikTokPixel, uet = UetTag, clarity = Clarity, pinterest = PinterestTag,
        consentBanner = ConsentBanner,
        consentDefault = ConsentDefault,
        consent = Consent is null ? null : new { analytics = Consent.Analytics, ads = Consent.Ads, personalization = Consent.Personalization },
        consentSource = ConsentSource,
        serverEvents = ServerEvents
    }, OutboxCommerceEventPublisher.JsonAyar);
}

public interface ITrackingScriptProvider
{
    /// <summary>İstek için head modeli — bot UA'sı / kanal yok / hiç aktif takip entegrasyonu yoksa null
    /// (hiçbir script basılmaz). Hata fırlatmaz.</summary>
    Task<TrackingHeadModel?> GetAsync(HttpContext http, CancellationToken ct = default);
}

/// <summary>
/// İE-3 Faz C-1 (2026-08-22): kanalın aktif takip entegrasyonlarını (TrackingSettingsProvider, 2 dk cache)
/// head modeline çevirir. GTM "X GTM içinden yönetiliyor" bayrakları açıkken ilgili doğrudan script'ler
/// basılmaz (çift sayım koruması). Bot UA'larına hiçbir şey basılmaz.
/// </summary>
public sealed class TrackingScriptProvider(
    ITrackingSettingsProvider settings,
    IStoreContext storeContext,
    IStoreMemberSession memberSession,
    ECSPros.Integration.Application.Services.IIntegrationDbContext integrationDb,
    ILogger<TrackingScriptProvider> logger) : ITrackingScriptProvider
{
    // UrunListesiController.BotImzalari ile aynı küme (benzer ürünler crawler koruması, 2026-08-15)
    private static readonly string[] BotImzalari =
    [
        "bot", "crawler", "spider", "crawl", "slurp", "externalagent", "facebookexternalhit",
        "meta-external", "python-requests", "curl/", "wget/", "go-http-client", "okhttp",
        "headless", "preview", "fetcher", "scrapy", "ahrefs", "semrush", "mj12", "yandex",
        "bingpreview", "petalbot", "bytespider", "gptbot", "claudebot", "ccbot", "applebot",
    ];

    public static bool BotMu(string? ua)
    {
        if (string.IsNullOrEmpty(ua)) return true;
        foreach (var imza in BotImzalari)
            if (ua.Contains(imza, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public async Task<TrackingHeadModel?> GetAsync(HttpContext http, CancellationToken ct = default)
    {
        try
        {
            if (BotMu(http.Request.Headers.UserAgent.ToString())) return null;
            var platform = await storeContext.GetPlatformAsync(ct);
            if (platform is null) return null;

            var s = await settings.GetAsync(platform.Id, ct);
            if (!s.Any) return null;

            var gtm = s.Servis("gtm");
            var ga4 = s.Servis("ga4");
            var ads = s.Servis("google_ads");
            var meta = s.Servis("meta");
            var tiktok = s.Servis("tiktok");
            var uet = s.Servis("microsoft_ads");
            var clarity = s.Servis("microsoft_clarity");
            var pin = s.Servis("pinterest");
            var gsc = s.Servis("google_search_console");

            var gtmId = gtm?.Get("containerId");
            var manageGa4 = gtmId is not null && gtm!.Bool("manageGa4");
            var manageAds = gtmId is not null && gtm!.Bool("manageAds");
            var managePixels = gtmId is not null && gtm!.Bool("managePixels");

            ConsentState? consentRaw = null; string? consentSource = null;
            if (http.Request.Cookies.ContainsKey(TrackingHttpContextReader.ConsentCookie))
            {
                consentRaw = TrackingHttpContextReader.ReadConsent(http); consentSource = "cookie";
            }
            else
            {
                // İE-6 Faz F: çerez yok ama üye girişli → üyenin SON kaydı (cihazlar arası senkron)
                try
                {
                    var uye = await memberSession.MevcutUyeAsync(http);
                    if (uye is not null)
                    {
                        var son = await integrationDb.TrackingConsentLogs.AsNoTracking()
                            .Where(c => c.MemberId == uye.MemberId)
                            .OrderByDescending(c => c.CreatedAt)
                            .Select(c => new { c.Analytics, c.Ads, c.Personalization })
                            .FirstOrDefaultAsync(ct);
                        if (son is not null)
                        {
                            consentRaw = new ConsentState(son.Analytics, son.Ads, son.Personalization);
                            consentSource = "member";
                        }
                    }
                }
                catch (Exception ex) { logger.LogDebug(ex, "Üye consent senkronu okunamadı."); }
            }

            var serverEvents = new List<string>();
            // Meta CAPI açıksa tarayıcı davranış event'lerini sunucuya da yollar (dedup event_id ile) — karar §7-2
            if (meta is not null && meta.Bool("conversionApiEnabled") && meta.Get("pixelId") is not null)
                serverEvents.AddRange(new[] { CommerceEventNames.AddedToCart, CommerceEventNames.CheckoutStarted, CommerceEventNames.ProductViewed });

            var model = new TrackingHeadModel(
                platform.Id,
                gtmId, manageGa4, manageAds, managePixels,
                manageGa4 ? null : ga4?.Get("measurementId"),
                manageAds ? null : ads?.Get("conversionId"),
                ads?.Get("purchaseLabel"), ads?.Get("addToCartLabel"), ads?.Get("beginCheckoutLabel"),
                ads?.Bool("enhancedConversions") ?? false,
                managePixels ? null : meta?.Get("pixelId"),
                managePixels ? null : tiktok?.Get("pixelId"),
                managePixels ? null : uet?.Get("uetTagId"),
                clarity?.Get("projectId"),
                managePixels ? null : pin?.Get("tagId"),
                gsc?.Get("verificationCode"),
                s.ConsentBanner, s.ConsentDefault, consentRaw, serverEvents.ToArray(), consentSource);
            return model.Any ? model : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Takip head modeli üretilemedi — script basılmadı.");
            return null;
        }
    }
}
