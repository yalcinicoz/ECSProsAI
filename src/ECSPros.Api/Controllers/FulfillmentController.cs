using ECSPros.Fulfillment.Application.Commands.CompletePickingPlan;
using ECSPros.Fulfillment.Application.Commands.PrintPackageLabel;
using ECSPros.Fulfillment.Application.Commands.UpdateBinStatus;
using ECSPros.Fulfillment.Application.Queries.GetPickingPlanDetail;
using ECSPros.Fulfillment.Application.Commands.CreatePackage;
using ECSPros.Fulfillment.Application.Commands.CreatePackingStation;
using ECSPros.Fulfillment.Application.Commands.CreatePickingPlan;
using ECSPros.Fulfillment.Application.Commands.StartPickingPlan;
using ECSPros.Fulfillment.Application.Commands.UpdatePackingStation;
using ECSPros.Fulfillment.Application.Queries.GetPackages;
using ECSPros.Fulfillment.Application.Queries.GetPackingStations;
using ECSPros.Fulfillment.Application.Queries.GetPickingPlans;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/fulfillment")]
[Authorize]
public class FulfillmentController : ControllerBase
{
    private readonly IMediator _mediator;

    public FulfillmentController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Toplama planlarını listeler.</summary>
    [HttpGet("picking-plans")]
    public async Task<IActionResult> GetPickingPlans(
        [FromQuery] string? status,
        [FromQuery] Guid? warehouseId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPickingPlansQuery(status, warehouseId, page, pageSize), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni toplama planı oluşturur.</summary>
    [HttpPost("picking-plans")]
    public async Task<IActionResult> CreatePickingPlan([FromBody] CreatePickingPlanRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid))
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new CreatePickingPlanCommand(
            request.WarehouseId,
            request.PlanType,
            request.OrderIds,
            uid), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/fulfillment/picking-plans", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Toplama planı detayını döner.</summary>
    [HttpGet("picking-plans/{id:guid}")]
    public async Task<IActionResult> GetPickingPlanDetail(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPickingPlanDetailQuery(id), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Toplama planını başlatır (pending → picking).</summary>
    [HttpPost("picking-plans/{id:guid}/start")]
    public async Task<IActionResult> StartPickingPlan(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid))
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new StartPickingPlanCommand(id, uid), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }

    /// <summary>Toplama planını tamamlar (picking → completed). Siparişler paketleme kuyruğuna girer.</summary>
    [HttpPost("picking-plans/{id:guid}/complete")]
    public async Task<IActionResult> CompletePickingPlan(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid))
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new CompletePickingPlanCommand(id, uid), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }

    /// <summary>Paketleme istasyonlarını listeler.</summary>
    [HttpGet("packing-stations")]
    public async Task<IActionResult> GetPackingStations(
        [FromQuery] Guid? warehouseId,
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPackingStationsQuery(warehouseId, activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni paketleme istasyonu oluşturur.</summary>
    [HttpPost("packing-stations")]
    public async Task<IActionResult> CreatePackingStation([FromBody] CreatePackingStationRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreatePackingStationCommand(
            request.WarehouseId,
            request.StationCode,
            request.Barcode,
            request.StationName,
            request.SlotCount,
            request.IsObm), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/fulfillment/packing-stations", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Paketleme istasyonu günceller.</summary>
    [HttpPut("packing-stations/{id:guid}")]
    public async Task<IActionResult> UpdatePackingStation(Guid id, [FromBody] UpdatePackingStationRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid))
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new UpdatePackingStationCommand(
            id,
            request.StationName,
            request.SlotCount,
            request.IsObm,
            request.AssignedTo,
            request.Status,
            uid), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }

    /// <summary>Sorting bin durumunu günceller (empty → filling → ready).</summary>
    [HttpPatch("bins/{binId:guid}/status")]
    public async Task<IActionResult> UpdateBinStatus(Guid binId, [FromBody] UpdateBinStatusRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid))
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new UpdateBinStatusCommand(binId, request.Status, uid), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Paketleri listeler.</summary>
    [HttpGet("packages")]
    public async Task<IActionResult> GetPackages(
        [FromQuery] Guid? orderId,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPackagesQuery(orderId), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Paket etiketi basıldı olarak işaretler.</summary>
    [HttpPost("packages/{packageId:guid}/print-label")]
    public async Task<IActionResult> PrintPackageLabel(Guid packageId, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid))
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new PrintPackageLabelCommand(packageId, uid), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Yeni paket oluşturur.</summary>
    [HttpPost("packages")]
    public async Task<IActionResult> CreatePackage([FromBody] CreatePackageRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid))
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new CreatePackageCommand(
            request.OrderId,
            request.ShipmentId,
            request.PackageNumber,
            request.Barcode,
            request.Weight,
            request.Width,
            request.Height,
            request.Length,
            request.Desi,
            uid), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/fulfillment/packages", new { success = true, data = new { id = result.Value } });
    }
}

public record UpdateBinStatusRequest(string Status);

public record UpdatePackingStationRequest(
    string? StationName,
    int SlotCount,
    bool IsObm,
    Guid? AssignedTo,
    string Status);

public record CreatePickingPlanRequest(
    Guid WarehouseId,
    string PlanType,
    List<Guid> OrderIds);

public record CreatePackingStationRequest(
    Guid WarehouseId,
    string StationCode,
    string Barcode,
    string? StationName,
    int SlotCount,
    bool IsObm);

public record CreatePackageRequest(
    Guid OrderId,
    Guid? ShipmentId,
    int PackageNumber,
    string Barcode,
    decimal? Weight,
    decimal? Width,
    decimal? Height,
    decimal? Length,
    decimal? Desi);
