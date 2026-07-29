using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Marketplace.Send;

/// <summary>
/// Batch sonuç sorgulama worker'ı (§4.2, K7): sırası gelen açık paketleri pazaryerinden
/// sorgular. Kısmi cevap normaldir — yalnız dönen item'lar çözülür, kalan pending aynı
/// ExternalBatchId ile sonraki turda sorgulanır. Backoff 1→2→5→10→30 dk; 24 saati aşan
/// paket timed_out olur ve kalan item'lar 'unknown'a düşer — KÖRLEMESİNE yeniden
/// gönderilmez (duplicate riski), mutabakat senkronu doğrular (F5).
/// "Sorgulama bitti mi" takibi personelin değil bu worker'ın işidir.
/// </summary>
public sealed class MarketplaceBatchWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<MarketplaceBatchWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);
    private static readonly int[] BackoffMinutes = [1, 2, 5, 10, 30];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Açılışı bekle — ilk turda DB hazır olsun
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessDueBatchesAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Batch worker turu başarısız."); }

            try { await Task.Delay(Tick, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <param name="force">true → NextPollAt beklemeden tüm açık paketler sorgulanır (elle tetikleme).</param>
    public async Task ProcessDueBatchesAsync(CancellationToken ct, bool force = false)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IIntegrationDbContext>();

        var now = DateTime.UtcNow;
        var due = await db.MarketplaceBatches
            .Where(b => (b.Status == "submitted" || b.Status == "polling")
                        && (force || b.NextPollAt == null || b.NextPollAt <= now))
            .OrderBy(b => b.SubmittedAt)
            .Take(20)
            .ToListAsync(ct);
        if (due.Count == 0) return;

        var client = scope.ServiceProvider.GetRequiredService<TrendyolSellerClient>();
        var classifier = scope.ServiceProvider.GetRequiredService<MarketplaceErrorClassifier>();
        var issues = scope.ServiceProvider.GetRequiredService<MarketplaceIssueService>();

        foreach (var batch in due)
        {
            ct.ThrowIfCancellationRequested();
            try { await PollBatchAsync(db, client, classifier, issues, batch, ct); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Batch sorgulanamadı: {Batch} ({External})", batch.Id, batch.ExternalBatchId);
                Reschedule(batch);
            }
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task PollBatchAsync(
        IIntegrationDbContext db, TrendyolSellerClient client, MarketplaceErrorClassifier classifier,
        MarketplaceIssueService issues, MarketplaceBatch batch, CancellationToken ct)
    {
        // Zaman aşımı: kalanlar unknown — körlemesine yeniden gönderme yok (mutabakat doğrular)
        if (DateTime.UtcNow - batch.SubmittedAt > MaxAge)
        {
            var stale = await db.MarketplaceBatchItems
                .Where(i => i.BatchId == batch.Id && i.Status == "pending").ToListAsync(ct);
            foreach (var item in stale)
            {
                item.Status = "unknown";
                item.ResolvedAt = DateTime.UtcNow;
            }
            batch.Status = "timed_out";
            batch.Error = $"{stale.Count} satırın sonucu {MaxAge.TotalHours:0} saat içinde alınamadı — mutabakat senkronu doğrulayacak.";
            batch.NextPollAt = null;
            await issues.UpsertOpenAsync(batch.Marketplace, batch.FirmPlatformId, "batch_timed_out",
                $"batch_timeout:{batch.ExternalBatchId}",
                $"Gönderim paketi zaman aşımına uğradı ({stale.Count} satır)",
                $"Paket {batch.ExternalBatchId} — sonuç 24 saat içinde alınamadı.",
                "Mutabakat çalıştırın: pazaryerindeki fiili durum item'ları çözer.",
                "batch", batch.Id, ct);
            logger.LogWarning("Batch zaman aşımı: {Batch} — {Count} satır unknown.", batch.ExternalBatchId, stale.Count);
            return;
        }

        if (batch.ExternalBatchId is null)
        {
            batch.Status = "failed";
            batch.Error = "ExternalBatchId yok — gönderim kabul edilmemiş.";
            return;
        }

        var (config, cfgError) = await client.ResolveConfigAsync(batch.FirmIntegrationId, ct);
        if (config is null)
        {
            batch.Error = cfgError;
            Reschedule(batch);
            return;
        }

        var result = await client.GetBatchStatusAsync(config, batch.ExternalBatchId, ct);
        batch.LastPolledAt = DateTime.UtcNow;
        if (result is null || result.Items.Count == 0)
        {
            Reschedule(batch); // pazaryeri henüz işlemedi — normal, sabırla sor
            return;
        }

        var pending = await db.MarketplaceBatchItems
            .Where(i => i.BatchId == batch.Id && i.Status == "pending").ToListAsync(ct);
        var byBarcode = pending
            .GroupBy(i => i.Barcode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var resolvedItems = new List<(MarketplaceBatchItem Item, bool Success)>();
        foreach (var r in result.Items)
        {
            if (!byBarcode.TryGetValue(r.Barcode, out var item)) continue;
            // Pazaryeri hâlâ işliyorsa item'ı çözme — sonraki turda kesin sonuç gelir
            var status = r.Status.ToUpperInvariant();
            if (status is not ("SUCCESS" or "FAILED")) continue;

            item.ResolvedAt = DateTime.UtcNow;
            if (status == "SUCCESS")
            {
                item.Status = "success";
                batch.SuccessCount++;
                resolvedItems.Add((item, true));
            }
            else
            {
                var classified = await classifier.ClassifyAsync(batch.Marketplace, r.FailureReason, ct);
                item.Status = "failed";
                item.ErrorRaw = Truncate(r.FailureReason, 2000);
                item.ErrorCode = classified.Code;
                item.SuggestedCategoryExternalId = classified.SuggestedCategoryExternalId;
                batch.FailedCount++;
                resolvedItems.Add((item, false));
            }
            batch.ResolvedCount++;
        }

        // Listing kayıtlarını güncelle (varyant düzeyi) — paket tipine göre ayrışır
        if (resolvedItems.Count > 0)
        {
            var variantIds = resolvedItems.Select(x => x.Item.VariantId).ToList();
            var listings = await db.MarketplaceProducts
                .Where(mp => mp.FirmIntegrationId == batch.FirmIntegrationId && variantIds.Contains(mp.VariantId))
                .ToDictionaryAsync(mp => mp.VariantId, ct);
            foreach (var (item, success) in resolvedItems)
            {
                if (!listings.TryGetValue(item.VariantId, out var mp)) continue;
                if (batch.BatchType == "price_stock")
                {
                    // Fiyat-stok: ürün yüklü kalır; başarıda hedef değerler işlenir,
                    // hatada durum bozulmaz ama hata görünür olur.
                    if (success)
                    {
                        mp.MarketplacePrice = item.SentPrice ?? mp.MarketplacePrice;
                        mp.MarketplaceStock = item.SentStock ?? mp.MarketplaceStock;
                        mp.StockSyncedAt = DateTime.UtcNow;
                        mp.LastSyncError = null;
                        mp.LastErrorCode = null;
                    }
                    else
                    {
                        mp.LastSyncError = Truncate(item.ErrorRaw, 500);
                        mp.LastErrorCode = item.ErrorCode;
                    }
                }
                else if (success)
                {
                    mp.SyncStatus = "synced";
                    mp.LastSyncedAt = DateTime.UtcNow;
                    mp.LastSyncError = null;
                    mp.LastErrorCode = null;
                    mp.SuggestedCategoryExternalId = null;
                }
                else
                {
                    mp.SyncStatus = "failed";
                    mp.LastSyncError = Truncate(item.ErrorRaw, 500);
                    mp.LastErrorCode = item.ErrorCode;
                    mp.SuggestedCategoryExternalId = item.SuggestedCategoryExternalId;
                    mp.LastSentPayloadHash = null; // düzeltme sonrası yeniden gönderim diff'e takılmasın
                }
                mp.UpdatedAt = DateTime.UtcNow;
            }
        }

        var stillPending = pending.Count(i => i.Status == "pending");
        if (stillPending == 0)
        {
            batch.Status = batch.FailedCount > 0 ? "completed_with_errors" : "completed";
            batch.NextPollAt = null;
            logger.LogInformation("Batch tamamlandı: {Batch} — {Ok} başarılı, {Fail} hatalı.",
                batch.ExternalBatchId, batch.SuccessCount, batch.FailedCount);
        }
        else
        {
            Reschedule(batch); // kısmi cevap — kalan item'lar sonraki turda
        }
    }

    private static void Reschedule(MarketplaceBatch batch)
    {
        batch.Status = "polling";
        batch.PollAttempts++;
        var minutes = BackoffMinutes[Math.Min(batch.PollAttempts - 1, BackoffMinutes.Length - 1)];
        batch.NextPollAt = DateTime.UtcNow.AddMinutes(minutes);
    }

    private static string? Truncate(string? s, int max) =>
        s is null ? null : s.Length <= max ? s : s[..max] + "…";
}
