using ECSPros.Api.Services.Store;
using ECSPros.Shared.Contracts.Tracking;

namespace ECSPros.Api.Services.Tracking.Adapters;

/// <summary>
/// GA4 Measurement Protocol (İE-4 Faz D-1): POST www.google-analytics.com/mp/collect?measurement_id&api_secret.
/// OAuth gerektirmez (Google tarafı server-side için tercih edilen yol — karar v2 #6). Yalnız
/// `sendServerSide` açıkken ve sipariş event'leri için (purchase/refund) — davranış event'leri tarayıcıdan gider.
/// client_id = tarayıcı _ga (yoksa üye hash'i / dedup id'den türetilir). Consent analytics=true şart (worker).
/// Test event'leri /debug/mp/collect doğrulama ucuna gider (gerçek mülke yazılmaz).
/// </summary>
public sealed class Ga4MeasurementProtocolAdapter(
    IHttpClientFactory httpFactory,
    ITrackingSettingsProvider settings) : ITrackingAdapter
{
    public string Code => "ga4";
    public string ConsentCategory => "analytics";

    public bool Supports(CommerceEvent e, TrackingServiceSettings s)
        => s.Bool("sendServerSide") && s.Get("measurementId") is not null
           && e.Name is CommerceEventNames.OrderCompleted or CommerceEventNames.Refund;

    public async Task<TrackingSendResult> SendAsync(CommerceEvent e, TrackingServiceSettings s, CancellationToken ct)
    {
        var secrets = await settings.GetSecretsAsync(e.FirmPlatformId, Code, ct);
        if (!secrets.TryGetValue("measurementProtocolApiSecret", out var secret) || string.IsNullOrWhiteSpace(secret))
            return TrackingSendResult.Fail("GA4 measurementProtocolApiSecret tanımlı değil.");

        var clientId = e.Client.GaClientId
                       ?? (e.Client.ExternalIdSha256 is { } ex ? $"{Math.Abs(ex.GetHashCode())}.{TrackingAdapterBase.UnixSaniye(e.OccurredAt)}" : null)
                       ?? $"{Math.Abs(e.DedupId.GetHashCode())}.{TrackingAdapterBase.UnixSaniye(e.OccurredAt)}";

        var parametreler = new Dictionary<string, object?>
        {
            ["currency"] = e.Currency,
            ["value"] = e.Value ?? e.Items.Sum(i => i.Price * i.Quantity - i.Discount),
            ["transaction_id"] = e.TransactionId ?? e.DedupId,
            ["items"] = e.Items.Select(i => new
            {
                item_id = i.ItemId, item_name = i.Name, item_brand = i.Brand, item_category = i.Category,
                item_variant = i.Variant, price = i.Price, quantity = i.Quantity, discount = i.Discount
            }).ToArray()
        };
        if (e.Extra.TryGetValue("shipping", out var sh) && decimal.TryParse(sh, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var shD)) parametreler["shipping"] = shD;
        if (e.Extra.TryGetValue("tax", out var tx) && decimal.TryParse(tx, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var txD)) parametreler["tax"] = txD;
        if (e.Extra.TryGetValue("coupon", out var cp) && !string.IsNullOrWhiteSpace(cp)) parametreler["coupon"] = cp;
        if (e.Source == "server") parametreler["engagement_time_msec"] = 1;

        var body = new Dictionary<string, object?>
        {
            ["client_id"] = clientId,
            ["timestamp_micros"] = TrackingAdapterBase.UnixSaniye(e.OccurredAt) * 1_000_000L,
            ["non_personalized_ads"] = !e.Consent.Personalization,
            ["events"] = new[] { new { name = TrackingAdapterBase.Ga4Adi(e.Name), @params = parametreler } }
        };
        if (e.Client.ExternalIdSha256 is { } uid) body["user_id"] = uid;

        var test = TrackingAdapterBase.TestMi(e);
        var url = $"https://www.google-analytics.com/{(test ? "debug/" : "")}mp/collect?measurement_id={Uri.EscapeDataString(s.Get("measurementId")!)}&api_secret={Uri.EscapeDataString(secret)}";
        var http = httpFactory.CreateClient(TrackingAdapterBase.HttpClientName);
        var sonuc = await TrackingAdapterBase.PostJsonAsync(http, url, body, ct);
        if (test && sonuc.Success && sonuc.ResponseSummary is { } oz && oz.Contains("validationMessages\":[{"))
            return TrackingSendResult.Fail("GA4 doğrulama uyarısı: " + oz, sonuc.HttpStatus);
        return sonuc;
    }
}
