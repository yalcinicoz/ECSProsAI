using ECSPros.Api.Services.Marketplace;
using ECSPros.Api.Services.Marketplace.Reference;
using ECSPros.Api.Services.Marketplace.Send;
using ECSPros.Integration.Application.Adapters;
using ECSPros.Integration.Application.Commands.FetchMarketplaceOrders;
using ECSPros.Integration.Application.Commands.SyncMarketplaceProduct;
using ECSPros.Integration.Application.Commands.UpdateMarketplaceStock;
using ECSPros.Integration.Application.Queries.GetIntegrationLogs;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// Pazaryeri mağaza yönetimi (admin): mağaza kartları özeti, mağaza ürünleri
/// (yüklü + yüklenecek), elle senkron tetikleme ve senkron geçmişi.
/// Mağaza = PlatformType.IsMarketplace olan FirmPlatform; senkron işlemleri mağazanın
/// aktif pazaryeri sözleşmesi (FirmPlatformIntegration) üzerinden yürür.
/// </summary>
[ApiController]
[Route("api/marketplaces")]
[Authorize]
public class MarketplacesController(
    IMediator mediator,
    MarketplaceAdminService service,
    MarketplaceReferenceSyncService referenceSync,
    MarketplaceSendService sendService,
    MarketplaceBatchWorker batchWorker) : ControllerBase
{
    // Batch (asenkron paket) gönderimi destekleyen servisler — diğerleri eski adapter yolunda kalır.
    private static readonly HashSet<string> BatchCapable = ["trendyol"];
    // Tek istekte işlenecek üst sınırlar — adapter'lar sıralı çağrılır, istek süresi patlamasın.
    private const int MaxSyncVariants = 200;
    private const int MaxStockVariants = 500;

    /// <summary>Pazaryeri mağazaları özeti — kart ekranının tek çağrısı.</summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(CancellationToken ct)
    {
        var stores = await service.GetOverviewAsync(ct);
        return Ok(new { success = true, data = stores });
    }

    /// <summary>Mağazanın ürünleri. status: to_upload | synced | pending | failed | deactivated | boş (tüm gönderilmişler).</summary>
    [HttpGet("{firmPlatformId:guid}/products")]
    public async Task<IActionResult> GetProducts(
        Guid firmPlatformId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await service.GetProductsAsync(
            firmPlatformId, status, search, page, Math.Clamp(pageSize, 1, 200), ct);
        return Ok(new { success = true, data = new { items, totalCount, page, pageSize } });
    }

    /// <summary>Mağazanın senkron geçmişi (pazaryeri sözleşmelerine yazılmış loglar).</summary>
    [HttpGet("{firmPlatformId:guid}/logs")]
    public async Task<IActionResult> GetLogs(
        Guid firmPlatformId,
        [FromQuery] string? operationType,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var integrationIds = await service.GetIntegrationIdsAsync(firmPlatformId, ct);
        if (integrationIds.Count == 0)
            return Ok(new { success = true, data = new PagedResult<IntegrationLogDto>([], 0, page, pageSize) });

        var result = await mediator.Send(new GetIntegrationLogsQuery(
            ServiceType: "marketplace", OperationType: operationType, Status: status,
            Page: page, PageSize: pageSize, FirmIntegrationIds: integrationIds), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    public record SyncProductsRequest(List<Guid>? VariantIds = null, List<Guid>? ProductIds = null);

    /// <summary>Seçilen varyant/ürünleri pazaryerine gönderir (fiyat kanal önceliğiyle, stok online satılabilir toplam).</summary>
    [HttpPost("{firmPlatformId:guid}/sync-products")]
    public async Task<IActionResult> SyncProducts(
        Guid firmPlatformId, [FromBody] SyncProductsRequest request, CancellationToken ct)
    {
        if (request.VariantIds is not { Count: > 0 } && request.ProductIds is not { Count: > 0 })
            return BadRequest(new { success = false, error = "variantIds veya productIds vermelisiniz." });

        var integration = await service.ResolveIntegrationAsync(firmPlatformId, ct);
        if (integration is null)
            return BadRequest(new { success = false, error = "Bu mağaza için aktif pazaryeri bağlantısı (sözleşme) yok. Firma detayından pazaryeri servisine bağlı bir sözleşme ekleyin." });

        // F4: batch destekli pazaryerinde gerçek gönderim — sonuç asenkron, worker sorgular.
        if (BatchCapable.Contains(integration.ServiceCode))
        {
            var (result, error) = await sendService.SubmitProductsAsync(
                firmPlatformId, integration.IntegrationId, request.VariantIds, request.ProductIds, ct);
            if (error is not null)
                return BadRequest(new { success = false, error });
            return Ok(new { success = true, data = new
            {
                mode = "batch",
                submitted = result!.Submitted,
                batchCount = result.BatchCount,
                skippedNotReady = result.SkippedNotReady,
                skippedUnchanged = result.SkippedUnchanged,
                skippedNoBarcode = result.SkippedNoBarcode
            } });
        }

        var payloads = await service.GetSyncPayloadsAsync(firmPlatformId, request.VariantIds, request.ProductIds, ct);
        if (payloads.Count == 0)
            return BadRequest(new { success = false, error = "Gönderilecek aktif varyant bulunamadı." });
        if (payloads.Count > MaxSyncVariants)
            return BadRequest(new { success = false, error = $"Tek istekte en fazla {MaxSyncVariants} varyant gönderilebilir (istenen: {payloads.Count}). Daha küçük parçalar halinde gönderin." });

        var stocks = await service.GetSellableStocksAsync(payloads.Select(p => p.VariantId).ToList(), ct);

        int ok = 0, fail = 0;
        var errors = new List<string>();
        foreach (var p in payloads)
        {
            var payload = new MarketplaceProductPayload(
                p.VariantId, p.Barcode ?? p.Sku, p.Title, p.Description,
                p.Price, stocks.GetValueOrDefault(p.VariantId));
            var result = await mediator.Send(new SyncMarketplaceProductCommand(
                integration.IntegrationId, integration.ServiceCode, payload, firmPlatformId), ct);
            if (result.IsFailure)
            {
                fail++;
                if (errors.Count < 10) errors.Add($"{p.ProductCode}/{p.Sku}: {result.Error}");
            }
            else ok++;
        }

        return Ok(new { success = true, data = new { requested = payloads.Count, succeeded = ok, failed = fail, errors } });
    }

    public record UpdateStocksRequest(List<Guid>? VariantIds = null);

    /// <summary>Stok bilgisini pazaryerine iter — variantIds boşsa mağazanın tüm senkron ürünleri.</summary>
    [HttpPost("{firmPlatformId:guid}/update-stocks")]
    public async Task<IActionResult> UpdateStocks(
        Guid firmPlatformId, [FromBody] UpdateStocksRequest request, CancellationToken ct)
    {
        var integration = await service.ResolveIntegrationAsync(firmPlatformId, ct);
        if (integration is null)
            return BadRequest(new { success = false, error = "Bu mağaza için aktif pazaryeri bağlantısı (sözleşme) yok." });

        // F5: batch destekli pazaryerinde fiyat-stok hızlı kanalı (diff-based, asenkron sonuç).
        if (BatchCapable.Contains(integration.ServiceCode))
        {
            var (result, error) = await sendService.SubmitPriceStockAsync(
                firmPlatformId, integration.IntegrationId, request.VariantIds, ct);
            if (error is not null)
                return BadRequest(new { success = false, error });
            return Ok(new { success = true, data = new
            {
                mode = "batch",
                submitted = result!.Submitted,
                batchCount = result.BatchCount,
                skippedUnchanged = result.SkippedUnchanged
            } });
        }

        var variantIds = request.VariantIds is { Count: > 0 }
            ? request.VariantIds
            : await service.GetSyncedVariantIdsAsync(firmPlatformId, ct);
        if (variantIds.Count == 0)
            return BadRequest(new { success = false, error = "Stok güncellenecek senkron ürün yok." });
        if (variantIds.Count > MaxStockVariants)
            return BadRequest(new { success = false, error = $"Tek istekte en fazla {MaxStockVariants} varyantın stoğu güncellenebilir (istenen: {variantIds.Count})." });

        var stocks = await service.GetSellableStocksAsync(variantIds, ct);

        int ok = 0, fail = 0;
        var errors = new List<string>();
        foreach (var variantId in variantIds)
        {
            var result = await mediator.Send(new UpdateMarketplaceStockCommand(
                integration.IntegrationId, integration.ServiceCode, variantId,
                stocks.GetValueOrDefault(variantId)), ct);
            if (result.IsFailure)
            {
                fail++;
                if (errors.Count < 10) errors.Add(result.Error!);
            }
            else ok++;
        }

        return Ok(new { success = true, data = new { requested = variantIds.Count, succeeded = ok, failed = fail, errors } });
    }

    /// <summary>Mağazanın gönderim paketleri (F4): durum + çözülen/toplam + hatalı satır özetleri.</summary>
    [HttpGet("{firmPlatformId:guid}/batches")]
    public async Task<IActionResult> GetBatches(
        Guid firmPlatformId, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var batches = await service.GetBatchesAsync(firmPlatformId, Math.Clamp(limit, 1, 100), ct);
        return Ok(new { success = true, data = batches });
    }

    /// <summary>Açık paketleri hemen sorgular (worker beklemeden) — elle tetikleme.</summary>
    [HttpPost("batches/poll-now")]
    public async Task<IActionResult> PollBatchesNow(CancellationToken ct)
    {
        await batchWorker.ProcessDueBatchesAsync(ct, force: true);
        return Ok(new { success = true, data = true });
    }

    /// <summary>Yüklenecek adayların denetim sayıları — Hazır/Eksik çipleri (F3).</summary>
    [HttpGet("{firmPlatformId:guid}/readiness-counts")]
    public async Task<IActionResult> GetReadinessCounts(Guid firmPlatformId, CancellationToken ct)
    {
        var counts = await service.GetReadinessCountsAsync(firmPlatformId, ct);
        if (counts is null)
            return BadRequest(new { success = false, error = "Mağaza bulunamadı." });
        return Ok(new { success = true, data = counts });
    }

    // ── Referans verisi senkronu (marketplace_ref DB — kategori/özellik/değer) ──

    public record ReferenceSyncRequest(string Marketplace, string Scope, List<string>? CategoryIds = null);

    /// <summary>
    /// Referans senkronu başlatır (arka planda yürür). scope: categories | attributes.
    /// attributes kapsamı categoryIds ile daraltılabilir; boşsa tüm aktif yaprak kategoriler.
    /// </summary>
    [HttpPost("reference-sync")]
    public async Task<IActionResult> StartReferenceSync([FromBody] ReferenceSyncRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Marketplace))
            return BadRequest(new { success = false, error = "marketplace zorunlu." });

        var (runId, error) = await referenceSync.StartAsync(
            request.Marketplace.Trim().ToLowerInvariant(), request.Scope, request.CategoryIds, ct);
        if (error is not null)
            return BadRequest(new { success = false, error });
        return Ok(new { success = true, data = new { runId } });
    }

    /// <summary>Referans senkron koşuları (ilerleme dahil — süren koşu processedCategories ile izlenir).</summary>
    [HttpGet("reference-sync/runs")]
    public async Task<IActionResult> GetReferenceSyncRuns(
        [FromQuery] string? marketplace, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var runs = await referenceSync.GetRunsAsync(
            string.IsNullOrWhiteSpace(marketplace) ? null : marketplace.Trim().ToLowerInvariant(),
            Math.Clamp(limit, 1, 100), ct);
        return Ok(new { success = true, data = runs });
    }

    /// <summary>Pazaryeri başına referans veri özeti (kategori/özellik/değer sayıları + son koşu).</summary>
    [HttpGet("reference-sync/summary")]
    public async Task<IActionResult> GetReferenceSyncSummary(CancellationToken ct)
    {
        var summary = await referenceSync.GetSummaryAsync(ct);
        if (summary is null)
            return Ok(new { success = true, data = new List<RefSummaryDto>(), configured = false });
        return Ok(new { success = true, data = summary, configured = true });
    }

    // ── Mutabakat + sorun kuyruğu (F5) ──────────────────────────────────────

    /// <summary>Mutabakat: pazaryerindeki fiili listing bizimkiyle karşılaştırılır —
    /// stok/eşik-altı fiyat otomatik düzeltilir, büyük sapmalar ve kayıplar sorun olur,
    /// zaman aşımına düşmüş item'lar çözülür, fiili kategori istisnaya işlenir.</summary>
    [HttpPost("{firmPlatformId:guid}/reconcile")]
    public async Task<IActionResult> Reconcile(
        Guid firmPlatformId,
        [FromServices] MarketplaceReconciliationService reconciliation,
        CancellationToken ct)
    {
        var integration = await service.ResolveIntegrationAsync(firmPlatformId, ct);
        if (integration is null)
            return BadRequest(new { success = false, error = "Bu mağaza için aktif pazaryeri bağlantısı (sözleşme) yok." });
        if (!BatchCapable.Contains(integration.ServiceCode))
            return BadRequest(new { success = false, error = $"'{integration.ServiceCode}' için mutabakat desteği henüz yok." });

        var (result, error) = await reconciliation.RunAsync(firmPlatformId, integration.IntegrationId, ct);
        if (error is not null) return BadRequest(new { success = false, error });
        return Ok(new { success = true, data = result });
    }

    /// <summary>Mağazanın açık sorunları (otomatik açılır/kapanır kuyruk).</summary>
    [HttpGet("{firmPlatformId:guid}/issues")]
    public async Task<IActionResult> GetIssues(
        Guid firmPlatformId, [FromQuery] string status = "open", [FromQuery] int limit = 100, CancellationToken ct = default)
    {
        var issues = await service.GetIssuesAsync(firmPlatformId, status, Math.Clamp(limit, 1, 200), ct);
        return Ok(new { success = true, data = issues });
    }

    /// <summary>Sorunu yoksay — koşul sürerse bir sonraki taramada yeniden açılır (bilinçli).</summary>
    [HttpPost("issues/{id:guid}/dismiss")]
    public async Task<IActionResult> DismissIssue(
        Guid id, [FromServices] MarketplaceIssueService issueService, CancellationToken ct)
    {
        var userId = Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value, out var uid) ? uid : (Guid?)null;
        if (!await issueService.DismissAsync(id, userId, ct))
            return BadRequest(new { success = false, error = "Açık sorun bulunamadı." });
        return Ok(new { success = true, data = true });
    }

    /// <summary>Pazaryerinden yeni siparişleri çeker (elle tetikleme).</summary>
    [HttpPost("{firmPlatformId:guid}/fetch-orders")]
    public async Task<IActionResult> FetchOrders(
        Guid firmPlatformId, [FromQuery] DateTime? since, CancellationToken ct)
    {
        var integration = await service.ResolveIntegrationAsync(firmPlatformId, ct);
        if (integration is null)
            return BadRequest(new { success = false, error = "Bu mağaza için aktif pazaryeri bağlantısı (sözleşme) yok." });

        var result = await mediator.Send(new FetchMarketplaceOrdersCommand(
            integration.IntegrationId, integration.ServiceCode, since), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }
}
