using ECSPros.Api.Services.Store;
using ECSPros.Shared.Contracts.Tracking;

namespace ECSPros.Api.Services.Tracking.Adapters;

/// <summary>
/// TikTok Events API v1.3 (İE-4 Faz D-1): POST business-api.tiktok.com/open_api/v1.3/event/track/
/// (Access-Token başlığı). event_id = DedupId (Pixel dedup), user hash'li e-posta/telefon + ttclid + IP/UA.
/// Test event'leri yalnız `testEventCode` (TikTok "test_event_code") doluyken gönderilir.
/// </summary>
public sealed class TikTokEventsAdapter(
    IHttpClientFactory httpFactory,
    ITrackingSettingsProvider settings) : ITrackingAdapter
{
    public string Code => "tiktok";
    public string ConsentCategory => "ads";

    public bool Supports(CommerceEvent e, TrackingServiceSettings s)
        => s.Bool("eventsApiEnabled") && s.Get("pixelId") is not null && TrackingAdapterBase.TikTokAdi(e.Name) is not null;

    public async Task<TrackingSendResult> SendAsync(CommerceEvent e, TrackingServiceSettings s, CancellationToken ct)
    {
        var testCode = s.Get("testEventCode");
        if (TrackingAdapterBase.TestMi(e) && string.IsNullOrWhiteSpace(testCode))
            return TrackingSendResult.Fail("Test event için TikTok 'testEventCode' ayarı gerekli.");

        var secrets = await settings.GetSecretsAsync(e.FirmPlatformId, Code, ct);
        if (!secrets.TryGetValue("accessToken", out var token) || string.IsNullOrWhiteSpace(token))
            return TrackingSendResult.Fail("TikTok accessToken tanımlı değil.");

        var user = new Dictionary<string, object?>();
        if (e.Client.EmailSha256 is { } em) user["email"] = em;
        if (e.Client.PhoneSha256 is { } ph) user["phone"] = ph;
        if (e.Client.ExternalIdSha256 is { } ex) user["external_id"] = ex;
        if (e.Client.Ip is { } ip) user["ip"] = ip;
        if (e.Client.UserAgent is { } ua) user["user_agent"] = ua;
        if (e.Client.TtClickId is { } tt) user["ttclid"] = tt;

        var props = new Dictionary<string, object?>
        {
            ["currency"] = e.Currency,
            ["value"] = e.Value ?? e.Items.Sum(i => i.Price * i.Quantity - i.Discount),
            ["content_type"] = "product",
            ["contents"] = e.Items.Select(i => new { content_id = i.ItemId, content_name = i.Name, quantity = i.Quantity, price = i.Price }).ToArray()
        };
        if (e.TransactionId is not null) props["order_id"] = e.TransactionId;
        if (e.Extra.TryGetValue("search_term", out var st)) props["query"] = st;

        var body = new Dictionary<string, object?>
        {
            ["event_source"] = e.Source == "mobile" ? "app" : "web",
            ["event_source_id"] = s.Get("pixelId"),
            ["data"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["event"] = TrackingAdapterBase.TikTokAdi(e.Name),
                    ["event_time"] = TrackingAdapterBase.UnixSaniye(e.OccurredAt),
                    ["event_id"] = e.DedupId,
                    ["user"] = user,
                    ["properties"] = props,
                    ["page"] = new { url = e.Client.PageUrl, referrer = e.Client.Referrer }
                }
            }
        };
        if (!string.IsNullOrWhiteSpace(testCode)) body["test_event_code"] = testCode;

        var http = httpFactory.CreateClient(TrackingAdapterBase.HttpClientName);
        return await TrackingAdapterBase.PostJsonAsync(http, "https://business-api.tiktok.com/open_api/v1.3/event/track/", body, ct,
            req => req.Headers.TryAddWithoutValidation("Access-Token", token));
    }
}
