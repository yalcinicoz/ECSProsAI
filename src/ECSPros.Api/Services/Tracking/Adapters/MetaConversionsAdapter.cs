using ECSPros.Api.Services.Store;
using ECSPros.Shared.Contracts.Tracking;

namespace ECSPros.Api.Services.Tracking.Adapters;

/// <summary>
/// Meta Conversions API (İE-4 Faz D-1): POST graph.facebook.com/{v}/{pixelId}/events.
/// event_id = DedupId (Pixel ile dedup), user_data hash'li (em/ph/external_id) + fbp/fbc + IP/UA.
/// Yalnız `conversionApiEnabled` açık kanalda ve consent.ads=true ise (worker kontrol eder).
/// Test event'leri (panel) YALNIZ `testEventCode` doluyken gönderilir — canlı pixel'e sahte Purchase
/// düşmesin. Token yalnız burada çözülür, loglanmaz.
/// </summary>
public sealed class MetaConversionsAdapter(
    IHttpClientFactory httpFactory,
    ITrackingSettingsProvider settings) : ITrackingAdapter
{
    public const string GraphVersion = "v20.0";
    public string Code => "meta";
    public string ConsentCategory => "ads";

    public bool Supports(CommerceEvent e, TrackingServiceSettings s)
        => s.Bool("conversionApiEnabled") && s.Get("pixelId") is not null && TrackingAdapterBase.MetaAdi(e.Name) is not null;

    public async Task<TrackingSendResult> SendAsync(CommerceEvent e, TrackingServiceSettings s, CancellationToken ct)
    {
        var pixelId = s.Get("pixelId")!;
        var testCode = s.Get("testEventCode");
        if (TrackingAdapterBase.TestMi(e) && string.IsNullOrWhiteSpace(testCode))
            return TrackingSendResult.Fail("Test event için Meta 'testEventCode' ayarı gerekli (canlı pixel'e test düşmez).");

        var secrets = await settings.GetSecretsAsync(e.FirmPlatformId, Code, ct);
        if (!secrets.TryGetValue("accessToken", out var token) || string.IsNullOrWhiteSpace(token))
            return TrackingSendResult.Fail("Meta accessToken tanımlı değil.");

        var userData = new Dictionary<string, object?>();
        void Ekle(string k, string? v) { if (!string.IsNullOrWhiteSpace(v)) userData[k] = v; }
        if (e.Client.EmailSha256 is { } em) userData["em"] = new[] { em };
        if (e.Client.PhoneSha256 is { } ph) userData["ph"] = new[] { ph };
        if (e.Client.ExternalIdSha256 is { } ex) userData["external_id"] = new[] { ex };
        Ekle("client_ip_address", e.Client.Ip);
        Ekle("client_user_agent", e.Client.UserAgent);
        Ekle("fbp", e.Client.Fbp);
        Ekle("fbc", e.Client.Fbc);

        var custom = new Dictionary<string, object?>
        {
            ["currency"] = e.Currency,
            ["value"] = e.Value ?? e.Items.Sum(i => i.Price * i.Quantity - i.Discount),
            ["content_type"] = "product",
            ["content_ids"] = e.Items.Select(i => i.ItemId).ToArray(),
            ["contents"] = e.Items.Select(i => new { id = i.ItemId, quantity = i.Quantity, item_price = i.Price }).ToArray(),
            ["num_items"] = e.Items.Sum(i => i.Quantity)
        };
        if (e.TransactionId is not null) custom["order_id"] = e.TransactionId;
        if (e.Extra.TryGetValue("search_term", out var st)) custom["search_string"] = st;
        if (e.Items.Count > 0) custom["content_name"] = e.Items[0].Name;

        var body = new Dictionary<string, object?>
        {
            ["data"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["event_name"] = TrackingAdapterBase.MetaAdi(e.Name),
                    ["event_time"] = TrackingAdapterBase.UnixSaniye(e.OccurredAt),
                    ["event_id"] = e.DedupId,
                    ["action_source"] = e.Source == "mobile" ? "app" : "website",
                    ["event_source_url"] = e.Client.PageUrl,
                    ["user_data"] = userData,
                    ["custom_data"] = custom
                }
            }
        };
        if (!string.IsNullOrWhiteSpace(testCode)) body["test_event_code"] = testCode;

        var http = httpFactory.CreateClient(TrackingAdapterBase.HttpClientName);
        var url = $"https://graph.facebook.com/{GraphVersion}/{Uri.EscapeDataString(pixelId)}/events?access_token={Uri.EscapeDataString(token)}";
        var sonuc = await TrackingAdapterBase.PostJsonAsync(http, url, body, ct);
        // token URL'de — hata mesajına URL girmez (PostJsonAsync yalnız gövdeyi özetler)
        return sonuc;
    }
}
