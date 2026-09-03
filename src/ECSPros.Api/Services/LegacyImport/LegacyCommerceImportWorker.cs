namespace ECSPros.Api.Services.LegacyImport;

public sealed record LegacyImportSliceReport(
    string Slice,
    bool Success,
    bool DryRun,
    int Changed,
    int Skipped,
    string? Error = null);

/// <summary>L3-L6 dilimleri bu sözleşmeyle worker'a eklenir.</summary>
public interface ILegacyCommerceImportSlice
{
    string Slice { get; }
    Task<LegacyImportSliceReport> RunAsync(CancellationToken ct);
}

/// <summary>Yalnız Worker/Both node'da kaydedilen, advisory lock korumalı geçici import zamanlayıcısı.</summary>
public sealed class LegacyCommerceImportWorker(
    LegacyReadImportOptions options,
    DistributedWorkerLock workerLock,
    IServiceScopeFactory scopeFactory,
    ILogger<LegacyCommerceImportWorker> logger) : BackgroundService
{
    private readonly DateTime _startedAtUtc = DateTime.UtcNow;
    private readonly HashSet<string> _missingHandlersLogged = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lastSuccessfulRun = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lastAttempt = new(StringComparer.OrdinalIgnoreCase);
    private DateOnly? _lastReconciliationDate;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) return;

        try { await Task.Delay(TimeSpan.FromSeconds(options.StartupDelaySeconds), stoppingToken); }
        catch (OperationCanceledException) { return; }

        logger.LogInformation(
            "Legacy read import worker başladı (DryRun={DryRun}, PlatformId={PlatformId}, Slices={Slices}).",
            options.DryRun, options.PlatformId, string.Join(',', options.EnabledSlices()));

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var slice in options.EnabledSlices())
            {
                if (stoppingToken.IsCancellationRequested) break;
                if (!IsDue(slice, DateTime.UtcNow)) continue;
                _lastAttempt[slice] = DateTime.UtcNow;
                if (await RunSliceAsync(slice, stoppingToken))
                    _lastSuccessfulRun[slice] = DateTime.UtcNow;
            }

            await RunDailyReconciliationIfDueAsync(stoppingToken);

            try { await Task.Delay(TimeSpan.FromSeconds(options.IntervalSeconds), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunDailyReconciliationIfDueAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        if (now.Hour != options.FullReconciliationHourUtc || _lastReconciliationDate == today) return;

        using var scope = scopeFactory.CreateScope();
        var reconciliation = scope.ServiceProvider.GetRequiredService<LegacyImportReconciliationService>();
        try
        {
            var report = await reconciliation.RunAsync(ct);
            _lastReconciliationDate = today;
            foreach (var entity in report.Entities)
                logger.LogInformation(
                    "Legacy uzlaştırma [{Entity}]: kaynak={Source}, eşleşen={Matched}, eksik={Missing}",
                    entity.Entity, entity.SourceCount, entity.TargetMatchedCount, entity.MissingSourceIds.Count);
            if (!report.IsComplete)
                logger.LogWarning("Legacy tam uzlaştırma eksik kayıt buldu: toplam={Missing}", report.TotalMissing);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Legacy günlük tam uzlaştırma başarısız");
        }
    }

    private bool IsDue(string slice, DateTime now)
    {
        if (slice.Equals(LegacyImportSlices.Images, StringComparison.OrdinalIgnoreCase) &&
            !_lastAttempt.ContainsKey(slice) &&
            now - _startedAtUtc < TimeSpan.FromMinutes(options.ImagesFullStartupDelayMinutes))
            return false;
        if (!_lastSuccessfulRun.TryGetValue(slice, out var lastRun))
        {
            // Bağlantı kesikken özellikle ağır görsel uzlaştırmasını ana loop kadansında
            // tekrar tekrar başlatma; geçici hataya kontrollü backoff uygula.
            return !_lastAttempt.TryGetValue(slice, out var lastAttempt) ||
                   now - lastAttempt >= TimeSpan.FromSeconds(Math.Max(300, options.IntervalSeconds));
        }
        var interval = slice.Equals(LegacyImportSlices.Images, StringComparison.OrdinalIgnoreCase)
            ? TimeSpan.FromMinutes(options.ImagesIntervalMinutes)
            : slice.Equals(LegacyImportSlices.MissingImages, StringComparison.OrdinalIgnoreCase)
                ? TimeSpan.FromMinutes(options.MissingImagesIntervalMinutes)
                : TimeSpan.FromSeconds(options.IntervalSeconds);
        return now - lastRun >= interval;
    }

    private async Task<bool> RunSliceAsync(string slice, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetServices<ILegacyCommerceImportSlice>()
                .SingleOrDefault(x => x.Slice.Equals(slice, StringComparison.OrdinalIgnoreCase));
            if (handler is null)
            {
                if (_missingHandlersLogged.Add(slice))
                    logger.LogWarning("Legacy read import [{Slice}] etkin fakat handler henüz kayıtlı değil; kaynak bağlantısı açılmadı.", slice);
                return false;
            }

            // PostgreSQL restart/failover (57P01) lock bağlantısını kesebilir. Lock alma ve
            // handle dispose işlemleri de aynı transient hata sınırının içinde kalmalıdır.
            await using var lease = await workerLock.TryAcquireAsync(
                $"legacy-read-import-{options.PlatformId}-{slice}", ct);
            if (lease is null) return false;

            var result = await handler.RunAsync(ct);
            logger.LogInformation(
                "Legacy read import [{Slice}] {Status}: değişiklik={Changed}, atlanan={Skipped}{Error}",
                slice, result.Success ? (result.DryRun ? "DRY-RUN" : "OK") : "HATA",
                result.Changed, result.Skipped,
                result.Error is null ? string.Empty : $", hata={result.Error}");
            return result.Success;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return false; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Legacy read import [{Slice}] beklenmeyen hata", slice);
            return false;
        }
    }
}
