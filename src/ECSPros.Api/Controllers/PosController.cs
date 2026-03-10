using ECSPros.Pos.Application.Commands.CloseSession;
using ECSPros.Pos.Application.Commands.CompleteSale;
using ECSPros.Pos.Application.Commands.OpenSession;
using ECSPros.Pos.Application.Commands.RefundSale;
using ECSPros.Pos.Application.Queries.GetPosSaleDetail;
using ECSPros.Pos.Application.Queries.GetPosSales;
using ECSPros.Pos.Application.Queries.GetPosRegisters;
using ECSPros.Pos.Application.Queries.GetSessionSummary;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/pos")]
[Authorize]
public class PosController : ControllerBase
{
    private readonly IMediator _mediator;

    public PosController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>POS kasalarını listeler.</summary>
    [HttpGet("registers")]
    public async Task<IActionResult> GetRegisters([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPosRegistersQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>POS oturumu açar.</summary>
    [HttpPost("sessions/open")]
    public async Task<IActionResult> OpenSession([FromBody] OpenSessionRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid))
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new OpenSessionCommand(
            request.RegisterId,
            uid,
            request.OpeningCash,
            request.Notes), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/pos/sessions/{result.Value}", new { success = true, data = new { sessionId = result.Value } });
    }

    /// <summary>POS satışını tamamlar — stok otomatik düşülür.</summary>
    [HttpPost("sales")]
    public async Task<IActionResult> CompleteSale([FromBody] CompleteSaleRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid))
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var command = new CompleteSaleCommand(
            request.SessionId,
            request.MemberId,
            request.Items.Select(i => new SaleItemDto(
                i.VariantId, i.Barcode, i.ProductName,
                i.Quantity, i.UnitPrice, i.DiscountAmount, i.TaxRate)).ToList(),
            request.Payments.Select(p => new SalePaymentDto(
                p.PaymentMethod, p.Amount, p.TenderedAmount, p.ChangeAmount)).ToList(),
            request.Notes,
            uid);

        var result = await _mediator.Send(command, ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/pos/sales/{result.Value}", new { success = true, data = new { saleId = result.Value } });
    }

    /// <summary>POS satışlarını listeler.</summary>
    [HttpGet("sales")]
    public async Task<IActionResult> GetSales(
        [FromQuery] Guid? sessionId,
        [FromQuery] Guid? registerId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetPosSalesQuery(sessionId, registerId, dateFrom, dateTo, status, page, pageSize), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>POS satış detayını döner.</summary>
    [HttpGet("sales/{saleId:guid}")]
    public async Task<IActionResult> GetSaleDetail(Guid saleId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPosSaleDetailQuery(saleId), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>POS satışını iade eder — stok otomatik geri yüklenir.</summary>
    [HttpPost("sales/{saleId:guid}/refund")]
    public async Task<IActionResult> RefundSale(Guid saleId, [FromBody] RefundSaleRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid))
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new RefundSaleCommand(saleId, request.Reason, uid), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }

    /// <summary>Oturum özeti (gün sonu kasa raporu).</summary>
    [HttpGet("sessions/{sessionId:guid}/summary")]
    public async Task<IActionResult> GetSessionSummary(Guid sessionId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSessionSummaryQuery(sessionId), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>POS oturumunu kapatır.</summary>
    [HttpPost("sessions/{sessionId:guid}/close")]
    public async Task<IActionResult> CloseSession(Guid sessionId, [FromBody] CloseSessionRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CloseSessionCommand(
            sessionId,
            request.ClosingCash,
            request.Notes), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }
}

public record OpenSessionRequest(Guid RegisterId, decimal OpeningCash, string? Notes);
public record CloseSessionRequest(decimal ClosingCash, string? Notes);
public record RefundSaleRequest(string? Reason);

public record CompleteSaleItemRequest(
    Guid VariantId,
    string? Barcode,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal TaxRate);

public record CompleteSalePaymentRequest(
    string PaymentMethod,
    decimal Amount,
    decimal? TenderedAmount,
    decimal? ChangeAmount);

public record CompleteSaleRequest(
    Guid SessionId,
    Guid? MemberId,
    List<CompleteSaleItemRequest> Items,
    List<CompleteSalePaymentRequest> Payments,
    string? Notes);
