using ECSPros.Api.Services;
using ECSPros.Api.Services.Tracking;
using ECSPros.Shared.Contracts.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECSPros.Api.Controllers;

/// <summary>
/// İE-2 Faz B-6: tarayıcı (site.js — sendServer:true) ve MOBİL uygulama commerce event ucu.
/// Gövde yalnız public veri taşır; sunucu FirmPlatformId/IP/UA/MemberId/consent'i ekler ve
/// outbox'a yazar (server-side adapter'lar Faz D). Davranış event'leri için zorunlu DEĞİL —
/// GA4/Pixel tarayıcıdan zaten gider; bu uç server-side tekrarı (Meta CAPI AddToCart vb.) ve
/// mobil (tarayıcı pixel'i yok) içindir. Geçersiz ad → 400. Tracking kapalıyken 200 döner
/// (istemci ayrım yapmaz). Referans: docs/mobil-api-referansi.md "Commerce event".
/// </summary>
[ApiController]
[Route("api/store/events")]
[EnableRateLimiting("store-sensitive")]
public class StoreEventsController(
    ICommerceEventPublisher publisher,
    IStoreContext storeContext) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Post([FromBody] StoreEventRequest req, CancellationToken ct)
    {
        if (!CommerceEventNames.IsValid(req.Name))
            return BadRequest(new { success = false, error = "Geçersiz event adı.", allowed = CommerceEventNames.All.OrderBy(x => x) });
        if (req.Name is CommerceEventNames.OrderCompleted or CommerceEventNames.Refund)
            return BadRequest(new { success = false, error = "order_completed/refund yalnız sunucu tarafından üretilir." });

        var platformId = req.FirmPlatformId ?? (await storeContext.GetPlatformAsync(ct))?.Id;
        if (platformId is null || platformId == Guid.Empty)
            return BadRequest(new { success = false, error = "Kanal çözülemedi (firmPlatformId)." });

        Guid? memberId = null;
        if (User.FindFirst("type")?.Value == "member")
        {
            var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(sub, out var mid)) memberId = mid;
        }

        var client = TrackingHttpContextReader.ReadClient(HttpContext, null, null, memberId);
        if (req.Client is { } c)
        {
            client = client with
            {
                Fbp = c.Fbp ?? client.Fbp, Fbc = c.Fbc ?? client.Fbc, GaClientId = c.GaClientId ?? client.GaClientId,
                TtClickId = c.Ttclid ?? client.TtClickId, Gclid = c.Gclid ?? client.Gclid,
                PageUrl = c.PageUrl ?? client.PageUrl, Referrer = c.Referrer ?? client.Referrer
            };
        }
        var consent = req.Consent is { } rc
            ? new ConsentState(rc.Analytics, rc.Ads, rc.Personalization)   // mobil: uygulama içi izin ekranı
            : TrackingHttpContextReader.ReadConsent(HttpContext);

        var items = (req.Items ?? new()).Take(100).Select(i => new CommerceItem(
            i.ItemId ?? "", i.ItemGroupId ?? "", i.Name ?? "", i.Brand, i.Category, i.Variant,
            i.Price ?? 0m, i.Quantity ?? 1, i.Discount ?? 0m)).ToList();
        var extra = (req.Extra ?? new()).Take(20).ToDictionary(k => k.Key, k => k.Value ?? "");
        var source = string.Equals(req.Source, "mobile", StringComparison.OrdinalIgnoreCase) ? "mobile" : "web";

        await publisher.PublishAsync(new CommerceEvent(
            Name: req.Name!,
            OccurredAt: DateTime.UtcNow,
            FirmPlatformId: platformId.Value,
            DedupId: string.IsNullOrWhiteSpace(req.DedupId) ? Guid.NewGuid().ToString("D") : req.DedupId.Trim()[..Math.Min(100, req.DedupId.Trim().Length)],
            Source: source,
            MemberId: memberId,
            Currency: string.IsNullOrWhiteSpace(req.Currency) ? "TRY" : req.Currency!,
            Value: req.Value,
            TransactionId: req.TransactionId,
            Items: items,
            Client: client,
            Consent: consent,
            Extra: extra), ct);

        return Ok(new { success = true });
    }
}

public record StoreEventRequest(
    string? Name,
    Guid? FirmPlatformId = null,
    string? DedupId = null,
    string? Source = null,                       // web | mobile
    string? Currency = null,
    decimal? Value = null,
    string? TransactionId = null,
    List<StoreEventItem>? Items = null,
    Dictionary<string, string?>? Extra = null,
    StoreEventClient? Client = null,
    StoreEventConsent? Consent = null);

public record StoreEventItem(string? ItemId, string? ItemGroupId, string? Name, string? Brand, string? Category,
    string? Variant, decimal? Price, int? Quantity, decimal? Discount);
public record StoreEventClient(string? Fbp, string? Fbc, string? GaClientId, string? Ttclid, string? Gclid, string? PageUrl, string? Referrer);
public record StoreEventConsent(bool Analytics, bool Ads, bool Personalization);
