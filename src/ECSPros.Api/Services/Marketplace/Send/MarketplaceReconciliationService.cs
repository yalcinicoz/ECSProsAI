using ECSPros.Api.Services.Marketplace.Reference;
using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECSPros.Api.Services.Marketplace.Send;

public sealed record ReconcileResultDto(
    int RemoteCount,
    int Compared,
    int AutoFixQueued,
    int PriceDriftIssues,
    int MissingOnMarketplace,
    int UnknownResolved,
    int RemoteCategoryOverrides,
    int AutoResolvedIssues);

/// <summary>
/// Mutabakat job'ı (§5): pazaryerindeki fiili listing'i çekip bizde olması gerekenle
/// karşılaştırır. Stok farkı ve eşik-altı fiyat farkı otomatik düzeltme paketine girer
/// (bizim veri kazanır); eşik-üstü fiyat farkı ISSUE olur — pazaryeri tarafındaki fark
/// bilinçli olabilir (kampanya/komisyon), körlemesine ezilmez. Bizde synced görünüp
/// pazaryerinde olmayan ürün issue + yeniden gönderime açılır. Pazaryerinin fiili
/// kategorisi farklıysa Source=remote istisna yazılır (sonraki güncellemeler reddedilmesin).
/// Zaman aşımına düşmüş (unknown) batch item'ları burada çözülür (K7).
/// </summary>
public sealed class MarketplaceReconciliationService(
    NpgsqlDataSource mainDb,
    MarketplaceRefDb refDb,
    IIntegrationDbContext db,
    MarketplaceAdminService admin,
    TrendyolSellerClient trendyol,
    MarketplaceSendService sendService,
    MarketplaceIssueService issues,
    IConfiguration configuration,
    ILogger<MarketplaceReconciliationService> logger)
{
    private const int PageSize = 200;
    private const int MaxPages = 100;

    public async Task<(ReconcileResultDto? Result, string? Error)> RunAsync(
        Guid firmPlatformId, Guid firmIntegrationId, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        var marketplace = await admin.GetMarketplaceCodeAsync(firmPlatformId, ct);
        if (marketplace is null) return (null, "Mağaza bulunamadı.");

        var (config, cfgError) = await trendyol.ResolveConfigAsync(firmIntegrationId, ct);
        if (config is null) return (null, cfgError);

        var driftPercent = configuration.GetValue("Marketplace:PriceDriftPercent", 10m);

        // 1) Pazaryerindeki fiili listing (sayfalı tam çekim)
        var remote = new Dictionary<string, TrendyolSellerClient.TrendyolListingItem>(StringComparer.OrdinalIgnoreCase);
        var page = 0;
        int totalPages;
        do
        {
            ct.ThrowIfCancellationRequested();
            var (items, tp) = await trendyol.GetProductsPageAsync(config, page, PageSize, ct);
            foreach (var item in items) remote[item.Barcode] = item;
            totalPages = tp;
            page++;
        } while (page < totalPages && page < MaxPages);
        if (page == MaxPages && totalPages > MaxPages)
            logger.LogWarning("Mutabakat: {Marketplace} listing {Max} sayfada kesildi ({Total} sayfa var).",
                marketplace, MaxPages, totalPages);

        // 2) Bizim listing'ler + beklenen fiyat/stok
        var listings = await db.MarketplaceProducts
            .Where(mp => mp.FirmIntegrationId == firmIntegrationId)
            .ToListAsync(ct);
        var syncedListings = listings.Where(l => l.SyncStatus == "synced" && l.ExternalBarcode != null).ToList();
        var variantIds = syncedListings.Select(l => l.VariantId).ToList();
        var payloads = variantIds.Count > 0
            ? await admin.GetSyncPayloadsAsync(firmPlatformId, variantIds, null, ct) : [];
        var priceByVariant = payloads.ToDictionary(p => p.VariantId, p => p.Price);
        var productByVariant = payloads.ToDictionary(p => p.VariantId, p => p.ProductId);
        var stocks = await admin.GetSellableStocksAsync(variantIds, ct);

        var seenKeys = new HashSet<string>();
        var autoFix = new List<Guid>();
        int priceDriftIssues = 0, missing = 0, compared = 0, remoteOverrides = 0;

        foreach (var listing in syncedListings)
        {
            var barcode = listing.ExternalBarcode!;
            if (!remote.TryGetValue(barcode, out var r))
            {
                // Bizde yüklü görünen ürün pazaryerinde yok
                missing++;
                var key = $"missing:{barcode}";
                seenKeys.Add(key);
                await issues.UpsertOpenAsync(marketplace, firmPlatformId, "missing_on_marketplace", key,
                    $"Ürün pazaryerinde bulunamadı: {barcode}",
                    "Bizde 'yüklü' görünüyor ama pazaryeri listing'inde yok (silinmiş/arşivlenmiş olabilir).",
                    "Ürünü Hazır listesinden yeniden gönderin.",
                    "variant", listing.VariantId, ct);
                listing.LastSentPayloadHash = null; // yeniden gönderime açılır (diff engellemesin)
                continue;
            }

            compared++;
            var expectedStock = stocks.GetValueOrDefault(listing.VariantId);
            var expectedPrice = priceByVariant.GetValueOrDefault(listing.VariantId);

            var stockDiff = r.Quantity is int rq && rq != expectedStock;
            var priceDiff = expectedPrice > 0 && r.SalePrice is decimal rp && rp != expectedPrice;
            if (priceDiff)
            {
                var pct = Math.Abs(expectedPrice - r.SalePrice!.Value) / expectedPrice * 100;
                if (pct > driftPercent)
                {
                    // Büyük sapma bilinçli olabilir — körlemesine ezme, personele sor
                    priceDriftIssues++;
                    var key = $"price:{barcode}";
                    seenKeys.Add(key);
                    await issues.UpsertOpenAsync(marketplace, firmPlatformId, "price_drift", key,
                        $"Fiyat sapması %{pct:0.#}: {barcode}",
                        $"Bizde {expectedPrice:0.##} ₺, pazaryerinde {r.SalePrice:0.##} ₺.",
                        "Pazaryerindeki fark kampanya/elle müdahale olabilir — doğruysa yoksayın, değilse Stok-Fiyat Güncelle çalıştırın.",
                        "variant", listing.VariantId, ct);
                    priceDiff = false; // otomatik düzeltmeye GİRMEZ
                }
            }
            if (stockDiff || priceDiff)
                autoFix.Add(listing.VariantId);

            // Fiili kategori bizim çözümden farklıysa Source=remote istisna (K4)
            if (r.CategoryId is long remoteCat && productByVariant.TryGetValue(listing.VariantId, out var productId)
                && await ApplyRemoteCategoryAsync(marketplace, productId, remoteCat.ToString(), ct))
                remoteOverrides++;

            // Pazaryerindeki güncel değerleri listing'e işle (görünürlük)
            if (r.SalePrice is decimal sp2) listing.MarketplacePrice = sp2;
            if (r.Quantity is int q2) listing.MarketplaceStock = q2;
            listing.StockSyncedAt = DateTime.UtcNow;
        }

        // 3) unknown batch item'larını listing gerçeğiyle çöz (K7 — körlemesine resend yerine doğrulama)
        var unknownResolved = await ResolveUnknownItemsAsync(firmIntegrationId, remote, ct);

        // 4) Kaybolan koşulların açık issue'ları otomatik kapanır
        var autoResolved = await issues.ResolveStaleAsync(
            firmPlatformId, ["price_drift", "missing_on_marketplace"], seenKeys, ct);

        await db.SaveChangesAsync(ct);

        // 5) Otomatik düzeltme paketi (stok + eşik-altı fiyat) — bizim veri kazanır
        if (autoFix.Count > 0)
        {
            var (_, fixError) = await sendService.SubmitPriceStockAsync(
                firmPlatformId, firmIntegrationId, autoFix, ct);
            if (fixError is not null)
                logger.LogWarning("Mutabakat otomatik düzeltme gönderilemedi: {Error}", fixError);
        }

        // 6) Koşu kaydı (Senkron Geçmişi'nde görünür)
        db.IntegrationLogs.Add(new IntegrationLog
        {
            FirmIntegrationId = firmIntegrationId,
            ServiceType = "marketplace",
            OperationType = "reconcile",
            Status = "success",
            DurationMs = (int)(DateTime.UtcNow - started).TotalMilliseconds,
            ResponsePayload = System.Text.Json.JsonSerializer.Serialize(new
            {
                remote = remote.Count, compared, autoFix = autoFix.Count,
                priceDriftIssues, missing, unknownResolved, autoResolved
            })
        });
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Mutabakat ({Marketplace}): {Remote} pazaryeri kaydı, {Compared} karşılaştırıldı, {Fix} otomatik düzeltme, {Drift} fiyat sapması, {Missing} kayıp, {Overrides} remote kategori.",
            marketplace, remote.Count, compared, autoFix.Count, priceDriftIssues, missing, remoteOverrides);
        return (new ReconcileResultDto(remote.Count, compared, autoFix.Count, priceDriftIssues,
            missing, unknownResolved, remoteOverrides, autoResolved), null);
    }

    /// <summary>Pazaryerinin fiili kategorisi çözümümüzden farklıysa Source=remote istisna yazar —
    /// mevcut manuel/red istisnasının ÜZERİNE yazmaz (personel kararı korunur).</summary>
    private async Task<bool> ApplyRemoteCategoryAsync(
        string marketplace, Guid productId, string remoteCategoryId, CancellationToken ct)
    {
        var readiness = await db.MarketplaceProductReadiness.AsNoTracking()
            .Where(r => r.Marketplace == marketplace && r.ProductId == productId && r.FirmPlatformId == null)
            .Select(r => r.ResolvedCategoryExternalId)
            .FirstOrDefaultAsync(ct);
        if (readiness == remoteCategoryId) return false; // fark yok

        var existing = await db.MarketplaceProductCategoryOverrides.FirstOrDefaultAsync(
            o => o.Marketplace == marketplace && o.ProductId == productId && o.FirmPlatformId == null, ct);
        if (existing is not null && existing.Source != "remote") return false; // personel/red kararına dokunma
        if (existing?.CategoryExternalId == remoteCategoryId) return false;

        // Ad/path snapshot'ı referans DB'den (yoksa kimlikle yetin — K3 gereği boş bırakılmaz)
        string name = remoteCategoryId, path = remoteCategoryId;
        var ds = await refDb.GetAsync(ct);
        if (ds is not null)
        {
            await using var cmd = ds.CreateCommand(
                "SELECT name, path FROM mp_categories WHERE marketplace=$1 AND external_id=$2");
            cmd.Parameters.AddWithValue(marketplace);
            cmd.Parameters.AddWithValue(remoteCategoryId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct)) { name = r.GetString(0); path = r.GetString(1); }
        }

        if (existing is null)
        {
            db.MarketplaceProductCategoryOverrides.Add(new MarketplaceProductCategoryOverride
            {
                ProductId = productId,
                Marketplace = marketplace,
                CategoryExternalId = remoteCategoryId,
                CategoryName = name,
                CategoryPath = path,
                Source = "remote",
                Note = "Mutabakat: pazaryerindeki fiili kategori."
            });
        }
        else
        {
            existing.CategoryExternalId = remoteCategoryId;
            existing.CategoryName = name;
            existing.CategoryPath = path;
            existing.Note = "Mutabakat: pazaryerindeki fiili kategori (güncellendi).";
            existing.UpdatedAt = DateTime.UtcNow;
        }
        return true;
    }

    private async Task<int> ResolveUnknownItemsAsync(
        Guid firmIntegrationId,
        Dictionary<string, TrendyolSellerClient.TrendyolListingItem> remote,
        CancellationToken ct)
    {
        var unknownItems = await (
            from item in db.MarketplaceBatchItems
            join batch in db.MarketplaceBatches on item.BatchId equals batch.Id
            where batch.FirmIntegrationId == firmIntegrationId && item.Status == "unknown"
            select item).ToListAsync(ct);
        if (unknownItems.Count == 0) return 0;

        var variantIds = unknownItems.Select(i => i.VariantId).Distinct().ToList();
        var listings = await db.MarketplaceProducts
            .Where(mp => mp.FirmIntegrationId == firmIntegrationId && variantIds.Contains(mp.VariantId))
            .ToDictionaryAsync(mp => mp.VariantId, ct);

        foreach (var item in unknownItems)
        {
            item.ResolvedAt = DateTime.UtcNow;
            var found = remote.ContainsKey(item.Barcode);
            item.Status = found ? "success" : "failed";
            if (!found) item.ErrorRaw = "Zaman aşımı sonrası pazaryeri listing'inde bulunamadı — yeniden gönderilebilir.";
            if (listings.TryGetValue(item.VariantId, out var mp))
            {
                if (found)
                {
                    mp.SyncStatus = "synced";
                    mp.LastSyncedAt = DateTime.UtcNow;
                    mp.LastSyncError = null;
                }
                else
                {
                    mp.SyncStatus = "failed";
                    mp.LastSyncError = "Gönderim sonucu alınamadı ve ürün pazaryerinde görünmüyor.";
                    mp.LastSentPayloadHash = null;
                }
                mp.UpdatedAt = DateTime.UtcNow;
            }
        }
        return unknownItems.Count;
    }
}
