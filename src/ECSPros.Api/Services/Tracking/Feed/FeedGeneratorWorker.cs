using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using ECSPros.Core.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Tracking.Feed;

/// <summary>Kanal başına son üretim durumu (panel kartı) — status.json'a da yazılır (restart sonrası korunur).</summary>
public sealed record FeedStatus(Guid FirmPlatformId, string PlatformCode, DateTime? LastRunAt, int DurationMs,
    int ProductCount, int ItemCount, int InStockCount, long XmlBytes, long CsvBytes, string? Error, bool Running);

public interface IFeedStatusStore
{
    FeedStatus? Get(Guid platformId);
    void Set(FeedStatus st);
}

public sealed class FeedStatusStore : IFeedStatusStore
{
    private readonly ConcurrentDictionary<Guid, FeedStatus> _d = new();
    public FeedStatus? Get(Guid platformId) => _d.TryGetValue(platformId, out var s) ? s : null;
    public void Set(FeedStatus st) => _d[st.FirmPlatformId] = st;
}

/// <summary>Panelden "Şimdi üret" tetiği — worker kuyruğu (kanal id) hemen işler.</summary>
public interface IFeedTrigger { void Trigger(Guid platformId); }

public sealed class FeedTrigger : IFeedTrigger
{
    public Channel<Guid> Kuyruk { get; } = Channel.CreateUnbounded<Guid>();
    public void Trigger(Guid platformId) => Kuyruk.Writer.TryWrite(platformId);
}

public static class FeedPaths
{
    public static string OutputRoot(IConfiguration config, IHostEnvironment env)
        => config["Feeds:OutputPath"] is { Length: > 0 } p ? p : Path.Combine(env.ContentRootPath, "App_Data", "feeds");
    public static string PlatformDir(IConfiguration config, IHostEnvironment env, string platformCode)
        => Path.Combine(OutputRoot(config, env), platformCode);
}

/// <summary>
/// İE-5 Faz E (2026-08-22): feed üretim worker'ı — aktif `google_merchant` entegrasyonu olan her kanal için
/// Feeds:IntervalHours (6) saatte bir (ilk üretim açılıştan 2 dk sonra) XML+CSV üretir; panel tetiğiyle anında.
/// `feedKey` ayarı boşsa üretir ve kanal entegrasyon Settings'ine yazar (feed URL'si bu anahtarla korunur).
/// Tracking:Enabled'dan BAĞIMSIZ (feed ayrı işlev); Feeds:Enabled=false ile kapatılır.
/// </summary>
public sealed class FeedGeneratorWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    IHostEnvironment env,
    IFeedStatusStore statusStore,
    FeedTrigger trigger,
    ILogger<FeedGeneratorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken st)
    {
        var enabled = config.GetValue("Feeds:Enabled", true);
        var interval = TimeSpan.FromHours(Math.Max(1, config.GetValue("Feeds:IntervalHours", 6)));
        logger.LogInformation("Feed üretimi: {Durum} (aralık {Saat} sa, çıktı {Dir})", enabled ? "AKTİF ✓" : "KAPALI (Feeds:Enabled=false)", interval.TotalHours, FeedPaths.OutputRoot(config, env));
        if (!enabled) return;
        DurumlariYukle();

        var sonrakiZamanli = DateTime.UtcNow.AddSeconds(Math.Max(5, config.GetValue("Feeds:FirstRunDelaySeconds", 120))); // açılış yükünden sonra
        while (!st.IsCancellationRequested)
        {
            try
            {
                var bekleme = sonrakiZamanli - DateTime.UtcNow;
                if (bekleme < TimeSpan.Zero) bekleme = TimeSpan.Zero;
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(st);
                cts.CancelAfter(bekleme);
                Guid? tetik = null;
                try { tetik = await trigger.Kuyruk.Reader.ReadAsync(cts.Token); }
                catch (OperationCanceledException) when (!st.IsCancellationRequested) { /* zamanlı tur */ }

                if (tetik is { } pid) await KanalUretAsync(pid, st);
                else
                {
                    await TumKanallariUretAsync(st);
                    sonrakiZamanli = DateTime.UtcNow.Add(interval);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "FeedGeneratorWorker döngü hatası");
                await Task.Delay(TimeSpan.FromMinutes(1), st);
            }
        }
    }

    private async Task TumKanallariUretAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var coreDb = scope.ServiceProvider.GetRequiredService<ICoreDbContext>();
        var kanallar = await coreDb.FirmPlatformIntegrations.AsNoTracking()
            .Where(fi => fi.IsActive && fi.FirmPlatformId != null && fi.IntegrationService.Code == "google_merchant")
            .Select(fi => fi.FirmPlatformId!.Value).Distinct().ToListAsync(ct);
        foreach (var pid in kanallar) await KanalUretAsync(pid, ct);
    }

    public async Task KanalUretAsync(Guid platformId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var coreDb = scope.ServiceProvider.GetRequiredService<ICoreDbContext>();
        var platform = await coreDb.FirmPlatforms.AsNoTracking().Where(p => p.Id == platformId).Select(p => new { p.Code }).FirstOrDefaultAsync(ct);
        if (platform is null) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var onceki = statusStore.Get(platformId);
        statusStore.Set(new FeedStatus(platformId, platform.Code, onceki?.LastRunAt, onceki?.DurationMs ?? 0, onceki?.ProductCount ?? 0, onceki?.ItemCount ?? 0, onceki?.InStockCount ?? 0, onceki?.XmlBytes ?? 0, onceki?.CsvBytes ?? 0, null, true));
        try
        {
            await FeedKeyGarantiAsync(coreDb, platformId, ct);
            var generator = scope.ServiceProvider.GetRequiredService<FeedGenerator>();
            var dir = FeedPaths.PlatformDir(config, env, platform.Code);
            var r = await generator.GenerateAsync(platformId, platform.Code, dir, ct);
            sw.Stop();
            var st = new FeedStatus(platformId, platform.Code, DateTime.UtcNow, (int)sw.ElapsedMilliseconds, r.ProductCount, r.ItemCount, r.InStockCount, r.XmlBytes, r.CsvBytes, null, false);
            statusStore.Set(st); DurumYaz(dir, st);
            logger.LogInformation("Feed üretildi: {Kanal} — {Urun} ürün / {Kalem} kalem ({Stokta} stokta), {Ms} ms, xml {Kb} KB", platform.Code, r.ProductCount, r.ItemCount, r.InStockCount, sw.ElapsedMilliseconds, r.XmlBytes / 1024);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            var st = new FeedStatus(platformId, platform.Code, onceki?.LastRunAt, (int)sw.ElapsedMilliseconds, onceki?.ProductCount ?? 0, onceki?.ItemCount ?? 0, onceki?.InStockCount ?? 0, onceki?.XmlBytes ?? 0, onceki?.CsvBytes ?? 0, ex.Message.Length > 500 ? ex.Message[..500] : ex.Message, false);
            statusStore.Set(st);
            logger.LogError(ex, "Feed üretimi başarısız ({Kanal})", platform.Code);
        }
    }

    /// <summary>google_merchant kayıtlarında feedKey yoksa üretip yazar (tek seferlik; TrackingSettingsProvider 2 dk cache).</summary>
    private static async Task FeedKeyGarantiAsync(ICoreDbContext coreDb, Guid platformId, CancellationToken ct)
    {
        var kayitlar = await coreDb.FirmPlatformIntegrations
            .Where(fi => fi.IsActive && fi.IntegrationService.Code == "google_merchant" && (fi.FirmPlatformId == platformId || fi.FirmPlatformId == null))
            .ToListAsync(ct);
        var degisti = false;
        foreach (var k in kayitlar)
        {
            if (k.Settings.TryGetValue("feedKey", out var v) && !string.IsNullOrWhiteSpace(v?.ToString())) continue;
            var yeni = new Dictionary<string, object>(k.Settings) { ["feedKey"] = Guid.NewGuid().ToString("N") };
            k.Settings = yeni; k.UpdatedAt = DateTime.UtcNow; degisti = true;
        }
        if (degisti) await coreDb.SaveChangesAsync(ct);
    }

    private void DurumYaz(string dir, FeedStatus st)
    {
        try { File.WriteAllText(Path.Combine(dir, "status.json"), JsonSerializer.Serialize(st, OutboxCommerceEventPublisher.JsonAyar)); }
        catch (Exception ex) { logger.LogDebug(ex, "status.json yazılamadı"); }
    }

    private void DurumlariYukle()
    {
        try
        {
            var root = FeedPaths.OutputRoot(config, env);
            if (!Directory.Exists(root)) return;
            foreach (var f in Directory.GetFiles(root, "status.json", SearchOption.AllDirectories))
            {
                var st = JsonSerializer.Deserialize<FeedStatus>(File.ReadAllText(f), OutboxCommerceEventPublisher.JsonAyar);
                if (st is not null) statusStore.Set(st with { Running = false });
            }
        }
        catch (Exception ex) { logger.LogDebug(ex, "status.json okunamadı"); }
    }
}
