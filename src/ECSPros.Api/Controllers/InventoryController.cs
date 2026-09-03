using ECSPros.Inventory.Application.Commands.AddTransferItem;
using ECSPros.Inventory.Application.Commands.AdjustStock;
using ECSPros.Inventory.Application.Commands.BulkDeleteLocations;
using ECSPros.Inventory.Application.Commands.CreateTransfer;
using ECSPros.Inventory.Application.Commands.CreateWarehouse;
using ECSPros.Inventory.Application.Commands.CreateWarehouseLocation;
using ECSPros.Inventory.Application.Commands.UpdateTransferStatus;
using ECSPros.Inventory.Application.Commands.UpdateWarehouse;
using ECSPros.Inventory.Application.Commands.UpdateWarehouseLocation;
using ECSPros.Inventory.Application.Queries.GetReservations;
using ECSPros.Inventory.Application.Queries.GetStocks;
using ECSPros.Inventory.Application.Queries.GetTransferDetail;
using ECSPros.Inventory.Application.Queries.GetTransfers;
using ECSPros.Inventory.Application.Queries.GetWarehouseLocations;
using ECSPros.Inventory.Application.Queries.GetWarehouses;
using ECSPros.Shared.Contracts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICacheBustPublisher _cacheBust;

    public InventoryController(IMediator mediator, ICacheBustPublisher cacheBust)
    {
        _mediator = mediator;
        _cacheBust = cacheBust;
    }

    /// <summary>Depoları listeler.</summary>
    [HttpGet("warehouses")]
    public async Task<IActionResult> GetWarehouses([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetWarehousesQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni depo oluşturur.</summary>
    [HttpPost("warehouses")]
    public async Task<IActionResult> CreateWarehouse([FromBody] CreateWarehouseRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateWarehouseCommand(
            request.Code,
            request.NameI18n,
            request.WarehouseType ?? "main",
            request.Address,
            request.IsSellableOnline,
            request.ReservePriority,
            request.SortOrder), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/inventory/warehouses", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Depo günceller.</summary>
    [HttpPut("warehouses/{id:guid}")]
    public async Task<IActionResult> UpdateWarehouse(Guid id, [FromBody] UpdateWarehouseRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        Guid.TryParse(userId, out var uid);

        var result = await _mediator.Send(new UpdateWarehouseCommand(
            id,
            request.NameI18n,
            request.WarehouseType,
            request.Address,
            request.IsSellableOnline,
            request.ReservePriority,
            request.IsActive,
            request.SortOrder,
            uid), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }

    // ─── Kısım / Birim (üçlü depo yapısı — B-06) ────────────────────────────────

    /// <summary>Deponun kısımları + birimleri, stok özetleriyle.</summary>
    [HttpGet("warehouses/{id:guid}/sections")]
    public async Task<IActionResult> GetWarehouseSections(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new ECSPros.Inventory.Application.Queries.GetWarehouseSections.GetWarehouseSectionsQuery(id), ct);
        return Ok(new { success = true, data = r.Value });
    }

    /// <summary>Depoya kısım ekler.</summary>
    [HttpPost("warehouses/{id:guid}/sections")]
    public async Task<IActionResult> CreateWarehouseSection(Guid id, [FromBody] CreateSectionRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new ECSPros.Inventory.Application.Commands.ManageWarehouseSections.CreateWarehouseSectionCommand(
            id, req.Code, req.Name, req.IsSellableOnline, req.PickingOrder), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Created("", new { success = true, data = new { id = r.Value } });
    }

    /// <summary>Kısım günceller (satışa açıklık dahil).</summary>
    [HttpPut("sections/{id:guid}")]
    public async Task<IActionResult> UpdateWarehouseSection(Guid id, [FromBody] UpdateSectionRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new ECSPros.Inventory.Application.Commands.ManageWarehouseSections.UpdateWarehouseSectionCommand(
            id, req.Name, req.IsSellableOnline, req.IsActive, req.PickingOrder), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Ok(new { success = true });
    }

    /// <summary>Kısma birim/raf ekler.</summary>
    [HttpPost("sections/{id:guid}/bins")]
    public async Task<IActionResult> CreateWarehouseBin(Guid id, [FromBody] CreateBinRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new ECSPros.Inventory.Application.Commands.ManageWarehouseSections.CreateWarehouseBinCommand(
            id, req.Code, req.Barcode, req.Name), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Created("", new { success = true, data = new { id = r.Value } });
    }

    /// <summary>Birim/raf günceller.</summary>
    [HttpPut("bins/{id:guid}")]
    public async Task<IActionResult> UpdateWarehouseBin(Guid id, [FromBody] UpdateBinRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new ECSPros.Inventory.Application.Commands.ManageWarehouseSections.UpdateWarehouseBinCommand(
            id, req.Name, req.Barcode, req.IsActive), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Ok(new { success = true });
    }

    public record CreateSectionRequest(string Code, string Name, bool IsSellableOnline = true, int PickingOrder = 0);
    public record UpdateSectionRequest(string Name, bool IsSellableOnline, bool IsActive, int PickingOrder);
    public record CreateBinRequest(string Code, string Barcode, string? Name);
    public record UpdateBinRequest(string? Name, string Barcode, bool IsActive);

    /// <summary>Stok bilgilerini listeler.</summary>
    [HttpGet("stocks")]
    public async Task<IActionResult> GetStocks(
        [FromQuery] Guid? warehouseId,
        [FromQuery] Guid? variantId,
        [FromQuery] bool availableOnly = false,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetStocksQuery(warehouseId, variantId, availableOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Admin stok listesi: sayfalı + ürün/depo/kısım/raf bilgisiyle zenginleştirilmiş
    /// (eski /stocks 165K satırı sayfasız döndürüyordu — admin sayfası artık bunu kullanır).</summary>
    [HttpGet("stocks/admin-list")]
    public async Task<IActionResult> GetStocksAdmin(
        [FromQuery] string? search,
        [FromQuery] Guid? warehouseId,
        [FromQuery] bool availableOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        [FromQuery] Guid? variantId = null,
        [FromQuery] Guid? sectionId = null,
        [FromQuery] Guid? binId = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ECSPros.Api.Handlers.GetStocksAdminQuery(
            search, warehouseId, availableOnly, page, pageSize, variantId, sectionId, binId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Arama sonucundan türetilen ikincil filtre seçenekleri: bulunan ürünün
    /// varyantları + stoğun bulunduğu depo/kısım/raflar (sayaçlı).</summary>
    [HttpGet("stocks/admin-list/facets")]
    public async Task<IActionResult> GetStocksAdminFacets(
        [FromQuery] string? search,
        [FromQuery] Guid? warehouseId,
        [FromQuery] bool availableOnly = false,
        [FromQuery] Guid? variantId = null,
        [FromQuery] Guid? sectionId = null,
        [FromQuery] Guid? binId = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ECSPros.Api.Handlers.GetStocksAdminFacetsQuery(
            search, warehouseId, availableOnly, variantId, sectionId, binId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Stok hareketi (giriş/çıkış/düzeltme) kaydeder.</summary>
    [HttpPost("stocks/adjust")]
    public async Task<IActionResult> AdjustStock([FromBody] AdjustStockRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        Guid.TryParse(userId, out var uid);

        var result = await _mediator.Send(new AdjustStockCommand(
            request.VariantId,
            request.WarehouseId,
            request.QuantityDelta,
            request.MovementType,
            request.Notes,
            uid == Guid.Empty ? null : uid), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        ECSPros.Api.Services.StockCacheInvalidation.Bust(_cacheBust);
        return Ok(new { success = true, data = new { movementId = result.Value } });
    }

    // ─── Warehouse Locations ───────────────────────────────────────────────────

    /// <summary>Depo lokasyonlarını listeler.</summary>
    [HttpGet("warehouses/{id:guid}/locations")]
    public async Task<IActionResult> GetWarehouseLocations(
        Guid id, [FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetWarehouseLocationsQuery(id, activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Depo lokasyonu oluşturur.</summary>
    [HttpPost("warehouses/{id:guid}/locations")]
    public async Task<IActionResult> CreateWarehouseLocation(
        Guid id, [FromBody] CreateWarehouseLocationRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateWarehouseLocationCommand(
            id, request.Code, request.Barcode, request.Name, request.ParentId,
            request.LocationType, request.ReservePriority, request.PickingOrder, request.SortOrder), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/inventory/warehouses/{id}/locations", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Kod aralığındaki lokasyonları toplu siler (dolu lokasyon varsa engeller).</summary>
    [HttpDelete("warehouses/{id:guid}/locations/bulk")]
    public async Task<IActionResult> BulkDeleteLocations(
        Guid id, [FromBody] BulkDeleteLocationsRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new BulkDeleteLocationsCommand(id, request.StartCode, request.EndCode), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Depo lokasyonunu günceller.</summary>
    [HttpPut("locations/{id:guid}")]
    public async Task<IActionResult> UpdateWarehouseLocation(
        Guid id, [FromBody] UpdateWarehouseLocationRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateWarehouseLocationCommand(
            id, request.Name, request.LocationType, request.ReservePriority,
            request.PickingOrder, request.SortOrder, request.IsActive), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }

    // ─── Reservations ──────────────────────────────────────────────────────────

    /// <summary>Stok rezervasyonlarını listeler.</summary>
    [HttpGet("reservations")]
    public async Task<IActionResult> GetReservations(
        [FromQuery] Guid? warehouseId,
        [FromQuery] Guid? variantId,
        [FromQuery] string? referenceType,
        [FromQuery] Guid? referenceId,
        [FromQuery] string? status,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetReservationsQuery(warehouseId, variantId, referenceType, referenceId, status), ct);
        return Ok(new { success = true, data = result.Value });
    }

    // ─── Transfers ─────────────────────────────────────────────────────────────

    /// <summary>Transfer taleplerini listeler.</summary>
    [HttpGet("transfers")]
    public async Task<IActionResult> GetTransfers(
        [FromQuery] Guid? fromWarehouseId,
        [FromQuery] Guid? toWarehouseId,
        [FromQuery] string? status,
        [FromQuery] string? transferType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetTransfersQuery(fromWarehouseId, toWarehouseId, status, transferType, page, pageSize), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Transfer talebi detayı.</summary>
    [HttpGet("transfers/{id:guid}")]
    public async Task<IActionResult> GetTransferDetail(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTransferDetailQuery(id), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni transfer talebi oluşturur.</summary>
    [HttpPost("transfers")]
    public async Task<IActionResult> CreateTransfer([FromBody] CreateTransferRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        Guid.TryParse(userId, out var uid);

        var items = (request.Items ?? []).Select(i => new CreateTransferItemDto(
            i.VariantId, i.RequestedQuantity, i.FromLocationId, i.ToLocationId)).ToList();

        var result = await _mediator.Send(new CreateTransferCommand(
            request.FromWarehouseId, request.ToWarehouseId, request.TransferType,
            request.Notes, uid, items), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created("/api/inventory/transfers", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Transfer talebine kalem ekler (sadece draft durumunda).</summary>
    [HttpPost("transfers/{id:guid}/items")]
    public async Task<IActionResult> AddTransferItem(Guid id, [FromBody] AddTransferItemRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddTransferItemCommand(
            id, request.VariantId, request.RequestedQuantity, request.FromLocationId, request.ToLocationId), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Transfer durumunu günceller (draft→pending→picking→picked→in_transit→delivered→completed veya cancelled).</summary>
    [HttpPatch("transfers/{id:guid}/status")]
    public async Task<IActionResult> UpdateTransferStatus(
        Guid id, [FromBody] UpdateTransferStatusRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateTransferStatusCommand(id, request.Status, request.Notes), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }
}

public record CreateWarehouseRequest(
    string Code,
    Dictionary<string, string> NameI18n,
    string? WarehouseType,
    string? Address,
    bool IsSellableOnline = true,
    int ReservePriority = 0,
    int SortOrder = 0);

public record UpdateWarehouseRequest(
    Dictionary<string, string> NameI18n,
    string WarehouseType,
    string? Address,
    bool IsSellableOnline,
    int ReservePriority,
    bool IsActive,
    int SortOrder);

public record AdjustStockRequest(
    Guid VariantId,
    Guid WarehouseId,
    int QuantityDelta,
    string MovementType,
    string? Notes);

public record CreateWarehouseLocationRequest(
    string Code,
    string Barcode,
    string? Name,
    Guid? ParentId,
    string LocationType = "bin",
    int ReservePriority = 0,
    int PickingOrder = 0,
    int SortOrder = 0);

public record UpdateWarehouseLocationRequest(
    string? Name,
    string LocationType,
    int ReservePriority,
    int PickingOrder,
    int SortOrder,
    bool IsActive);

public record CreateTransferRequest(
    Guid FromWarehouseId,
    Guid ToWarehouseId,
    string TransferType,
    string? Notes,
    List<TransferItemRequest>? Items);

public record AddTransferItemRequest(
    Guid VariantId,
    int RequestedQuantity,
    Guid? FromLocationId,
    Guid? ToLocationId);

public record TransferItemRequest(
    Guid VariantId,
    int RequestedQuantity,
    Guid? FromLocationId,
    Guid? ToLocationId);

public record UpdateTransferStatusRequest(string Status, string? Notes);

public record BulkDeleteLocationsRequest(string StartCode, string EndCode);
