using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;

namespace ECSPros.Api.Services.LegacyStock;

public sealed class LegacyStockSyncWorker(
    LegacyStockSyncService sync,
    LegacyStockMappingRepairService mappingRepair,
    LegacyStockSyncOptions options,
    DistributedWorkerLock workerLock,
    IServiceScopeFactory scopeFactory,
    ILogger<LegacyStockSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(options.StartupDelaySeconds), stoppingToken); }
        catch (OperationCanceledException) { return; }

        logger.LogInformation(
            "Geçici MySQL stock-only worker başladı (Enabled={Enabled}, DryRun={DryRun}, " +
            "RepairMissingMappings={RepairMappings}, MappingRepairDryRun={RepairDryRun}, IntervalSeconds={Interval}).",
            options.Enabled, options.DryRun, options.RepairMissingMappings,
            options.MappingRepairDryRun, options.IntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (options.Enabled && sync.IsConfigured)
            {
                try
                {
                    await using var lease = await workerLock.TryAcquireAsync("legacy-stock-sync", stoppingToken);
                    if (lease is not null)
                    {
                        if (options.RepairMissingMappings)
                        {
                            var repairReport = await mappingRepair.RepairAsync(stoppingToken);
                            if (repairReport.Success)
                                logger.LogInformation(
                                    "Legacy stock eşleme onarımı {Status}: değişiklik={Changed}, süre={Duration}ms; {Detail}",
                                    repairReport.DryRun ? "DRY-RUN" : "OK", repairReport.Changed,
                                    repairReport.DurationMs, repairReport.Detail);
                            else
                            {
                                logger.LogError(
                                    "Legacy stock eşleme onarımı HATA: süre={Duration}ms, hata={Error}",
                                    repairReport.DurationMs, repairReport.Error);
                                await SaveLogAsync(repairReport, "repair_stock_mappings", stoppingToken);
                                throw new InvalidOperationException(
                                    $"Legacy stock eşleme onarımı başarısız: {repairReport.Error}");
                            }
                            await SaveLogAsync(repairReport, "repair_stock_mappings", stoppingToken);
                        }

                        var report = await sync.SyncAsync(stoppingToken);
                        if (report.Success)
                            logger.LogInformation(
                                "Legacy stock-only {Status}: değişiklik={Changed}, süre={Duration}ms; {Detail}",
                                report.DryRun ? "DRY-RUN" : "OK", report.Changed, report.DurationMs, report.Detail);
                        else
                            logger.LogError("Legacy stock-only HATA: süre={Duration}ms, hata={Error}",
                                report.DurationMs, report.Error);
                        await SaveLogAsync(report, "sync_stock_snapshot", stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { logger.LogError(ex, "Legacy stock-only worker döngü hatası"); }
            }

            try { await Task.Delay(TimeSpan.FromSeconds(options.IntervalSeconds), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task SaveLogAsync(LegacyStockSyncReport report, string operationType, CancellationToken ct)
    {
        if (report.Success && report.Changed == 0) return;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IIntegrationDbContext>();
            db.IntegrationLogs.Add(new IntegrationLog
            {
                FirmIntegrationId = Guid.Empty,
                ServiceType = "legacy-stock",
                OperationType = operationType,
                Status = report.Success ? (report.DryRun ? "dry_run" : "success") : "failure",
                RequestPayload = report.Detail.Length <= 8000 ? report.Detail : report.Detail[..8000],
                ErrorMessage = report.Error is { Length: > 1000 } error ? error[..1000] : report.Error,
                DurationMs = report.DurationMs,
                ReferenceType = "LegacyStockSync"
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Legacy stock-only integration logu yazılamadı"); }
    }
}
