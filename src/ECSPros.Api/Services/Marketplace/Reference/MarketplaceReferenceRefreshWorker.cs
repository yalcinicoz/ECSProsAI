namespace ECSPros.Api.Services.Marketplace.Reference;

/// <summary>
/// RF2 (2026-09-01, plan: docs/pazaryeri-referans-ve-esleme-plani.md) — günlük otomatik
/// referans tazeleme (K3 kararı: haftalık tam + günlük delta). Her gün
/// MarketplaceRef:AutoSync:HourUtc saatinde (vars. 04 UTC = 07 TR) desteklenen her
/// pazaryeri için sırayla:
///   1) scope=categories        → ağaç deltası (tek istek; yeni/kalkan kategori + change_log)
///   2) scope=attributes-missing → yeni yapraklar + damgası ReferenceStaleDays'ten (7 gün)
///      eski olanlar — böylece TAM tarama haftaya kendiliğinden YAYILIR (günde ~1/7).
/// Her koşu sonrası MappingHealthService senkron motoru içinde zaten çalışır (kırık/gözden
/// geçir eşlemeler işaretlenir). Manuel koşu sürüyorsa gün atlanmaz — bir sonraki saat
/// denemesinde tekrar sınanır. Yalnız Worker/Both rollü düğümde kayıtlıdır (A2 kapısı).
/// MarketplaceRef:AutoSync:Enabled=false ile kapatılır.
/// </summary>
public sealed class MarketplaceReferenceRefreshWorker(
    MarketplaceReferenceSyncService sync,
    IConfiguration config,
    ILogger<MarketplaceReferenceRefreshWorker> logger) : BackgroundService
{
    private static readonly TimeSpan KontrolAraligi = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan KosuZamanAsimi = TimeSpan.FromHours(3);

    protected override async Task ExecuteAsync(CancellationToken st)
    {
        var acik = config.GetValue("MarketplaceRef:AutoSync:Enabled", true);
        var saatUtc = Math.Clamp(config.GetValue("MarketplaceRef:AutoSync:HourUtc", 4), 0, 23);
        logger.LogInformation("Referans tazeleme: {Durum} (günlük {Saat}:00 UTC; pazaryerleri: {Liste})",
            acik ? "AKTİF ✓" : "KAPALI (MarketplaceRef:AutoSync:Enabled=false)",
            saatUtc, string.Join(", ", sync.SupportedMarketplaces));
        if (!acik) return;

        DateOnly? sonBasariliGun = null;
        while (!st.IsCancellationRequested)
        {
            try
            {
                var simdi = DateTime.UtcNow;
                var bugun = DateOnly.FromDateTime(simdi);
                if (simdi.Hour >= saatUtc && sonBasariliGun != bugun)
                {
                    var hepsiBasarili = true;
                    foreach (var marketplace in sync.SupportedMarketplaces)
                    {
                        hepsiBasarili &= await GunlukKosuGerekirseCalistirAsync(
                            marketplace, "categories", bugun, st);
                        hepsiBasarili &= await GunlukKosuGerekirseCalistirAsync(
                            marketplace, "attributes-missing", bugun, st);
                    }
                    // Kısmi hata: gün işaretlenmez → sonraki 10 dk kontrolünde yeniden denenir
                    // (StartAsync süren koşuyu zaten reddeder; sonsuz döngü olmaz).
                    if (hepsiBasarili) sonBasariliGun = bugun;
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Referans tazeleme turu hatası");
            }
            try { await Task.Delay(KontrolAraligi, st); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task<bool> GunlukKosuGerekirseCalistirAsync(
        string marketplace, string scope, DateOnly bugun, CancellationToken st)
    {
        if (await sync.HasCompletedRunOnDayAsync(marketplace, scope, bugun, st))
        {
            logger.LogInformation(
                "Referans tazeleme bugün zaten tamamlanmış; atlandı: {Marketplace}/{Scope}",
                marketplace, scope);
            return true;
        }

        return await KosuVeBekleAsync(marketplace, scope, st);
    }

    /// <summary>Senkronu başlatır ve bitişini bekler. true = tamamlandı (ya da iş yoktu).</summary>
    private async Task<bool> KosuVeBekleAsync(string marketplace, string scope, CancellationToken st)
    {
        var (runId, hata) = await sync.StartAsync(marketplace, scope, null, st);
        if (hata is not null)
        {
            // "zaten süren koşu var" dahil — bilgi loglanır, tur başarısız sayılır (yeniden denenir).
            logger.LogWarning("Referans tazeleme başlatılamadı ({Marketplace}/{Scope}): {Hata}",
                marketplace, scope, hata);
            return false;
        }

        var baslangic = DateTime.UtcNow;
        while (!st.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(15), st);
            var kosu = (await sync.GetRunsAsync(marketplace, 10, st)).FirstOrDefault(r => r.Id == runId);
            if (kosu is null) return false; // kayıt kayboldu — bekleme anlamsız
            if (kosu.Status == "completed")
            {
                logger.LogInformation(
                    "Referans tazeleme tamamlandı: {Marketplace}/{Scope} — +{A} ~{C} -{R} ={U}",
                    marketplace, scope, kosu.AddedCount, kosu.ChangedCount, kosu.RemovedCount, kosu.UnchangedCount);
                return true;
            }
            if (kosu.Status == "failed")
            {
                logger.LogError("Referans tazeleme başarısız ({Marketplace}/{Scope}): {Hata}",
                    marketplace, scope, kosu.Error);
                return false;
            }
            if (DateTime.UtcNow - baslangic > KosuZamanAsimi)
            {
                logger.LogError("Referans tazeleme zaman aşımı ({Marketplace}/{Scope}, koşu {RunId})",
                    marketplace, scope, runId);
                return false;
            }
        }
        return false;
    }
}
