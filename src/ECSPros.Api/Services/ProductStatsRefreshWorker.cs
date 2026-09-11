using ECSPros.Catalog.Infrastructure.Migrations;
using Npgsql;

namespace ECSPros.Api.Services;

/// <summary>
/// 2026-09-11: admin ürün listesi kapsamlı filtre — catalog.mv_product_stats (görsel var/kısmi/yok + stok toplamları)
/// açılıştan 1 dk sonra ve her 5 dk'da bir CONCURRENTLY yenilenir (liste erişilebilir kalır, ~2-3 sn).
/// Çoklu örnek (staging aynı DB): advisory lock ile tek yenileyici; alamayan tur atlar.
/// Panelden "Şimdi yenile" de aynı komutu çalıştırır (<see cref="ProductStatsRefresher"/>).
/// </summary>
public sealed class ProductStatsRefreshWorker(
    ProductStatsRefresher refresher,
    ILogger<ProductStatsRefreshWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await refresher.RefreshAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Ürün istatistik (mv_product_stats) yenileme turu başarısız."); }
            try { await Task.Delay(Tick, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}

/// <summary>REFRESH MATERIALIZED VIEW CONCURRENTLY — worker ve panel "Şimdi yenile" ortak yolu; kilit alınamazsa false.</summary>
public sealed class ProductStatsRefresher(NpgsqlDataSource dataSource, DistributedWorkerLock workerLock, ILogger<ProductStatsRefresher> logger)
{
    public async Task<bool> RefreshAsync(CancellationToken ct)
    {
        await using var handle = await workerLock.TryAcquireAsync("product-stats-refresh", ct);
        if (handle is null) { logger.LogDebug("mv_product_stats: başka örnek yeniliyor, tur atlandı."); return false; }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(ProductStatsSql.Refresh, conn) { CommandTimeout = 180 };
        await cmd.ExecuteNonQueryAsync(ct);
        logger.LogInformation("mv_product_stats yenilendi ({Ms} ms).", sw.ElapsedMilliseconds);
        return true;
    }

    public async Task<DateTime?> LastRefreshAsync(CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT MAX(\"RefreshedAt\") FROM catalog.mv_product_stats", conn);
        var v = await cmd.ExecuteScalarAsync(ct);
        return v is DateTime d ? d : null;
    }
}
