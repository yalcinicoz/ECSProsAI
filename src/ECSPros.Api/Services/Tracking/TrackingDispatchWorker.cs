using System.Diagnostics;
using System.Text.Json;
using ECSPros.Api.Services.Store;
using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using ECSPros.Shared.Contracts.Tracking;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Tracking;

/// <summary>
/// Commerce event dispatcher (İE-2 Faz B-5): tracking_event_outbox'taki pending satırları
/// 5 sn'de bir 50'lik dilimlerle okur; her satır için kanalın aktif takip entegrasyonlarını
/// (ITrackingSettingsProvider) ve kayıtlı ITrackingAdapter'ları eşler, consent kategorisini
/// uygular, gönderir, sonucu IntegrationLog'a yazar. Hedef adapter yoksa → skipped.
/// Hata: üstel geri çekilme 1/5/30/120/360 dk, 5 denemede error. Tracking:Enabled=false →
/// worker hiç çalışmaz; Tracking:DryRun=true → HTTP atmaz, log'a "dry_run" yazar.
/// 90 günden eski tracking_order_context + done/skipped outbox satırları günde bir temizlenir.
/// </summary>
public sealed class TrackingDispatchWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    DistributedWorkerLock workerLock,
    ILogger<TrackingDispatchWorker> logger) : BackgroundService
{
    private static readonly int[] BackoffMinutes = { 1, 5, 30, 120, 360 };
    private DateTime _sonTemizlik = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken st)
    {
        var enabled = config.GetValue("Tracking:Enabled", false);
        var dryRun = config.GetValue("Tracking:DryRun", false);
        logger.LogInformation("Tracking dispatch: {Durum}",
            !enabled ? "KAPALI (Tracking:Enabled=false — outbox'a yazılmaz, worker boşta)"
            : dryRun ? "DRY-RUN (outbox işlenir, dış platforma HTTP atılmaz)"
            : "AKTİF ✓");
        if (!enabled) return;

        while (!st.IsCancellationRequested)
        {
            try
            {
                await DilimIsleAsync(dryRun, st);
                if ((DateTime.UtcNow - _sonTemizlik).TotalHours >= 24)
                {
                    await TemizleAsync(st);
                    _sonTemizlik = DateTime.UtcNow;
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "TrackingDispatchWorker döngü hatası");
            }
            await Task.Delay(TimeSpan.FromSeconds(5), st);
        }
    }

    private async Task DilimIsleAsync(bool dryRun, CancellationToken ct)
    {
        await using var lease = await workerLock.TryAcquireAsync("tracking-dispatch", ct);
        if (lease is null) return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IIntegrationDbContext>();
        var ayarlar = scope.ServiceProvider.GetRequiredService<ITrackingSettingsProvider>();
        var adapters = scope.ServiceProvider.GetServices<ITrackingAdapter>().ToList();

        var now = DateTime.UtcNow;
        var bekleyenler = await db.TrackingEventOutbox
            .Where(o => o.Status == "pending" && (o.NextAttemptAt == null || o.NextAttemptAt <= now))
            .OrderBy(o => o.CreatedAt)
            .Take(50)
            .ToListAsync(ct);
        if (bekleyenler.Count == 0) return;

        foreach (var satir in bekleyenler)
        {
            CommerceEvent? e;
            try { e = JsonSerializer.Deserialize<CommerceEvent>(satir.PayloadJson, OutboxCommerceEventPublisher.JsonAyar); }
            catch (Exception ex) { Bitir(satir, "error", $"payload çözülemedi: {ex.Message}", null); continue; }
            if (e is null) { Bitir(satir, "error", "payload boş", null); continue; }

            var kanal = await ayarlar.GetAsync(satir.FirmPlatformId, ct);
            var hedefler = adapters
                .Select(a => (Adapter: a, Servis: kanal.Servis(a.Code)))
                .Where(x => x.Servis is not null && x.Adapter.Supports(e, x.Servis))
                .ToList();

            var sonuclar = new List<object>();
            if (hedefler.Count == 0)
            {
                Bitir(satir, "skipped", null, sonuclar.Count == 0 ? "[]" : null);
                satir.TargetsJson = "[]";
                continue;
            }

            var hata = false;
            foreach (var (adapter, servis) in hedefler)
            {
                var izinli = adapter.ConsentCategory switch
                {
                    "analytics" => e.Consent.Analytics,
                    "ads" => e.Consent.Ads,
                    _ => false
                };
                if (!izinli)
                {
                    sonuclar.Add(new { adapter = adapter.Code, status = "skipped", error = "consent yok" });
                    continue;
                }

                var sw = Stopwatch.StartNew();
                TrackingSendResult sonuc;
                if (dryRun)
                    sonuc = TrackingSendResult.Ok(null, "dry_run");
                else
                {
                    try { sonuc = await adapter.SendAsync(e, servis!, ct); }
                    catch (Exception ex) { sonuc = TrackingSendResult.Fail(ex.Message); }
                }
                sw.Stop();

                sonuclar.Add(new { adapter = adapter.Code, status = dryRun ? "dry_run" : (sonuc.Success ? "success" : "failure"), error = sonuc.Error, http = sonuc.HttpStatus });
                db.IntegrationLogs.Add(new IntegrationLog
                {
                    FirmIntegrationId = servis!.FirmPlatformIntegrationId,
                    ServiceType = servis.ServiceType,
                    OperationType = "send_event:" + e.Name,
                    Status = dryRun ? "dry_run" : (sonuc.Success ? "success" : "failure"),
                    HttpStatusCode = sonuc.HttpStatus,
                    ErrorMessage = sonuc.Error is { Length: > 1900 } err ? err[..1900] : sonuc.Error,
                    ResponsePayload = sonuc.ResponseSummary,
                    RequestPayload = $"{{\"event\":\"{e.Name}\",\"dedupId\":\"{e.DedupId}\",\"source\":\"{e.Source}\"}}", // token/PII YOK
                    DurationMs = (int)sw.ElapsedMilliseconds,
                    ReferenceId = e.Name is CommerceEventNames.OrderCompleted or CommerceEventNames.Refund && Guid.TryParse(e.DedupId.Split(':')[0], out var oid) ? oid : null,
                    ReferenceType = e.Name is CommerceEventNames.OrderCompleted or CommerceEventNames.Refund ? "Order" : "TrackingEvent"
                });
                if (!sonuc.Success && !dryRun) hata = true;
            }

            satir.TargetsJson = JsonSerializer.Serialize(sonuclar, OutboxCommerceEventPublisher.JsonAyar);
            var hicGonderilmedi = sonuclar.Count > 0 && sonuclar.All(x => x.GetType().GetProperty("status")?.GetValue(x)?.ToString() == "skipped");
            if (hicGonderilmedi)
                Bitir(satir, "skipped", "tüm hedefler atlandı (consent yok)", null);
            else if (!hata)
                Bitir(satir, "done", null, null);
            else
            {
                satir.AttemptCount++;
                satir.LastError = string.Join(" | ", sonuclar.OfType<object>().Select(s => s.ToString()).Take(3));
                if (satir.AttemptCount >= BackoffMinutes.Length)
                {
                    satir.Status = "error";
                    satir.ProcessedAt = DateTime.UtcNow;
                }
                else
                    satir.NextAttemptAt = DateTime.UtcNow.AddMinutes(BackoffMinutes[satir.AttemptCount - 1]);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static void Bitir(TrackingEventOutbox satir, string durum, string? hata, string? targets)
    {
        satir.Status = durum;
        satir.ProcessedAt = DateTime.UtcNow;
        if (hata is not null) satir.LastError = hata.Length > 1900 ? hata[..1900] : hata;
        if (targets is not null) satir.TargetsJson = targets;
    }

    private async Task TemizleAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IIntegrationDbContext>();
        var esik = DateTime.UtcNow.AddDays(-90);
        var n1 = await db.TrackingOrderContexts.Where(x => x.CreatedAt < esik).ExecuteDeleteAsync(ct);
        var n2 = await db.TrackingEventOutbox.Where(x => x.CreatedAt < esik && (x.Status == "done" || x.Status == "skipped")).ExecuteDeleteAsync(ct);
        var esikConsent = DateTime.UtcNow.AddDays(-365); // İE-6: consent ispat günlüğü 12 ay
        var n3 = await db.TrackingConsentLogs.Where(x => x.CreatedAt < esikConsent).ExecuteDeleteAsync(ct);
        if (n1 + n2 + n3 > 0) logger.LogInformation("Tracking temizlik: {Ctx} bağlam + {Out} outbox (90 gün) + {Cns} consent (365 gün) satırı silindi", n1, n2, n3);
    }
}
