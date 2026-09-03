using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;

namespace ECSPros.Api.Services.ErpSource;

/// <summary>Yalnız Worker/Both node'da başlayan kalıcı ERP kaynak zamanlayıcısı.</summary>
public sealed class ErpSourceSyncWorker(
    ErpSourceSyncService sync,
    ErpSourceOptions options,
    DistributedWorkerLock workerLock,
    IServiceScopeFactory scopeFactory,
    ILogger<ErpSourceSyncWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(options.StartupDelaySeconds), stoppingToken); }
        catch (OperationCanceledException) { return; }

        logger.LogInformation("ERP Source worker başladı (Enabled={Enabled}, DryRun={DryRun}, Catalog={Catalog}, Price={Price}).",
            options.Enabled, options.DryRun, options.CatalogEnabled, options.PriceEnabled);

        DateTime lastCatalog = DateTime.MinValue, lastPrice = DateTime.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (options.Enabled && sync.IsConfigured)
                {
                    var now = DateTime.UtcNow;
                    var catalogDue = options.CatalogEnabled &&
                        now - lastCatalog >= TimeSpan.FromMinutes(Math.Max(3, options.CatalogMinutes));
                    var priceDue = options.PriceEnabled &&
                        now - lastPrice >= TimeSpan.FromMinutes(Math.Max(3, options.PriceMinutes));

                    if (catalogDue || priceDue)
                    {
                        await RunPipelineWithLeaseAsync(catalogDue, priceDue, stoppingToken);
                        if (options.CatalogEnabled) lastCatalog = now;
                        if (priceDue) lastPrice = now;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "ERP Source worker döngü hatası"); }

            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunPipelineWithLeaseAsync(bool catalogDue, bool priceDue, CancellationToken ct)
    {
        await using var lease = await workerLock.TryAcquireAsync("erp-source-catalog-price-pipeline", ct);
        if (lease is null) return;

        ErpSourceSyncReport? catalogReport = null;
        if (options.CatalogEnabled && (catalogDue || priceDue))
        {
            catalogReport = await sync.SyncCatalogAsync(ct);
            await LogReportAsync(catalogReport, ct);
        }

        if (!priceDue) return;
        if (options.CatalogEnabled && !IsOperationalSuccess(catalogReport))
        {
            logger.LogWarning(
                "ERP fiyat senkronu çalıştırılmadı: katalog/ürün/varyant bağımlılık fazı başarılı değil. Fiyat checkpoint'i korunarak sonraki çevrimde yeniden denenecek.");
            return;
        }

        var priceReport = await sync.SyncPricesAsync(ct);
        await LogReportAsync(priceReport, ct);
    }

    internal static bool IsOperationalSuccess(ErpSourceSyncReport? report)
        => report is not null && report.Success && report.Error is null;

    private async Task LogReportAsync(ErpSourceSyncReport report, CancellationToken ct)
    {
        var operationalSuccess = report.Success && report.Error is null;
        if (operationalSuccess)
            logger.LogInformation("ERP kaynak [{Slice}] {Status}: değişiklik={Changed}, süre={Duration}ms\n{Detail}",
                report.Slice, report.DryRun ? "DRY-RUN" : "OK",
                report.Changed, report.DurationMs, report.Detail.TrimEnd());
        else
            logger.LogError("ERP kaynak [{Slice}] HATA: değişiklik={Changed}, süre={Duration}ms, hata={Error}\n{Detail}",
                report.Slice, report.Changed, report.DurationMs, report.Error, report.Detail.TrimEnd());
        if (operationalSuccess && report.Changed == 0) return;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IIntegrationDbContext>();
            db.IntegrationLogs.Add(new IntegrationLog
            {
                FirmIntegrationId = Guid.Empty,
                ServiceType = "erp-source",
                OperationType = $"sync_{report.Slice}",
                Status = operationalSuccess ? (report.DryRun ? "dry_run" : "success") : "failure",
                RequestPayload = report.Detail.Length <= 8000 ? report.Detail : report.Detail[..8000],
                ErrorMessage = report.Error is { Length: > 1000 } e ? e[..1000] : report.Error,
                DurationMs = report.DurationMs,
                ReferenceType = "ErpSourceSync"
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) { logger.LogWarning(ex, "ERP kaynak senkron logu yazılamadı ({Slice})", report.Slice); }
    }
}
