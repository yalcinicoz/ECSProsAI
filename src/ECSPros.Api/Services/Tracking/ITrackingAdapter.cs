using ECSPros.Api.Services.Store;
using ECSPros.Shared.Contracts.Tracking;

namespace ECSPros.Api.Services.Tracking;

public sealed record TrackingSendResult(bool Success, string? Error = null, int? HttpStatus = null, string? ResponseSummary = null)
{
    public static TrackingSendResult Ok(int? http = null, string? summary = null) => new(true, null, http, summary);
    public static TrackingSendResult Fail(string error, int? http = null) => new(false, error, http);
}

/// <summary>
/// Server-side takip adapter'ı (İE-2 sözleşme; uygulamalar Faz D: MetaConversionsAdapter,
/// TikTokEventsAdapter, Ga4MeasurementProtocolAdapter, PinterestConversionsAdapter).
/// Worker: kanalda <see cref="Code"/>'lu entegrasyon aktif + Supports + consent kategorisi izinli ise SendAsync.
/// Secret'lar yalnız SendAsync içinde ITrackingSettingsProvider.GetSecretsAsync ile çözülür; ASLA loglanmaz.
/// </summary>
public interface ITrackingAdapter
{
    /// <summary>IntegrationService.Code ile birebir (meta | tiktok | ga4 | pinterest).</summary>
    string Code { get; }

    /// <summary>Consent kategorisi: "ads" (Meta/TikTok/Pinterest) | "analytics" (GA4).</summary>
    string ConsentCategory { get; }

    bool Supports(CommerceEvent e, TrackingServiceSettings settings);

    Task<TrackingSendResult> SendAsync(CommerceEvent e, TrackingServiceSettings settings, CancellationToken ct);
}
