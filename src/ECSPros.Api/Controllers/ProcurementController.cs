using ECSPros.Api.Authorization;
using ECSPros.Procurement.Application.Commands.CreatePurchaseOrder;
using ECSPros.Procurement.Application.Commands.DeletePurchaseOrderItem;
using ECSPros.Procurement.Application.Commands.SetPurchaseOrderStatus;
using ECSPros.Procurement.Application.Commands.UpdatePurchaseOrder;
using ECSPros.Procurement.Application.Commands.UpsertPurchaseOrderItems;
using ECSPros.Procurement.Application.Commands.CreateReceiptBatch;
using ECSPros.Procurement.Application.Commands.DeleteReceiptBatchItem;
using ECSPros.Procurement.Application.Commands.SetReceiptBatchPurchaseOrders;
using ECSPros.Procurement.Application.Commands.SetReceiptBatchStatus;
using ECSPros.Procurement.Application.Commands.UpdateReceiptBatch;
using ECSPros.Procurement.Application.Commands.UpsertReceiptBatchItems;
using ECSPros.Procurement.Application.Queries.GetReceiptBatchDetail;
using ECSPros.Procurement.Application.Queries.GetReceiptBatches;
using ECSPros.Procurement.Application.Commands.AccumulateSortingCount;
using ECSPros.Procurement.Application.Commands.PlaceSortingEntry;
using ECSPros.Procurement.Application.Queries.LookupBins;
using ECSPros.Procurement.Application.Commands.CreateSortingEntry;
using ECSPros.Procurement.Application.Commands.DeleteSortingEntry;
using ECSPros.Procurement.Application.Commands.MarkSortingEntryLabeled;
using ECSPros.Procurement.Application.Commands.UpdateSortingEntry;
using ECSPros.Procurement.Application.Commands.CreateMissingCardNotice;
using ECSPros.Procurement.Application.Commands.ResolveMissingCardNotice;
using ECSPros.Procurement.Application.Queries.GetMissingCardNotices;
using ECSPros.Procurement.Application.Queries.GetSortingEntries;
using ECSPros.Procurement.Application.Queries.LookupVariants;
using ECSPros.Procurement.Application.Queries.GetPurchaseOrderDetail;
using ECSPros.Procurement.Application.Queries.GetPurchaseOrders;
using ECSPros.Shared.Kernel.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// T1 Tedarik — Satın Alma (docs/urun-tedarik-is-akisi.md §2.1). HAFİF kayıt katmanı:
/// hiçbir akışı kilitlemez; mal kabul/ayrıştırma bu kayıtlar olmadan da yürür (İ2).
/// </summary>
[ApiController]
[Route("api/procurement")]
[Authorize]
[RequirePermission(Permissions.ProcurementManage)]
public class ProcurementController(IMediator mediator) : ControllerBase
{
    /// <summary>Satın alma listesi (tedarikçi/durum/arama filtreli, sayfalı).</summary>
    [HttpGet("purchase-orders")]
    public async Task<IActionResult> GetPurchaseOrders(
        [FromQuery] Guid? supplierId, [FromQuery] string? status, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetPurchaseOrdersQuery(supplierId, status, search, page, pageSize), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Satın alma detayı (kalemlerle).</summary>
    [HttpGet("purchase-orders/{id:guid}")]
    public async Task<IActionResult> GetPurchaseOrder(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetPurchaseOrderDetailQuery(id), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni satın alma (taslak) oluşturur; kod SA-YYYYAAGG-#### otomatik.</summary>
    [HttpPost("purchase-orders")]
    public async Task<IActionResult> CreatePurchaseOrder([FromBody] CreatePurchaseOrderRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new CreatePurchaseOrderCommand(req.SupplierId, req.OrderDate, req.ExpectedDate, req.Notes), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created(string.Empty, new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Başlık günceller (tarihler, not).</summary>
    [HttpPut("purchase-orders/{id:guid}")]
    public async Task<IActionResult> UpdatePurchaseOrder(Guid id, [FromBody] UpdatePurchaseOrderRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdatePurchaseOrderCommand(id, req.OrderDate, req.ExpectedDate, req.Notes), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Durum geçişi (draft→ordered→receiving→closed; draft/ordered→cancelled; closed→receiving geri açma).</summary>
    [HttpPost("purchase-orders/{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetPurchaseOrderStatusRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new SetPurchaseOrderStatusCommand(id, req.Status), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Kalem ekle/güncelle — panoya yapıştırma da bu uca toplu yeni kalem gönderir (K4).</summary>
    [HttpPost("purchase-orders/{id:guid}/items")]
    public async Task<IActionResult> UpsertItems(Guid id, [FromBody] UpsertPurchaseOrderItemsRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpsertPurchaseOrderItemsCommand(id, req.Items ?? new()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { affected = result.Value } });
    }

    /// <summary>Kalem siler (soft).</summary>
    [HttpDelete("purchase-orders/{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid id, Guid itemId, CancellationToken ct)
    {
        var result = await mediator.Send(new DeletePurchaseOrderItemCommand(id, itemId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ─── T2 Mal Kabul (docs/urun-tedarik-is-akisi.md §2.2) ───────────────────────

    /// <summary>Mal kabul partileri (tedarikçi/durum/arama filtreli, sayfalı).</summary>
    [HttpGet("receipts")]
    public async Task<IActionResult> GetReceiptBatches(
        [FromQuery] Guid? supplierId, [FromQuery] string? status, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetReceiptBatchesQuery(supplierId, status, search, page, pageSize), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Parti detayı (kaba kalemler + bağlı satın almalar).</summary>
    [HttpGet("receipts/{id:guid}")]
    public async Task<IActionResult> GetReceiptBatch(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetReceiptBatchDetailQuery(id), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Parti açar (İ2: kalem bilgisi zorunsuz); kod MK-YYYYAAGG-#### otomatik.</summary>
    [HttpPost("receipts")]
    public async Task<IActionResult> CreateReceiptBatch([FromBody] CreateReceiptBatchRequest req, CancellationToken ct)
    {
        Guid? userId = Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value, out var uid) ? uid : null;
        var result = await mediator.Send(new CreateReceiptBatchCommand(
            req.SupplierId, req.WarehouseId, req.ReceivedAt, req.PackageCount, req.DeliveryNoteNumber, req.Notes, userId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created(string.Empty, new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Başlık günceller (tarih, koli, irsaliye no, fatura bağı, not).</summary>
    [HttpPut("receipts/{id:guid}")]
    public async Task<IActionResult> UpdateReceiptBatch(Guid id, [FromBody] UpdateReceiptBatchRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateReceiptBatchCommand(
            id, req.ReceivedAt, req.PackageCount, req.DeliveryNoteNumber, req.SupplierInvoiceId, req.Notes), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Durum: received→sorting→completed; completed→sorting geri açma.</summary>
    [HttpPost("receipts/{id:guid}/status")]
    public async Task<IActionResult> SetReceiptBatchStatus(Guid id, [FromBody] SetPurchaseOrderStatusRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new SetReceiptBatchStatusCommand(id, req.Status), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Kaba evrak kalemi ekle/güncelle (opsiyonel — yalnız mutabakat girdisi).</summary>
    [HttpPost("receipts/{id:guid}/items")]
    public async Task<IActionResult> UpsertReceiptItems(Guid id, [FromBody] UpsertReceiptBatchItemsRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpsertReceiptBatchItemsCommand(id, req.Items ?? new()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { affected = result.Value } });
    }

    /// <summary>Kaba kalemi siler (soft).</summary>
    [HttpDelete("receipts/{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> DeleteReceiptItem(Guid id, Guid itemId, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteReceiptBatchItemCommand(id, itemId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Parti ↔ SA gevşek bağı: link | unlink (İ3; SA 'ordered' ise bilgi amaçlı 'receiving'e alınır).</summary>
    [HttpPost("receipts/{id:guid}/purchase-orders")]
    public async Task<IActionResult> SetReceiptPurchaseOrders(Guid id, [FromBody] SetReceiptPurchaseOrdersRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new SetReceiptBatchPurchaseOrdersCommand(id, req.PurchaseOrderIds ?? new(), req.Action), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { affected = result.Value } });
    }

    // ─── T4 Ayrıştırma (docs/urun-tedarik-is-akisi.md §2.3 — sistemin kalbi) ─────
    // Yetki: procurement.sort (depo personeli); manage sahipleri role üzerinden sort'u da alır.

    /// <summary>Varyant arama (barkod TAM → SKU TAM → içeren; en çok 10 aday). Yalnız MEVCUT kartlar (K9).</summary>
    [HttpGet("sorting/lookup")]
    public async Task<IActionResult> LookupVariants([FromQuery] string term, CancellationToken ct)
    {
        var result = await mediator.Send(new LookupVariantsQuery(term ?? ""), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Sayım kayıtları (parti / partisiz / yerleştirme durumu filtreli).</summary>
    [HttpGet("sorting/entries")]
    [RequirePermission(Permissions.ProcurementSort)]
    public async Task<IActionResult> GetSortingEntries(
        [FromQuery] Guid? batchId, [FromQuery] bool? unbatched, [FromQuery] string? putawayStatus,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetSortingEntriesQuery(batchId, unbatched, putawayStatus, page, pageSize), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// GERÇEK SAYIM (T4 revizyonu): depoya teslim okutması. Okutma modu quantity=1 ile her okutmada çağrılır,
    /// adet modu tek seferde N gönderir; aynı (parti, varyant) bekleyen kayıtta BİRİKİR.
    /// </summary>
    [HttpPost("sorting/scan")]
    [RequirePermission(Permissions.ProcurementSort)]
    public async Task<IActionResult> Scan([FromBody] ScanCountRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new AccumulateSortingCountCommand(
            req.BatchId, req.VariantId, req.Quantity, req.UnitCost, CurrentUserId()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { entryId = result.Value.EntryId, quantity = result.Value.Quantity } });
    }

    /// <summary>Sayım kaydı oluşturur (İ1). 'received' parti kendiliğinden 'sorting' olur.</summary>
    [HttpPost("sorting/entries")]
    [RequirePermission(Permissions.ProcurementSort)]
    public async Task<IActionResult> CreateSortingEntry([FromBody] CreateSortingEntryRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateSortingEntryCommand(
            req.BatchId, req.VariantId, req.Quantity, req.UnitCost, CurrentUserId()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created(string.Empty, new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Sayım kaydını günceller (yalnız yerleştirilmemiş).</summary>
    [HttpPut("sorting/entries/{id:guid}")]
    [RequirePermission(Permissions.ProcurementSort)]
    public async Task<IActionResult> UpdateSortingEntry(Guid id, [FromBody] UpdateSortingEntryRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateSortingEntryCommand(id, req.Quantity, req.UnitCost), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Sayım kaydını siler (yalnız yerleştirilmemiş; soft).</summary>
    [HttpDelete("sorting/entries/{id:guid}")]
    [RequirePermission(Permissions.ProcurementSort)]
    public async Task<IActionResult> DeleteSortingEntry(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteSortingEntryCommand(id), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Etiket basıldı işareti (basım /yazdir/etiket sekmesinde yapılır; sayaç burada tutulur).</summary>
    [HttpPost("sorting/entries/{id:guid}/labeled")]
    [RequirePermission(Permissions.ProcurementSort)]
    public async Task<IActionResult> MarkLabeled(Guid id, [FromBody] MarkLabeledRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new MarkSortingEntryLabeledCommand(id, req.Count), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>T5: birim (raf) arama — barkod TAM → kod içeren; kısım/depo adlarıyla.</summary>
    [HttpGet("sorting/bins")]
    public async Task<IActionResult> LookupBins([FromQuery] string term, [FromQuery] Guid? warehouseId, CancellationToken ct)
    {
        var result = await mediator.Send(new LookupBinsQuery(term ?? "", warehouseId), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// T5 Yerleştirme: sayım kaydını birime atar → STOK GİRER (movement: purchase, Ref=sorting_entry).
    /// quantity verilmezse tamamı; kısmi yerleştirmede kalan yeni bekleyen kayıtta kalır.
    /// </summary>
    [HttpPost("sorting/entries/{id:guid}/place")]
    [RequirePermission(Permissions.ProcurementSort)]
    public async Task<IActionResult> PlaceSortingEntry(Guid id, [FromBody] PlaceEntryRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new PlaceSortingEntryCommand(id, req.BinId, req.Quantity, CurrentUserId()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>K9 kart-eksik bildirimleri (varsayılan yalnız açık olanlar).</summary>
    [HttpGet("sorting/missing-cards")]
    [RequirePermission(Permissions.ProcurementSort)]
    public async Task<IActionResult> GetMissingCards([FromQuery] Guid? batchId, [FromQuery] string? status, CancellationToken ct)
    {
        var result = await mediator.Send(new GetMissingCardNoticesQuery(batchId, status ?? "open"), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kart eksik bildirimi düşer (kart AÇILMAZ — katalog sorumlusu kuyruğu, K9).</summary>
    [HttpPost("sorting/missing-cards")]
    [RequirePermission(Permissions.ProcurementSort)]
    public async Task<IActionResult> CreateMissingCard([FromBody] CreateMissingCardRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateMissingCardNoticeCommand(req.BatchId, req.DescriptionText, CurrentUserId()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created(string.Empty, new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Bildirimi çözer (kart açıldı).</summary>
    [HttpPost("sorting/missing-cards/{id:guid}/resolve")]
    [RequirePermission(Permissions.ProcurementSort)]
    public async Task<IActionResult> ResolveMissingCard(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new ResolveMissingCardNoticeCommand(id, CurrentUserId()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>
    /// T6 Tedarik Raporu: dönemsel mutabakat (SA ↔ sayım ↔ fatura — İ4: KESİN DEĞİLDİR) + KPI'lar +
    /// satışa girmeyenler. Varsayılan dönem: son 30 gün.
    /// </summary>
    [HttpGet("report")]
    public async Task<IActionResult> GetReport(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? supplierId,
        [FromServices] ECSPros.Api.Services.ProcurementReportService reportSvc, CancellationToken ct)
    {
        var t = (to ?? DateTime.UtcNow.Date.AddDays(1)).ToUniversalTime();
        var f = (from ?? t.AddDays(-30)).ToUniversalTime();
        if (f >= t) return BadRequest(new { success = false, error = "Başlangıç bitişten önce olmalı." });
        var (lines, kpis, notOnSale) = await reportSvc.GetAsync(f, t, supplierId, ct);
        return Ok(new { success = true, data = new { from = f, to = t, lines, kpis, notOnSale } });
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value, out var uid) ? uid : null;
}


public record CreateReceiptBatchRequest(Guid SupplierId, Guid WarehouseId, DateTime? ReceivedAt, int? PackageCount, string? DeliveryNoteNumber, string? Notes);
public record UpdateReceiptBatchRequest(DateTime? ReceivedAt, int? PackageCount, string? DeliveryNoteNumber, Guid? SupplierInvoiceId, string? Notes);
public record UpsertReceiptBatchItemsRequest(List<ReceiptBatchItemInput>? Items);
public record SetReceiptPurchaseOrdersRequest(List<Guid>? PurchaseOrderIds, string Action);
public record CreateSortingEntryRequest(Guid? BatchId, Guid VariantId, decimal Quantity, decimal? UnitCost);
public record UpdateSortingEntryRequest(decimal Quantity, decimal? UnitCost);
public record MarkLabeledRequest(int Count);
public record CreateMissingCardRequest(Guid? BatchId, string DescriptionText);
public record ScanCountRequest(Guid? BatchId, Guid VariantId, decimal Quantity = 1, decimal? UnitCost = null);
public record PlaceEntryRequest(Guid BinId, decimal? Quantity = null);

public record CreatePurchaseOrderRequest(Guid SupplierId, DateTime? OrderDate, DateTime? ExpectedDate, string? Notes);
public record UpdatePurchaseOrderRequest(DateTime? OrderDate, DateTime? ExpectedDate, string? Notes);
public record SetPurchaseOrderStatusRequest(string Status);
public record UpsertPurchaseOrderItemsRequest(List<PurchaseOrderItemInput>? Items);
