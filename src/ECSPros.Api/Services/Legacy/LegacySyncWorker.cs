using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;

namespace ECSPros.Api.Services.Legacy;

/// <summary>
/// Part B / B1-B3 zamanlayıcısı (2026-08-01): LegacySyncService dilimlerini periyodik koşar.
/// - Fiyat+kanal fiyatı+stok: sık (Legacy:Sync:PriceStockMinutes, varsayılan 10 dk)
/// - Yeni ürün (B1) + görsel (B3): seyrek (Legacy:Sync:CatalogMinutes, varsayılan 360 dk)
/// Dilimler hiçbir zaman paralel koşmaz (tek döngü). Eski sistem kapalı/erişilemezken yeni
/// site etkilenmez: her dilim kendi hatasını yutar, worker yaşamaya devam eder.
///
/// Kademeler: Legacy:MySqlConnection dolu + Legacy:Sync:Enabled=true → dilimler koşar
/// (varsayılan DryRun=true: yalnız rapor). Gerçek yazım için ek olarak Legacy:Sync:DryRun=false.
/// Sonuçlar: değişiklik/hata olan her koşu integration_logs'a (ServiceType=legacy,
/// OperationType=sync_*) yazılır; her koşu ayrıca uygulama loguna tek satır düşer.
/// </summary>
public sealed class LegacySyncWorker(
    LegacySyncService sync,
    LegacyOrderSyncService orderSync,
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    DistributedWorkerLock workerLock,
    ILogger<LegacySyncWorker> logger) : BackgroundService
{
    private static readonly TimeSpan KontrolAraligi = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Açılışta API tam ayağa kalksın (migration/seed) diye kısa bekleme
        try { await Task.Delay(TimeSpan.FromSeconds(90), stoppingToken); }
        catch (OperationCanceledException) { return; }

        logger.LogInformation("LegacySyncWorker başlatıldı (Enabled={Enabled}, DryRun={DryRun}).",
            config.GetValue("Legacy:Sync:Enabled", false), config.GetValue("Legacy:Sync:DryRun", true));

        DateTime sonFiyatStok = DateTime.MinValue, sonKatalog = DateTime.MinValue;
        DateTime sonSiparis = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (config.GetValue("Legacy:Sync:Enabled", false) && sync.IsConfigured)
                {
                    await using var lease = await workerLock.TryAcquireAsync("legacy-sync", stoppingToken);
                    if (lease is null)
                    {
                        await Task.Delay(KontrolAraligi, stoppingToken);
                        continue;
                    }

                    var simdi = DateTime.UtcNow;
                    var fiyatStokAralik = TimeSpan.FromMinutes(Math.Max(3, config.GetValue("Legacy:Sync:PriceStockMinutes", 10)));
                    var katalogAralik = TimeSpan.FromMinutes(Math.Max(30, config.GetValue("Legacy:Sync:CatalogMinutes", 360)));

                    // Seyrek dilim önce: yeni ürünler eklendiyse hemen ardından gelen
                    // fiyat/stok dilimi yeni varyantlara stok satırlarını açar.
                    if (simdi - sonKatalog >= katalogAralik)
                    {
                        sonKatalog = simdi;
                        if (config.GetValue("Legacy:Sync:NewProducts", true))
                        {
                            var rapor = await sync.SyncNewProductsAsync(stoppingToken);
                            await RaporlaAsync(rapor, stoppingToken);
                            if (rapor is { Success: true, DryRun: false, Changed: > 0 })
                                sonFiyatStok = DateTime.MinValue; // stok/fiyatı hemen tazele
                        }
                        if (config.GetValue("Legacy:Sync:Images", true))
                            await RaporlaAsync(await sync.SyncImagesAsync(stoppingToken), stoppingToken);
                    }

                    if (DateTime.UtcNow - sonFiyatStok >= fiyatStokAralik)
                    {
                        sonFiyatStok = DateTime.UtcNow;
                        await RaporlaAsync(await sync.SyncPriceAndStockAsync(stoppingToken), stoppingToken);
                    }

                    // F1 (2026-08-04): sipariş outbox dilimi — sık (varsayılan 2 dk); kuyruk
                    // boşken maliyeti tek SELECT. Rapor yalnız iş varken loglanır (RaporlaAsync).
                    var siparisAralik = TimeSpan.FromMinutes(Math.Max(1, config.GetValue("Legacy:Sync:OrderMinutes", 2)));
                    if (config.GetValue("Legacy:Sync:Orders", true) && DateTime.UtcNow - sonSiparis >= siparisAralik)
                    {
                        sonSiparis = DateTime.UtcNow;
                        await RaporlaAsync(await orderSync.SyncOrderOutboxAsync(stoppingToken), stoppingToken);
                        // F2: eskiye bağlanmış açık siparişlerin durum + kargo bilgisi geri çekilir
                        // (aynı kadans; LegacyOrderId'li sipariş yokken maliyeti tek SELECT).
                        await RaporlaAsync(await orderSync.SyncOrderStatusAsync(stoppingToken), stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "LegacySyncWorker döngü hatası");
            }

            try { await Task.Delay(KontrolAraligi, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RaporlaAsync(LegacySyncService.Report r, CancellationToken ct)
    {
        logger.LogInformation("Legacy senkron [{Slice}] {Durum}: değişiklik={Changed}, süre={Ms}ms{Hata}\n{Detay}",
            r.Slice, r.Success ? (r.DryRun ? "DRY-RUN" : "OK") : "HATA", r.Changed, r.DurationMs,
            r.Error is null ? "" : $", hata={r.Error}", r.Detail.TrimEnd());

        // integration_logs yalnız anlamlı koşularda (değişiklik var veya hata) — 10 dk'lık
        // kadansta "değişiklik yok" satırlarıyla tabloyu şişirmeyelim.
        if (r.Success && r.Changed == 0 && r.Error is null) return;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IIntegrationDbContext>();
            db.IntegrationLogs.Add(new IntegrationLog
            {
                FirmIntegrationId = Guid.Empty,
                ServiceType = "legacy",
                OperationType = $"sync_{r.Slice}",
                Status = r.Success ? (r.DryRun ? "dry_run" : "success") : "failure",
                RequestPayload = r.Detail.Length <= 8000 ? r.Detail : r.Detail[..8000],
                ErrorMessage = r.Error is { Length: > 1000 } e ? e[..1000] : r.Error,
                DurationMs = r.DurationMs,
                ReferenceType = "LegacySync",
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Legacy senkron log kaydı yazılamadı ({Slice})", r.Slice);
        }
    }
}
