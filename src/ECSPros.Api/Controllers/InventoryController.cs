using ECSPros.Inventory.Application.Commands.AdjustStock;
using ECSPros.Inventory.Application.Commands.CreateWarehouse;
using ECSPros.Inventory.Application.Queries.GetStocks;
using ECSPros.Inventory.Application.Queries.GetWarehouses;
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

    public InventoryController(IMediator mediator)
    {
        _mediator = mediator;
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

        return Ok(new { success = true, data = new { movementId = result.Value } });
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

public record AdjustStockRequest(
    Guid VariantId,
    Guid WarehouseId,
    int QuantityDelta,
    string MovementType,
    string? Notes);
