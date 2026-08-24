using ECSPros.Api.Authorization;
using ECSPros.Procurement.Application.Commands.CreatePurchaseOrder;
using ECSPros.Procurement.Application.Commands.DeletePurchaseOrderItem;
using ECSPros.Procurement.Application.Commands.SetPurchaseOrderStatus;
using ECSPros.Procurement.Application.Commands.UpdatePurchaseOrder;
using ECSPros.Procurement.Application.Commands.UpsertPurchaseOrderItems;
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
}

public record CreatePurchaseOrderRequest(Guid SupplierId, DateTime? OrderDate, DateTime? ExpectedDate, string? Notes);
public record UpdatePurchaseOrderRequest(DateTime? OrderDate, DateTime? ExpectedDate, string? Notes);
public record SetPurchaseOrderStatusRequest(string Status);
public record UpsertPurchaseOrderItemsRequest(List<PurchaseOrderItemInput>? Items);
