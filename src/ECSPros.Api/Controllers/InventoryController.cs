using ECSPros.Shared.Kernel.Grid;
using ECSPros.Shared.Kernel.Authorization;
using ECSPros.Api.Authorization;
using ECSPros.Inventory.Application.Commands.AddTransferItem;
using ECSPros.Inventory.Application.Commands.AdjustStock;
using ECSPros.Inventory.Application.Commands.BulkDeleteLocations;
using ECSPros.Inventory.Application.Commands.CreateTransfer;
using ECSPros.Inventory.Application.Commands.CreateWarehouse;
using ECSPros.Inventory.Application.Commands.CreateWarehouseLocation;
using ECSPros.Inventory.Application.Commands.UpdateTransferStatus;
using ECSPros.Inventory.Application.Commands.UpdateWarehouse;
using ECSPros.Inventory.Application.Commands.UpdateWarehouseLocation;
using ECSPros.Inventory.Application.Shelf;
using ECSPros.Api.Services.Inventory;
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
[RequirePermission(Permissions.InventoryView)]   // Y2: sayfa yetkisi
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
    /// <summary>Depoları TAM liste olarak döner — dropdown kaynağı. ⚠ Sayfalanmaz: stok, transfer,
    /// paketleme, toplama görevi, iade detayı ve satın alma ekranları bunu bütün hâlinde bekler.
    /// Liste EKRANI için sayfalı /warehouses/grid ucunu kullanın.</summary>
    [HttpGet("warehouses")]
    public async Task<IActionResult> GetWarehouses([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetWarehousesQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Depolar liste ekranı (DataGrid): sayfalı + f.* filtreleri + sort/dir.</summary>
    [HttpGet("warehouses/grid")]
    public async Task<IActionResult> GetWarehousesGrid([FromQuery] bool activeOnly = false, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        // Depo kanaldan bağımsız bir tanım kaydıdır → kanal kısıtı null.
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, null, defaultPageSize: 20);
        var result = await _mediator.Send(new GetWarehousesGridQuery(
            new WarehouseListFilters(activeOnly, search), grid.Page, grid.PageSize, grid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Depoları Excel'e aktarır (DataGrid): gövde search/sort/dir/filters/columns + named: activeOnly.</summary>
    [HttpPost("warehouses/export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> ExportWarehouses(
        [FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, [FromBody] GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<InventoryController> logger, CancellationToken ct)
    {
        var filters = new WarehouseListFilters(body.NamedValue("activeOnly") == "true", body.Search);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "warehouses", "depolar", "Depolar",
            ECSPros.Api.Grid.WarehouseExportColumns.All,
            max => _mediator.Send(new ExportWarehousesQuery(filters, body.ToGridRequest(null), max), ct),
            ct, alanIzinleri: await alanYetkileri.IzinlerAsync(ct));
    }

    /// <summary>Yeni depo oluşturur.</summary>
    [HttpPost("warehouses")]
    [RequirePermission(Permissions.InventoryManage)]   // Y2
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
    [RequirePermission(Permissions.InventoryManage)]   // Y2
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
    [RequirePermission(Permissions.InventoryManage)]   // Y2
    public async Task<IActionResult> CreateWarehouseSection(Guid id, [FromBody] CreateSectionRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new ECSPros.Inventory.Application.Commands.ManageWarehouseSections.CreateWarehouseSectionCommand(
            id, req.Code, req.Name, req.IsSellableOnline, req.PickingOrder), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Created("", new { success = true, data = new { id = r.Value } });
    }

    /// <summary>Kısım günceller (satışa açıklık dahil).</summary>
    [HttpPut("sections/{id:guid}")]
    [RequirePermission(Permissions.InventoryManage)]   // Y2
    public async Task<IActionResult> UpdateWarehouseSection(Guid id, [FromBody] UpdateSectionRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new ECSPros.Inventory.Application.Commands.ManageWarehouseSections.UpdateWarehouseSectionCommand(
            id, req.Name, req.IsSellableOnline, req.IsActive, req.PickingOrder), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Ok(new { success = true });
    }

    /// <summary>Kısma birim/raf ekler.</summary>
    [HttpPost("sections/{id:guid}/bins")]
    [RequirePermission(Permissions.InventoryManage)]   // Y2
    public async Task<IActionResult> CreateWarehouseBin(Guid id, [FromBody] CreateBinRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new ECSPros.Inventory.Application.Commands.ManageWarehouseSections.CreateWarehouseBinCommand(
            id, req.Code, req.Barcode, req.Name), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Created("", new { success = true, data = new { id = r.Value } });
    }

    /// <summary>Birim/raf günceller.</summary>
    [HttpPut("bins/{id:guid}")]
    [RequirePermission(Permissions.InventoryManage)]   // Y2
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
        // DataGrid F4 (2026-09-08): sort/dir + f.* (StockGrid.Schema beyaz listesi); page/pageSize merkezi clamp
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, null /* stok kanaldan bağımsızdır */, defaultPageSize: 30);
        var result = await _mediator.Send(new ECSPros.Api.Handlers.GetStocksAdminQuery(
            search, warehouseId, availableOnly, grid.Page, grid.PageSize, variantId, sectionId, binId, grid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Stok listesini Excel'e aktarır (DataGrid F4): gövdede aynı filtre modeli (search/sort/dir/filters + named: warehouseId,
    /// availableOnly, variantId, sectionId, binId) + kolon listesi. Ürün/depo adları satır satır zenginleştirilir (parti 500).</summary>
    [HttpPost("stocks/admin-list/export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> ExportStocksAdmin([FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, [FromBody] ECSPros.Shared.Kernel.Grid.GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<InventoryController> logger, CancellationToken ct)
    {
        var K = ECSPros.Api.Grid.GridExportEndpoint.Kimlik;
        var filters = new ECSPros.Api.Grid.StockListFilters(body.Search, K(body, "warehouseId"),
            ECSPros.Api.Grid.GridExportEndpoint.Bayrak(body, "availableOnly") ?? false, K(body, "variantId"), K(body, "sectionId"), K(body, "binId"));
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "stocks", "stok", "Stok",
            ECSPros.Api.Grid.StockExportColumns.All, max => _mediator.Send(new ECSPros.Api.Grid.ExportStocksQuery(filters, body.ToGridRequest(null /* stok kanaldan bağımsızdır */), max), ct), ct, alanIzinleri: await alanYetkileri.IzinlerAsync(ct));
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
            search, warehouseId, availableOnly, variantId, sectionId, binId, ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, null /* stok kanaldan bağımsızdır */)), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Stok hareketi (giriş/çıkış/düzeltme) kaydeder.</summary>
    [HttpPost("stocks/adjust")]
    [RequirePermission(Permissions.InventoryManage)]   // Y2
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
    [RequirePermission(Permissions.InventoryManage)]   // Y2
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
    [RequirePermission(Permissions.InventoryManage)]   // Y2
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
    [RequirePermission(Permissions.InventoryManage)]   // Y2
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

    /// <summary>Transfer taleplerini listeler (DataGrid: f.* filtreleri + sort/dir + arama).</summary>
    [HttpGet("transfers")]
    public async Task<IActionResult> GetTransfers(
        [FromQuery] Guid? fromWarehouseId,
        [FromQuery] Guid? toWarehouseId,
        [FromQuery] string? status,
        [FromQuery] string? transferType,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        // Depo transferi kanaldan bağımsızdır (iç depo hareketi) → kanal kısıtı null.
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, null, defaultPageSize: 20);
        var result = await _mediator.Send(new GetTransfersQuery(
            fromWarehouseId, toWarehouseId, status, transferType, grid.Page, grid.PageSize, search, grid), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Transferleri Excel'e aktarır (DataGrid): gövde search/sort/dir/filters/columns + named: status/transferType.</summary>
    [HttpPost("transfers/export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> ExportTransfers(
        [FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, [FromBody] GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<InventoryController> logger, CancellationToken ct)
    {
        var filters = new TransferListFilters(
            Status: body.NamedValue("status"), TransferType: body.NamedValue("transferType"), Search: body.Search);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "transfers", "transferler", "Transferler",
            ECSPros.Api.Grid.TransferExportColumns.All,
            max => _mediator.Send(new ExportTransfersQuery(filters, body.ToGridRequest(null), max), ct),
            ct, alanIzinleri: await alanYetkileri.IzinlerAsync(ct));
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
    [RequirePermission(Permissions.InventoryManage)]   // Y2
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
    [RequirePermission(Permissions.InventoryManage)]   // Y2
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
    [RequirePermission(Permissions.InventoryManage)]   // Y2
    public async Task<IActionResult> UpdateTransferStatus(
        Guid id, [FromBody] UpdateTransferStatusRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateTransferStatusCommand(id, request.Status, request.Notes), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ─── FAZ 15.3 Raf / göz operasyonları (docs/raf-operasyon-ekranlari-plani.md) ───────────────

    /// <summary>Stok otoritesi: legacy (aynalama — raf yazma uçları 409) | panel.</summary>
    [HttpGet("stock-authority")]
    public IActionResult GetStockAuthority([FromServices] StockAuthority authority)
        => Ok(new { success = true, data = new { authority = authority.Current, legacyOwnsStock = authority.LegacyOwnsStock, message = authority.LegacyOwnsStock ? StockAuthority.LegacyMessage : null } });

    private static IActionResult? OtoriteKontrol(StockAuthority authority)
        => authority.LegacyOwnsStock ? new ConflictObjectResult(new { success = false, error = StockAuthority.LegacyMessage, code = "stock_authority_legacy" }) : null;

    private Guid? KullaniciId()
    {
        var v = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(v, out var g) ? g : null;
    }

    /// <summary>Tek okutma kutusu: barkod bir GÖZ mü, ÜRÜN mü? Önce göz (inv_warehouse_bins.Barcode), sonra varyant barkodu.</summary>
    [HttpGet("shelf/resolve")]
    public async Task<IActionResult> ResolveScan([FromQuery] string barcode, [FromServices] ECSPros.Shared.Contracts.IProductService products, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return BadRequest(new { success = false, error = "Barkod boş." });
        var bin = await _mediator.Send(new GetBinContentsQuery(barcode.Trim(), null), ct);
        if (bin.IsSuccess) return Ok(new { success = true, data = new { kind = "bin", bin = bin.Value } });

        var v = await _mediator.Send(new ECSPros.Catalog.Application.Queries.GetVariantByBarcode.GetVariantByBarcodeQuery(barcode.Trim()), ct);
        if (v.IsFailure || v.Value is null) return Ok(new { success = true, data = new { kind = "none" } });
        ECSPros.Shared.Contracts.VariantDisplayInfo? g = null;
        try { g = (await products.GetVariantDisplayAsync(new[] { v.Value.VariantId }, ct)).GetValueOrDefault(v.Value.VariantId); } catch { /* isteğe bağlı */ }
        var bins = await _mediator.Send(new GetVariantBinsQuery(v.Value.VariantId, null), ct);
        return Ok(new { success = true, data = new { kind = "variant", variant = new {
            v.Value.VariantId, v.Value.ProductId, v.Value.ProductCode, v.Value.Sku,
            productName = g?.ProductNameI18n.GetValueOrDefault("tr") ?? v.Value.ProductNameI18n.GetValueOrDefault("tr") ?? v.Value.ProductCode,
            g?.OptionsText, g?.ImageUrl }, bins = bins.Value } });
    }

    /// <summary>R1 Raf içeriği (göz barkodu).</summary>
    [HttpGet("shelf/bins/{barcode}/contents")]
    public async Task<IActionResult> GetBinContents(string barcode, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetBinContentsQuery(barcode, null), ct);
        if (r.IsFailure) return NotFound(new { success = false, error = r.Error });
        return Ok(new { success = true, data = r.Value });
    }

    /// <summary>R1 Ürün hangi gözlerde.</summary>
    [HttpGet("shelf/variants/{variantId:guid}/bins")]
    public async Task<IActionResult> GetVariantBins(Guid variantId, [FromQuery] Guid? warehouseId, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetVariantBinsQuery(variantId, warehouseId), ct);
        return Ok(new { success = true, data = r.Value });
    }

    /// <summary>R2 Göze yerleştir (free: serbest giriş, not zorunlu; unbinned: rafsız satırdan).</summary>
    [HttpPost("shelf/place")]
    [RequirePermission(Permissions.InventoryManage)]
    public async Task<IActionResult> PlaceToBin([FromBody] ShelfPlaceRequest req, [FromServices] StockAuthority authority, CancellationToken ct)
    {
        if (OtoriteKontrol(authority) is { } engel) return engel;
        var r = await _mediator.Send(new PlaceToBinCommand(req.BinId, req.VariantId, req.Quantity, req.Source ?? PlaceSources.Free, req.Notes, KullaniciId()), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Ok(new { success = true, data = new { quantityInBin = r.Value } });
    }

    /// <summary>R3 Göz→göz taşıma (aynı depo).</summary>
    [HttpPost("shelf/move")]
    [RequirePermission(Permissions.InventoryManage)]
    public async Task<IActionResult> MoveBetweenBins([FromBody] ShelfMoveRequest req, [FromServices] StockAuthority authority, CancellationToken ct)
    {
        if (OtoriteKontrol(authority) is { } engel) return engel;
        var r = await _mediator.Send(new MoveBetweenBinsCommand(req.FromBinId, req.ToBinId, req.MoveAll,
            req.Items?.Select(i => new MoveItem(i.VariantId, i.Quantity)).ToList(), req.Notes, KullaniciId()), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Ok(new { success = true, data = new { moved = r.Value } });
    }

    /// <summary>R4 İadeden rafa (kapalı kısım gözü → satış rafı / defo).</summary>
    [HttpPost("shelf/return-to-shelf")]
    [RequirePermission(Permissions.InventoryManage)]
    public async Task<IActionResult> ReturnToShelf([FromBody] ShelfReturnRequest req, [FromServices] StockAuthority authority, CancellationToken ct)
    {
        if (OtoriteKontrol(authority) is { } engel) return engel;
        var r = await _mediator.Send(new ReturnToShelfCommand(req.FromBinId, req.ToBinId, req.VariantId, req.Quantity, req.ReturnId, req.Notes, KullaniciId()), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Ok(new { success = true, data = new { moved = r.Value } });
    }

    /// <summary>R6 Mağaza reyon: depolar arası anlık tek ürün taşıma.</summary>
    [HttpPost("shelf/store-move")]
    [RequirePermission(Permissions.InventoryManage)]
    public async Task<IActionResult> StoreMove([FromBody] ShelfStoreMoveRequest req, [FromServices] StockAuthority authority, CancellationToken ct)
    {
        if (OtoriteKontrol(authority) is { } engel) return engel;
        var uid = KullaniciId(); if (uid is null) return Unauthorized(new { success = false, error = "Geçersiz token." });
        var r = await _mediator.Send(new StoreMoveCommand(req.FromWarehouseId, req.ToWarehouseId, req.VariantId, req.Quantity, req.Notes, uid.Value), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Ok(new { success = true, data = new { transferId = r.Value } });
    }

    // ── R5 Raf sayımı ──

    /// <summary>Sayım oturumları (DataGrid: search/sort/dir/f.* — BinCountGrid.Schema).</summary>
    [HttpGet("bin-counts")]
    public async Task<IActionResult> GetBinCounts([FromQuery] string? search, CancellationToken ct)
    {
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, null, defaultPageSize: 20);
        var r = await _mediator.Send(new GetBinCountsQuery(grid.Page, grid.PageSize, search, grid), ct);
        return Ok(new { success = true, data = r.Value });
    }

    /// <summary>Sayımları Excel'e aktarır (DataGrid F4).</summary>
    [HttpPost("bin-counts/export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> ExportBinCounts([FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, [FromBody] GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<InventoryController> logger, CancellationToken ct)
        => await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "bin-counts", "raf-sayimlari", "Raf Sayımları",
            ECSPros.Api.Grid.BinCountExportColumns.All, max => _mediator.Send(new ExportBinCountsQuery(body.Search, body.ToGridRequest(null), max), ct), ct,
            alanIzinleri: await alanYetkileri.IzinlerAsync(ct));

    [HttpGet("bin-counts/{id:guid}")]
    public async Task<IActionResult> GetBinCount(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetBinCountDetailQuery(id), ct);
        if (r.IsFailure) return NotFound(new { success = false, error = r.Error });
        return Ok(new { success = true, data = r.Value });
    }

    /// <summary>Sayım başlat (açık oturum varsa onu döner). Aynalama kipinde de çalışır — rapor üretir.</summary>
    [HttpPost("bin-counts/start")]
    [RequirePermission(Permissions.InventoryManage)]
    public async Task<IActionResult> StartBinCount([FromBody] BinCountStartRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new StartBinCountCommand(req.BinId, KullaniciId()), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Ok(new { success = true, data = new { countId = r.Value } });
    }

    [HttpPost("bin-counts/{id:guid}/scan")]
    [RequirePermission(Permissions.InventoryManage)]
    public async Task<IActionResult> ScanBinCount(Guid id, [FromBody] BinCountScanRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new ScanBinCountCommand(id, req.VariantId, req.Delta), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Ok(new { success = true, data = r.Value });
    }

    [HttpPost("bin-counts/{id:guid}/finish")]
    [RequirePermission(Permissions.InventoryManage)]
    public async Task<IActionResult> FinishBinCount(Guid id, [FromBody] BinCountFinishRequest? req, CancellationToken ct)
    {
        var r = await _mediator.Send(new FinishBinCountCommand(id, req?.Notes), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Ok(new { success = true });
    }

    /// <summary>Farkı stoğa uygular — ayrı yetki (K3); otorite eskideyken 409.</summary>
    [HttpPost("bin-counts/{id:guid}/apply")]
    [RequirePermission(Permissions.InventoryCountApply)]
    public async Task<IActionResult> ApplyBinCount(Guid id, [FromServices] StockAuthority authority, CancellationToken ct)
    {
        if (OtoriteKontrol(authority) is { } engel) return engel;
        var uid = KullaniciId(); if (uid is null) return Unauthorized(new { success = false, error = "Geçersiz token." });
        var r = await _mediator.Send(new ApplyBinCountCommand(id, uid.Value), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Ok(new { success = true, data = new { appliedLines = r.Value } });
    }

    [HttpPost("bin-counts/{id:guid}/cancel")]
    [RequirePermission(Permissions.InventoryManage)]
    public async Task<IActionResult> CancelBinCount(Guid id, [FromBody] BinCountFinishRequest? req, CancellationToken ct)
    {
        var r = await _mediator.Send(new CancelBinCountCommand(id, req?.Notes), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Ok(new { success = true });
    }

    public record ShelfPlaceRequest(Guid BinId, Guid VariantId, int Quantity = 1, string? Source = null, string? Notes = null);
    public record ShelfMoveItemRequest(Guid VariantId, int? Quantity);
    public record ShelfMoveRequest(Guid FromBinId, Guid ToBinId, bool MoveAll = false, List<ShelfMoveItemRequest>? Items = null, string? Notes = null);
    public record ShelfReturnRequest(Guid FromBinId, Guid ToBinId, Guid VariantId, int Quantity = 1, Guid? ReturnId = null, string? Notes = null);
    public record ShelfStoreMoveRequest(Guid FromWarehouseId, Guid ToWarehouseId, Guid VariantId, int Quantity = 1, string? Notes = null);
    public record BinCountStartRequest(Guid BinId);
    public record BinCountScanRequest(Guid VariantId, int Delta = 1);
    public record BinCountFinishRequest(string? Notes);
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
