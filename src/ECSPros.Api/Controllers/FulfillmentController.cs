using ECSPros.Fulfillment.Application.Commands.CompletePickingPlan;
using ECSPros.Fulfillment.Application.Commands.ScanItem;
using ECSPros.Fulfillment.Application.Commands.ScanToBin;
using ECSPros.Fulfillment.Application.Queries.GetFulfillmentDashboard;
using ECSPros.Fulfillment.Application.Commands.PrintPackageLabel;
using ECSPros.Fulfillment.Application.Commands.UpdateBinStatus;
using ECSPros.Fulfillment.Application.Queries.GetPickingPlanDetail;
using ECSPros.Fulfillment.Application.Commands.CreatePackage;
using ECSPros.Fulfillment.Application.Commands.AssignCargoCode;
using ECSPros.Fulfillment.Application.Commands.ManagePackageNumberSeries;
using ECSPros.Fulfillment.Application.Commands.MergePackages;
using ECSPros.Fulfillment.Application.Commands.RenumberPackage;
using ECSPros.Fulfillment.Application.Commands.SplitOrderIntoPackages;
using ECSPros.Fulfillment.Application.Commands.UpdatePackage;
using ECSPros.Fulfillment.Application.Queries.GetPackageCodeHistory;
using ECSPros.Fulfillment.Application.Commands.CreatePackingStation;
using ECSPros.Fulfillment.Application.Commands.CreatePickingPlan;
using ECSPros.Fulfillment.Application.Commands.StartPickingPlan;
using ECSPros.Fulfillment.Application.Commands.UpdatePackingStation;
using ECSPros.Fulfillment.Application.Queries.GetPackages;
using ECSPros.Fulfillment.Application.Queries.GetPackingStations;
using ECSPros.Fulfillment.Application.Queries.GetPickingPlans;
using ECSPros.Api.Authorization;
using ECSPros.Shared.Kernel.Authorization;
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
            request.Barcode,
            request.Weight,
            request.Width,
            request.Height,
            request.Length,
            request.Desi,
            uid,
            request.SupplierId,
            request.Items?.Select(i => new CreatePackageItem(i.OrderItemId, i.VariantId, i.Quantity)).ToList()), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/fulfillment/packages", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Siparişi tedarikçiye göre paketlere böler (F2 — karar 2026-07-19).</summary>
    [HttpPost("packages/split")]
    public async Task<IActionResult> SplitOrderIntoPackages([FromBody] SplitOrderPackagesRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid))
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new SplitOrderIntoPackagesCommand(request.OrderId, uid), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/fulfillment/packages", new { success = true, data = new { packageIds = result.Value } });
    }

    // ── Paket Numarası Serileri (F5 — kanal başına, siparişten bağımsız) ───────

    /// <summary>Kanal başına paket numarası serilerini listeler.</summary>
    [HttpGet("package-number-series")]
    public async Task<IActionResult> GetPackageNumberSeries(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPackageNumberSeriesQuery(), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kanalın paket numarası serisini tanımlar/günceller — sayaç elle
    /// değiştirilemez, numaralar havuza geri dönmez.</summary>
    [HttpPut("package-number-series/{firmPlatformId:guid}")]
    public async Task<IActionResult> UpsertPackageNumberSeries(
        Guid firmPlatformId, [FromBody] UpsertPackageSeriesRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpsertPackageNumberSeriesCommand(
            firmPlatformId, request.Prefix, request.PadLength, request.IsActive), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Paketin fiziksel bilgilerini günceller (F4) — kimlik alanları
    /// (paket no / kargo kodu) renumber ve cargo-code akışlarından geçer.</summary>
    [HttpPut("packages/{packageId:guid}")]
    public async Task<IActionResult> UpdatePackage(Guid packageId, [FromBody] UpdatePackageRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid))
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new UpdatePackageCommand(
            packageId, request.Weight, request.Width, request.Height, request.Length,
            request.Desi, request.Barcode, uid), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Pakete seriden yeni numara verir (F4): gerekçe zorunlu, eski numara
    /// geçmişe yazılır ve havuza geri dönmez; bağlı kargo kodu temizlenir.</summary>
    [HttpPost("packages/{packageId:guid}/renumber")]
    public async Task<IActionResult> RenumberPackage(Guid packageId, [FromBody] RenumberPackageRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid))
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new RenumberPackageCommand(packageId, request.Reason, uid), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { packageNumber = result.Value } });
    }

    /// <summary>Paketin kod değişiklik izini listeler (F4).</summary>
    [HttpGet("packages/{packageId:guid}/code-history")]
    public async Task<IActionResult> GetPackageCodeHistory(Guid packageId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPackageCodeHistoryQuery(packageId), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Pakete kargo entegrasyon kodu atar (F3): externalCode verilirse aynen
    /// yazılır; verilmezse seçilen kargo entegrasyonunun stratejisine göre üretilir.</summary>
    [HttpPost("packages/{packageId:guid}/cargo-code")]
    public async Task<IActionResult> AssignCargoCode(Guid packageId, [FromBody] AssignCargoCodeRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid))
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new AssignCargoCodeCommand(
            packageId, request.FirmPlatformIntegrationId, request.ExternalCode, uid, request.Reason), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { cargoIntegrationCode = result.Value } });
    }

    /// <summary>Paket birleştirme — İSTİSNA akışı: ayrı izin + zorunlu gerekçe
    /// (normal akış paket başına fatura/kargodur, karar 2026-07-19).</summary>
    [HttpPost("packages/merge")]
    [RequirePermission(Permissions.OrderPackagesMerge)]
    public async Task<IActionResult> MergePackages([FromBody] MergePackagesRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid))
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new MergePackagesCommand(request.PackageIds, request.Reason, uid), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { packageId = result.Value } });
    }

    // ─── Scan Operations ───────────────────────────────────────────────────────

    /// <summary>Toplama planında ürün tarar — uygun kutya atar.</summary>
    [HttpPost("picking/{planId:guid}/scan-item")]
    public async Task<IActionResult> ScanItem(Guid planId, [FromBody] ScanItemRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid))
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new ScanItemCommand(planId, request.Barcode, uid), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Ürünü belirtilen kutya tarar.</summary>
    [HttpPost("sorting/bins/{binId:guid}/scan")]
    public async Task<IActionResult> ScanToBin(Guid binId, [FromBody] ScanItemRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid))
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new ScanToBinCommand(binId, request.Barcode, uid), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ─── Dashboard ─────────────────────────────────────────────────────────────

    /// <summary>Fulfillment operasyon özet dashboard.</summary>
    [HttpGet("dashboard/summary")]
    public async Task<IActionResult> GetDashboardSummary(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetFulfillmentDashboardQuery(), ct);
        return Ok(new { success = true, data = result.Value });
    }
}

public record UpdateBinStatusRequest(string Status);
public record ScanItemRequest(string Barcode);

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
    string? Barcode,
    decimal? Weight,
    decimal? Width,
    decimal? Height,
    decimal? Length,
    decimal? Desi,
    Guid? SupplierId = null,
    List<CreatePackageItemRequest>? Items = null);

public record CreatePackageItemRequest(Guid OrderItemId, Guid VariantId, int Quantity);

public record SplitOrderPackagesRequest(Guid OrderId);

public record MergePackagesRequest(List<Guid> PackageIds, string Reason);

public record AssignCargoCodeRequest(
    Guid? FirmPlatformIntegrationId = null,
    string? ExternalCode = null,
    string? Reason = null);

public record UpdatePackageRequest(
    decimal? Weight = null,
    decimal? Width = null,
    decimal? Height = null,
    decimal? Length = null,
    decimal? Desi = null,
    string? Barcode = null);

public record RenumberPackageRequest(string Reason);

public record UpsertPackageSeriesRequest(string Prefix, int PadLength, bool IsActive = true);
