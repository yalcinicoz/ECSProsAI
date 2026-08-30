using ECSPros.Api.Services.Store;
using ECSPros.Api.Services.Tracking;
using ECSPros.Integration.Application.Services;
using ECSPros.Shared.Contracts.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Controllers;

/// <summary>
/// İE-4 Faz D-5 (2026-08-22): Pazarlama → "Takip &amp; Reklam" panel ucu — kanal bazlı takip
/// entegrasyonu durumu (son başarılı/hata, 24 saat sayıları), outbox özeti + listesi, yeniden
/// deneme ve test event gönderimi. Secret'lar hiçbir yanıta girmez (yalnız public ayar anahtarları).
/// </summary>
[ApiController]
[Route("api/tracking")]
[Authorize]
public class TrackingAdminController(
    ITrackingSettingsProvider settings,
    IIntegrationDbContext integrationDb,
    ICommerceEventPublisher publisher,
    IConfiguration config) : ControllerBase
{
    private static readonly string[] ClientCodes = { "ga4", "gtm", "google_ads", "meta", "tiktok", "pinterest", "microsoft_ads", "microsoft_clarity", "google_search_console" };

    [HttpGet("status")]
    public async Task<IActionResult> Status([FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        if (firmPlatformId == Guid.Empty) return BadRequest(new { success = false, error = "firmPlatformId gerekli." });
        settings.Invalidate(firmPlatformId); // panel her zaman taze görür
        var s = await settings.GetAsync(firmPlatformId, ct);
        var since = DateTime.UtcNow.AddHours(-24);
        var ids = s.Services.Values.Select(v => v.FirmPlatformIntegrationId).ToList();

        var loglar = ids.Count == 0 ? new() : await integrationDb.IntegrationLogs.AsNoTracking()
            .Where(l => ids.Contains(l.FirmIntegrationId) && l.OperationType.StartsWith("send_event"))
            .GroupBy(l => l.FirmIntegrationId)
            .Select(g => new
            {
                Id = g.Key,
                LastSuccessAt = g.Where(x => x.Status == "success" || x.Status == "dry_run").Max(x => (DateTime?)x.CreatedAt),
                LastFailureAt = g.Where(x => x.Status == "failure").Max(x => (DateTime?)x.CreatedAt),
                Ok24 = g.Count(x => (x.Status == "success" || x.Status == "dry_run") && x.CreatedAt >= since),
                Fail24 = g.Count(x => x.Status == "failure" && x.CreatedAt >= since)
            })
            .ToListAsync(ct);
        var sonHatalar = ids.Count == 0 ? new() : await integrationDb.IntegrationLogs.AsNoTracking()
            .Where(l => ids.Contains(l.FirmIntegrationId) && l.Status == "failure" && l.OperationType.StartsWith("send_event"))
            .GroupBy(l => l.FirmIntegrationId)
            .Select(g => new { Id = g.Key, Error = g.OrderByDescending(x => x.CreatedAt).Select(x => x.ErrorMessage).FirstOrDefault() })
            .ToListAsync(ct);

        var servisler = s.Services.Values.OrderBy(v => Array.IndexOf(ClientCodes, v.Code) is var i && i < 0 ? 99 : i).Select(v =>
        {
            var log = loglar.FirstOrDefault(l => l.Id == v.FirmPlatformIntegrationId);
            var modlar = new List<string>();
            if (ClientCodes.Contains(v.Code)) modlar.Add(v.Code == "gtm" ? "gtm" : "client");
            if ((v.Code == "meta" && v.Bool("conversionApiEnabled")) || (v.Code == "tiktok" && v.Bool("eventsApiEnabled"))
                || (v.Code == "pinterest" && v.Bool("conversionApiEnabled")) || (v.Code == "ga4" && v.Bool("sendServerSide")))
                modlar.Add("server");
            return new
            {
                code = v.Code, serviceType = v.ServiceType, integrationId = v.FirmPlatformIntegrationId,
                platformaOzel = v.PlatformaOzel, ownership = v.Ownership, modes = modlar,
                settings = v.Settings.Where(kv => kv.Key != "ownership").ToDictionary(kv => kv.Key, kv => kv.Value),
                lastSuccessAt = log?.LastSuccessAt, lastFailureAt = log?.LastFailureAt,
                ok24 = log?.Ok24 ?? 0, fail24 = log?.Fail24 ?? 0,
                lastError = sonHatalar.FirstOrDefault(h => h.Id == v.FirmPlatformIntegrationId)?.Error
            };
        }).ToList();

        var ob = integrationDb.TrackingEventOutbox.AsNoTracking().Where(o => o.FirmPlatformId == firmPlatformId);
        var outbox = new
        {
            pending = await ob.CountAsync(o => o.Status == "pending", ct),
            error = await ob.CountAsync(o => o.Status == "error", ct),
            done24 = await ob.CountAsync(o => o.Status == "done" && o.CreatedAt >= since, ct),
            skipped24 = await ob.CountAsync(o => o.Status == "skipped" && o.CreatedAt >= since, ct),
            lastEventAt = await ob.MaxAsync(o => (DateTime?)o.CreatedAt, ct)
        };

        return Ok(new
        {
            success = true,
            data = new
            {
                enabled = config.GetValue("Tracking:Enabled", false),
                dryRun = config.GetValue("Tracking:DryRun", false),
                consentBanner = s.ConsentBanner, consentDefault = s.ConsentDefault, purchaseAt = s.PurchaseAt,
                services = servisler, outbox
            }
        });
    }

    /// <summary>İE-5: feed durumu — google_merchant varsa URL'ler (feedKey ile), son üretim, sayılar, hata.</summary>
    [HttpGet("feed-status")]
    public async Task<IActionResult> FeedStatus([FromQuery] Guid firmPlatformId,
        [FromServices] ECSPros.Api.Services.Tracking.Feed.IFeedStatusStore feedStatus,
        [FromServices] ECSPros.Core.Application.Services.ICoreDbContext coreDb,
        [FromServices] IHostEnvironment env, CancellationToken ct)
    {
        if (firmPlatformId == Guid.Empty) return BadRequest(new { success = false, error = "firmPlatformId gerekli." });
        var s = await settings.GetAsync(firmPlatformId, ct);
        var merchant = s.Servis("google_merchant");
        var platform = await coreDb.FirmPlatforms.AsNoTracking().Where(p => p.Id == firmPlatformId)
            .Select(p => new { p.Code, p.Settings }).FirstOrDefaultAsync(ct);
        var kok = platform?.Settings.TryGetValue("canonicalDomain", out var cd) == true && cd?.ToString() is { Length: > 0 } cds ? cds.TrimEnd('/') : "";
        var key = merchant?.Get("feedKey");
        var st = await feedStatus.GetAsync(firmPlatformId, ct); // FAZ 10 / A6: durum DB'den
        return Ok(new
        {
            success = true,
            data = new
            {
                enabled = merchant is not null,
                intervalHours = config.GetValue("Feeds:IntervalHours", 6),
                feedsEnabled = config.GetValue("Feeds:Enabled", true),
                xmlUrl = merchant is null || platform is null || key is null ? null : $"{kok}/feeds/{platform.Code}/google-shopping.xml?key={key}",
                csvUrl = merchant is null || platform is null || key is null ? null : $"{kok}/feeds/{platform.Code}/meta-catalog.csv?key={key}",
                keyPending = merchant is not null && key is null,
                status = st
            }
        });
    }

    /// <summary>İE-5: feed'i şimdi üret (worker kuyruğu — saniyeler/dakikalar içinde biter, feed-status ile izlenir).</summary>
    [HttpPost("feed/generate")]
    public async Task<IActionResult> FeedGenerate([FromBody] TrackingTestEventRequest req,
        [FromServices] ECSPros.Api.Services.Tracking.Feed.IFeedTrigger trigger, CancellationToken ct)
    {
        if (req.FirmPlatformId == Guid.Empty) return BadRequest(new { success = false, error = "firmPlatformId gerekli." });
        settings.Invalidate(req.FirmPlatformId);
        var s = await settings.GetAsync(req.FirmPlatformId, ct);
        if (s.Servis("google_merchant") is null)
            return BadRequest(new { success = false, error = "Bu kanalda aktif Google Merchant entegrasyonu yok." });
        if (!config.GetValue("Feeds:Enabled", true))
            return BadRequest(new { success = false, error = "Feeds:Enabled=false — bu ortamda feed üretimi kapalı." });
        await trigger.TriggerAsync(req.FirmPlatformId, ct); // FAZ 10 / A6: tetik DB kuyruğuna
        return Ok(new { success = true });
    }

    /// <summary>İE-6: son 30 günün consent tercih dağılımı (banner ispat günlüğünden).</summary>
    [HttpGet("consent-stats")]
    public async Task<IActionResult> ConsentStats([FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        if (firmPlatformId == Guid.Empty) return BadRequest(new { success = false, error = "firmPlatformId gerekli." });
        var since = DateTime.UtcNow.AddDays(-30);
        var q = integrationDb.TrackingConsentLogs.AsNoTracking().Where(c => c.FirmPlatformId == firmPlatformId && c.CreatedAt >= since);
        var total = await q.CountAsync(ct);
        var tam = await q.CountAsync(c => c.Analytics && c.Ads && c.Personalization, ct);
        var red = await q.CountAsync(c => !c.Analytics && !c.Ads && !c.Personalization, ct);
        var uyeli = await q.CountAsync(c => c.MemberId != null, ct);
        var analytics = await q.CountAsync(c => c.Analytics, ct);
        var ads = await q.CountAsync(c => c.Ads, ct);
        var son = await q.OrderByDescending(c => c.CreatedAt).Select(c => (DateTime?)c.CreatedAt).FirstOrDefaultAsync(ct);
        return Ok(new { success = true, data = new { days = 30, total, fullAccept = tam, fullReject = red, partial = total - tam - red, withMember = uyeli, analytics, ads, lastAt = son } });
    }

    [HttpGet("outbox")]
    public async Task<IActionResult> Outbox([FromQuery] Guid firmPlatformId, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        if (firmPlatformId == Guid.Empty) return BadRequest(new { success = false, error = "firmPlatformId gerekli." });
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var q = integrationDb.TrackingEventOutbox.AsNoTracking().Where(o => o.FirmPlatformId == firmPlatformId);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(o => o.Status == status);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(o => o.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new
            {
                id = o.Id, eventName = o.EventName, dedupId = o.DedupId, source = o.Source, status = o.Status,
                attemptCount = o.AttemptCount, nextAttemptAt = o.NextAttemptAt, lastError = o.LastError,
                targetsJson = o.TargetsJson, createdAt = o.CreatedAt, processedAt = o.ProcessedAt, occurredAt = o.OccurredAt
            }).ToListAsync(ct);
        return Ok(new { success = true, data = new { items, totalCount = total, page, pageSize } });
    }

    [HttpPost("outbox/{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        var satir = await integrationDb.TrackingEventOutbox.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (satir is null) return NotFound(new { success = false, error = "Kayıt bulunamadı." });
        satir.Status = "pending"; satir.NextAttemptAt = null; satir.AttemptCount = 0; satir.LastError = null; satir.ProcessedAt = null;
        await integrationDb.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    /// <summary>Test event — order_completed, consent GRANT, örnek kalem, Extra.test=true. Meta/TikTok adapter'ı
    /// yalnız testEventCode doluysa gönderir; GA4 debug ucuna gider. Tracking kapalıysa 400.</summary>
    [HttpPost("test-event")]
    public async Task<IActionResult> TestEvent([FromBody] TrackingTestEventRequest req, CancellationToken ct)
    {
        if (req.FirmPlatformId == Guid.Empty) return BadRequest(new { success = false, error = "firmPlatformId gerekli." });
        if (!config.GetValue("Tracking:Enabled", false))
            return BadRequest(new { success = false, error = "Tracking:Enabled=false — bu ortamda takip kapalı." });

        var dedup = "test:" + Guid.NewGuid().ToString("N");
        var client = TrackingHttpContextReader.ReadClient(HttpContext, "test@example.com", "05001234567", null);
        var ev = new CommerceEvent(
            CommerceEventNames.OrderCompleted, DateTime.UtcNow, req.FirmPlatformId, dedup, "server", null,
            "TRY", 1299.90m, "TEST-" + DateTime.UtcNow.ToString("HHmmss"),
            new[] { new CommerceItem("TEST-SKU-1", "TEST-P-1", "Test Ürün", null, "Test", "Siyah, M", 1299.90m, 1, 0m) },
            client, ConsentState.Grant,
            new Dictionary<string, string> { ["test"] = "true", ["order_number"] = "TEST" });
        await publisher.PublishAsync(ev, ct);

        var satir = await integrationDb.TrackingEventOutbox.AsNoTracking()
            .Where(o => o.DedupId == dedup).Select(o => new { o.Id }).FirstOrDefaultAsync(ct);
        return Ok(new { success = true, data = new { outboxId = satir?.Id, dedupId = dedup } });
    }
}

public record TrackingTestEventRequest(Guid FirmPlatformId);
