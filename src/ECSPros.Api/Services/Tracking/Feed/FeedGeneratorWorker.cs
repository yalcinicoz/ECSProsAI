using System.Text.Json;
using ECSPros.Core.Application.Services;
using ECSPros.Integration.Application.Services;
using ECSPros.Api.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECSPros.Api.Services.Tracking.Feed;

/// <summary>Kanal başına son üretim durumu (panel kartı DTO'su) — integration.feed_status satırından.</summary>
public sealed record FeedStatus(Guid FirmPlatformId, string PlatformCode, DateTime? LastRunAt, int DurationMs,
    int ProductCount, int ItemCount, int InStockCount, long XmlBytes, long CsvBytes, string? Error, bool Running);

/// <summary>
/// FAZ 10 / A6: feed durumu artık DB'de (integration.feed_status) — üretimi hangi düğüm
/// yaptıysa yapsın panel her düğümden aynı durumu okur (eski süreç-içi sözlük + status.json
/// dosyası kaldırıldı).
/// </summary>
public interface IFeedStatusStore
{
    Task<FeedStatus?> GetAsync(Guid platformId, CancellationToken ct = default);
    Task SetAsync(FeedStatus st, CancellationToken ct = default);
}

public sealed class DbFeedStatusStore(IIntegrationDbContext db, NodeOptions node) : IFeedStatusStore
{
    public async Task<FeedStatus?> GetAsync(Guid platformId, CancellationToken ct = default)
    {
        var r = await db.FeedStatuses.AsNoTracking()
            .FirstOrDefaultAsync(x => x.FirmPlatformId == platformId, ct);
        return r is null ? null
            : new FeedStatus(r.FirmPlatformId, r.PlatformCode, r.LastRunAt, r.DurationMs, r.ProductCount,
                r.ItemCount, r.InStockCount, r.XmlBytes, r.CsvBytes, r.Error, r.Running);
    }

    public async Task SetAsync(FeedStatus st, CancellationToken ct = default)
    {
        var r = await db.FeedStatuses.FirstOrDefaultAsync(x => x.FirmPlatformId == st.FirmPlatformId, ct);
        if (r is null)
        {
            r = new ECSPros.Integration.Domain.Entities.FeedRunStatus
            { FirmPlatformId = st.FirmPlatformId, CreatedAt = DateTime.UtcNow };
            db.FeedStatuses.Add(r);
        }
        r.PlatformCode = st.PlatformCode;
        r.LastRunAt = st.LastRunAt;
        r.DurationMs = st.DurationMs;
        r.ProductCount = st.ProductCount;
        r.ItemCount = st.ItemCount;
        r.InStockCount = st.InStockCount;
        r.XmlBytes = st.XmlBytes;
        r.CsvBytes = st.CsvBytes;
        r.Error = st.Error;
        r.Running = st.Running;
        r.NodeId = node.Id;
        r.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// FAZ 10 / A6: panelden "Şimdi üret" tetiği — süreç-içi Channel yerine integration.feed_jobs
/// satırı. Tetik hangi düğümden gelirse gelsin, işi Worker/Both rollü düğümdeki
/// FeedGeneratorWorker sahiplenir; worker kapalıyken tetik DB'de bekler, KAYBOLMAZ.
/// </summary>
public interface IFeedTrigger { Task TriggerAsync(Guid platformId, CancellationToken ct = default); }

public sealed class DbFeedTrigger(NpgsqlDataSource dataSource) : IFeedTrigger
{
    public async Task TriggerAsync(Guid platformId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO integration.feed_jobs
                ("Id", "FirmPlatformId", "RequestedAt", "Status", "AttemptCount", "CreatedAt", "IsDeleted")
            VALUES
                (@id, @platformId, @now, 'pending', 0, @now, false)
            ON CONFLICT ("FirmPlatformId")
                WHERE "Status" IN ('pending', 'processing') AND "IsDeleted" = false
            DO NOTHING
            """, conn);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("platformId", platformId);
        cmd.Parameters.AddWithValue("now", now);
        await cmd.ExecuteNonQueryAsync(ct);
    }
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
/// FAZ 10 / A6: tetik kuyruğu DB'de (integration.feed_jobs, FOR UPDATE SKIP LOCKED ile sahiplenilir,
/// Feeds:PollSeconds=10 sn'de bir bakılır); durum DB'de (integration.feed_status). Bu worker yalnız
/// Worker/Both rollü düğümde çalışır (A2 rol kapısı).
/// </summary>
public sealed class FeedGeneratorWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    IHostEnvironment env,
    NpgsqlDataSource dataSource,
    NodeOptions node,
    ILogger<FeedGeneratorWorker> logger) : BackgroundService
{
    private sealed record FeedJobLease(Guid JobId, Guid FirmPlatformId, int AttemptCount);
    private sealed record FeedExecutionResult(bool Success, string? Error = null);

    protected override async Task ExecuteAsync(CancellationToken st)
    {
        var enabled = config.GetValue("Feeds:Enabled", true);
        var interval = TimeSpan.FromHours(Math.Max(1, config.GetValue("Feeds:IntervalHours", 6)));
        var poll = TimeSpan.FromSeconds(Math.Max(2, config.GetValue("Feeds:PollSeconds", 10)));
        var lease = TimeSpan.FromSeconds(Math.Max(30, config.GetValue("Feeds:LeaseSeconds", 900)));
        var maxAttempts = Math.Max(1, config.GetValue("Feeds:MaxAttempts", 5));
        var retryDelay = TimeSpan.FromSeconds(Math.Max(1, config.GetValue("Feeds:RetryDelaySeconds", 60)));
        logger.LogInformation("Feed üretimi: {Durum} (aralık {Saat} sa, kuyruk kontrolü {Sn} sn, çıktı {Dir})",
            enabled ? "AKTİF ✓" : "KAPALI (Feeds:Enabled=false)", interval.TotalHours, poll.TotalSeconds, FeedPaths.OutputRoot(config, env));
        if (!enabled) return;
        await EskiStatusJsonlariAktarAsync(st); // A6 geçişi: bir kerelik dosya→DB aktarımı

        var sonrakiZamanli = DateTime.UtcNow.AddSeconds(Math.Max(5, config.GetValue("Feeds:FirstRunDelaySeconds", 120))); // açılış yükünden sonra
        while (!st.IsCancellationRequested)
        {
            try
            {
                // Panelden tetiklenmiş iş var mı? Kalıcı DB kuyruğundan atomik lease al.
                var tetik = await IsSahiplenAsync(lease, maxAttempts, st);
                if (tetik is not null)
                {
                    await SahiplenilenIsiCalistirAsync(tetik, lease, maxAttempts, retryDelay, st);
                    continue; // kuyrukta başka iş olabilir — beklemeden tekrar bak
                }

                if (DateTime.UtcNow >= sonrakiZamanli)
                {
                    // 10 dk'da bir tarama: hiç üretilmemiş (yeni eklenen Merchant kaydı) ya da aralığı dolmuş kanallar
                    await TumKanallariUretAsync(interval, st);
                    sonrakiZamanli = DateTime.UtcNow.AddMinutes(10);
                }

                await Task.Delay(poll, st);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception e)
            {
                logger.LogError(e, "FeedGeneratorWorker döngü hatası");
                await Task.Delay(TimeSpan.FromMinutes(1), st);
            }
        }
    }

    /// <summary>En eski hazır işi atomik sahiplenir. Süresi dolmuş processing satırları crash recovery
    /// için yeniden alınabilir; satır iş başlamadan hiçbir zaman silinmez.</summary>
    private async Task<FeedJobLease?> IsSahiplenAsync(TimeSpan lease, int maxAttempts, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            WITH exhausted AS (
                UPDATE integration.feed_jobs
                SET "Status" = 'failed',
                    "CompletedAt" = NOW(),
                    "LeaseOwner" = NULL,
                    "LeaseUntil" = NULL,
                    "LastError" = COALESCE("LastError", 'Worker kaybı sonrası maksimum deneme sayısına ulaşıldı.'),
                    "UpdatedAt" = NOW()
                WHERE "IsDeleted" = false
                  AND "Status" = 'processing'
                  AND "LeaseUntil" <= NOW()
                  AND "AttemptCount" >= @maxAttempts
                RETURNING "Id"
            ), candidate AS (
                SELECT "Id"
                FROM integration.feed_jobs
                WHERE "IsDeleted" = false
                  AND "AttemptCount" < @maxAttempts
                  AND "RequestedAt" <= NOW()
                  AND (
                      "Status" = 'pending'
                      OR ("Status" = 'processing' AND "LeaseUntil" <= NOW())
                  )
                ORDER BY "RequestedAt", "CreatedAt"
                LIMIT 1
                FOR UPDATE SKIP LOCKED
            )
            UPDATE integration.feed_jobs AS jobs
            SET "Status" = 'processing',
                "LeaseOwner" = @owner,
                "LeaseUntil" = NOW() + @lease,
                "AttemptCount" = jobs."AttemptCount" + 1,
                "StartedAt" = COALESCE(jobs."StartedAt", NOW()),
                "LastError" = NULL,
                "UpdatedAt" = NOW()
            FROM candidate
            WHERE jobs."Id" = candidate."Id"
            RETURNING jobs."Id", jobs."FirmPlatformId", jobs."AttemptCount"
            """, conn);
        cmd.Parameters.AddWithValue("maxAttempts", maxAttempts);
        cmd.Parameters.AddWithValue("owner", node.Id);
        cmd.Parameters.AddWithValue("lease", lease);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new FeedJobLease(reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2));
    }

    private async Task SahiplenilenIsiCalistirAsync(
        FeedJobLease job,
        TimeSpan lease,
        int maxAttempts,
        TimeSpan retryDelay,
        CancellationToken stoppingToken)
    {
        using var executionCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var heartbeat = LeaseYenileAsync(job.JobId, lease, executionCts, stoppingToken);

        try
        {
            var result = await KanalUretAsync(job.FirmPlatformId, executionCts.Token);
            using var finalizationCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            if (result.Success)
                await TamamlaAsync(job.JobId, finalizationCts.Token);
            else
                await BasarisizAsync(job, result.Error ?? "Feed üretimi başarısız.", maxAttempts, retryDelay, finalizationCts.Token);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown/crash sırasında satır processing kalır; lease dolunca başka worker geri alır.
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Feed işi lease sahipliği kaybedildiği için durduruldu: {JobId}", job.JobId);
        }
        finally
        {
            executionCts.Cancel();
            try { await heartbeat; }
            catch (OperationCanceledException) { }
        }
    }

    private async Task LeaseYenileAsync(
        Guid jobId,
        TimeSpan lease,
        CancellationTokenSource executionCts,
        CancellationToken stoppingToken)
    {
        var heartbeatInterval = TimeSpan.FromSeconds(Math.Max(10, lease.TotalSeconds / 3));
        using var timer = new PeriodicTimer(heartbeatInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(executionCts.Token))
            {
                await using var conn = await dataSource.OpenConnectionAsync(executionCts.Token);
                await using var cmd = new NpgsqlCommand("""
                    UPDATE integration.feed_jobs
                    SET "LeaseUntil" = NOW() + @lease, "UpdatedAt" = NOW()
                    WHERE "Id" = @id AND "Status" = 'processing' AND "LeaseOwner" = @owner
                    """, conn);
                cmd.Parameters.AddWithValue("id", jobId);
                cmd.Parameters.AddWithValue("owner", node.Id);
                cmd.Parameters.AddWithValue("lease", lease);
                if (await cmd.ExecuteNonQueryAsync(executionCts.Token) != 1)
                {
                    logger.LogError("Feed işi lease sahipliği kaybedildi: {JobId} / {NodeId}", jobId, node.Id);
                    executionCts.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (executionCts.IsCancellationRequested || stoppingToken.IsCancellationRequested)
        {
            // Normal iş bitişi veya servis kapanışı.
        }
        catch (Exception ex)
        {
            // Lease yenilenemiyorsa çift üretim riskini almamak için çalışan üretimi durdur.
            logger.LogError(ex, "Feed işi lease yenilenemedi; üretim durduruluyor: {JobId}", jobId);
            executionCts.Cancel();
        }
    }

    private async Task TamamlaAsync(Guid jobId, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            UPDATE integration.feed_jobs
            SET "Status" = 'completed', "CompletedAt" = NOW(),
                "LeaseOwner" = NULL, "LeaseUntil" = NULL, "LastError" = NULL, "UpdatedAt" = NOW()
            WHERE "Id" = @id AND "Status" = 'processing' AND "LeaseOwner" = @owner
            """, conn);
        cmd.Parameters.AddWithValue("id", jobId);
        cmd.Parameters.AddWithValue("owner", node.Id);
        if (await cmd.ExecuteNonQueryAsync(ct) != 1)
            throw new InvalidOperationException($"Feed işi tamamlanırken lease sahipliği bulunamadı: {jobId}");
    }

    private async Task BasarisizAsync(
        FeedJobLease job,
        string error,
        int maxAttempts,
        TimeSpan retryDelay,
        CancellationToken ct)
    {
        var sonDeneme = job.AttemptCount >= maxAttempts;
        var guvenliHata = error.Length > 2000 ? error[..2000] : error;
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            UPDATE integration.feed_jobs
            SET "Status" = @status,
                "RequestedAt" = CASE WHEN @failed THEN "RequestedAt" ELSE NOW() + @retryDelay END,
                "CompletedAt" = CASE WHEN @failed THEN NOW() ELSE NULL END,
                "LeaseOwner" = NULL,
                "LeaseUntil" = NULL,
                "LastError" = @error,
                "UpdatedAt" = NOW()
            WHERE "Id" = @id AND "Status" = 'processing' AND "LeaseOwner" = @owner
            """, conn);
        cmd.Parameters.AddWithValue("status", sonDeneme ? "failed" : "pending");
        cmd.Parameters.AddWithValue("failed", sonDeneme);
        cmd.Parameters.AddWithValue("retryDelay", retryDelay);
        cmd.Parameters.AddWithValue("error", guvenliHata);
        cmd.Parameters.AddWithValue("id", job.JobId);
        cmd.Parameters.AddWithValue("owner", node.Id);
        if (await cmd.ExecuteNonQueryAsync(ct) != 1)
            throw new InvalidOperationException($"Feed işi hata durumu yazılırken lease sahipliği bulunamadı: {job.JobId}");

        logger.LogWarning("Feed işi {Durum}: {JobId}, deneme {Deneme}/{Maksimum}",
            sonDeneme ? "kalıcı olarak başarısız" : "yeniden kuyruğa alındı",
            job.JobId, job.AttemptCount, maxAttempts);
    }

    private async Task TumKanallariUretAsync(TimeSpan interval, CancellationToken ct)
    {
        List<Guid> kanallar;
        using (var scope = scopeFactory.CreateScope())
        {
            var coreDb = scope.ServiceProvider.GetRequiredService<ICoreDbContext>();
            kanallar = await coreDb.FirmPlatformIntegrations.AsNoTracking()
                .Where(fi => fi.IsActive && fi.FirmPlatformId != null && fi.IntegrationService.Code == "google_merchant")
                .Select(fi => fi.FirmPlatformId!.Value).Distinct().ToListAsync(ct);
        }
        foreach (var pid in kanallar)
        {
            FeedStatus? st;
            using (var scope = scopeFactory.CreateScope())
                st = await scope.ServiceProvider.GetRequiredService<IFeedStatusStore>().GetAsync(pid, ct);
            var gerekli = st is null || st.LastRunAt is null || (DateTime.UtcNow - st.LastRunAt.Value) >= interval
                          || (st.Error is not null && (DateTime.UtcNow - (st.LastRunAt ?? DateTime.MinValue)) >= TimeSpan.FromMinutes(30));
            if (gerekli) await KanalUretAsync(pid, ct);
        }
    }

    private async Task<FeedExecutionResult> KanalUretAsync(Guid platformId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var coreDb = scope.ServiceProvider.GetRequiredService<ICoreDbContext>();
        var statusStore = scope.ServiceProvider.GetRequiredService<IFeedStatusStore>();
        var platform = await coreDb.FirmPlatforms.AsNoTracking().Where(p => p.Id == platformId).Select(p => new { p.Code }).FirstOrDefaultAsync(ct);
        if (platform is null) return new FeedExecutionResult(false, $"Feed platformu bulunamadı: {platformId}");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var onceki = await statusStore.GetAsync(platformId, ct);
        await statusStore.SetAsync(new FeedStatus(platformId, platform.Code, onceki?.LastRunAt, onceki?.DurationMs ?? 0, onceki?.ProductCount ?? 0, onceki?.ItemCount ?? 0, onceki?.InStockCount ?? 0, onceki?.XmlBytes ?? 0, onceki?.CsvBytes ?? 0, null, true), ct);
        try
        {
            await FeedKeyGarantiAsync(coreDb, platformId, ct);
            var generator = scope.ServiceProvider.GetRequiredService<FeedGenerator>();
            var dir = FeedPaths.PlatformDir(config, env, platform.Code);
            var r = await generator.GenerateAsync(platformId, platform.Code, dir, ct);
            if (string.Equals(config["Storage:Provider"], "S3", StringComparison.OrdinalIgnoreCase))
            {
                var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
                await UploadFeedAsync(storage, platform.Code, "google-shopping.xml", r.XmlPath,
                    "application/xml; charset=utf-8", ct);
                await UploadFeedAsync(storage, platform.Code, "meta-catalog.csv", r.CsvPath,
                    "text/csv; charset=utf-8", ct);
            }
            sw.Stop();
            await statusStore.SetAsync(new FeedStatus(platformId, platform.Code, DateTime.UtcNow, (int)sw.ElapsedMilliseconds, r.ProductCount, r.ItemCount, r.InStockCount, r.XmlBytes, r.CsvBytes, null, false), ct);
            logger.LogInformation("Feed üretildi: {Kanal} — {Urun} ürün / {Kalem} kalem ({Stokta} stokta), {Ms} ms, xml {Kb} KB", platform.Code, r.ProductCount, r.ItemCount, r.InStockCount, sw.ElapsedMilliseconds, r.XmlBytes / 1024);
            return new FeedExecutionResult(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            await statusStore.SetAsync(new FeedStatus(platformId, platform.Code, onceki?.LastRunAt, (int)sw.ElapsedMilliseconds, onceki?.ProductCount ?? 0, onceki?.ItemCount ?? 0, onceki?.InStockCount ?? 0, onceki?.XmlBytes ?? 0, onceki?.CsvBytes ?? 0, ex.Message.Length > 500 ? ex.Message[..500] : ex.Message, false), CancellationToken.None);
            logger.LogError(ex, "Feed üretimi başarısız ({Kanal})", platform.Code);
            return new FeedExecutionResult(false, ex.Message);
        }
    }

    private static async Task UploadFeedAsync(
        IFileStorage storage, string platformCode, string fileName, string path,
        string contentType, CancellationToken ct)
    {
        await using var input = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await storage.SavePublicAsync($"feeds/{platformCode}", fileName, input, contentType, ct);
    }

    /// <summary>A6 geçiş köprüsü: eski status.json dosyalarındaki son üretim durumunu DB'de karşılığı
    /// OLMAYAN kanallara bir kez aktarır — restart sonrası tüm feed'lerin gereksiz yeniden üretimini
    /// önler. Dosyalar silinmez; DB satırı oluştuktan sonra bir daha okunmaz.</summary>
    private async Task EskiStatusJsonlariAktarAsync(CancellationToken ct)
    {
        try
        {
            var root = FeedPaths.OutputRoot(config, env);
            if (!Directory.Exists(root)) return;
            using var scope = scopeFactory.CreateScope();
            var statusStore = scope.ServiceProvider.GetRequiredService<IFeedStatusStore>();
            foreach (var f in Directory.GetFiles(root, "status.json", SearchOption.AllDirectories))
            {
                var eski = JsonSerializer.Deserialize<FeedStatus>(await File.ReadAllTextAsync(f, ct),
                    OutboxCommerceEventPublisher.JsonAyar);
                if (eski is null || await statusStore.GetAsync(eski.FirmPlatformId, ct) is not null) continue;
                await statusStore.SetAsync(eski with { Running = false }, ct);
                logger.LogInformation("Feed durumu dosyadan DB'ye aktarıldı: {Kanal}", eski.PlatformCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Eski status.json aktarımı başarısız — feed'ler ilk turda yeniden üretilir.");
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
}
