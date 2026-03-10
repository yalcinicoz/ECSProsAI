using ECSPros.Finance.Application.Commands.CreateSupplier;
using ECSPros.Finance.Application.Commands.CreateSupplierDelivery;
using ECSPros.Finance.Application.Commands.CreateSupplierInvoice;
using ECSPros.Finance.Application.Commands.CreateSupplierPayment;
using ECSPros.Finance.Application.Commands.CreateSupplierReturn;
using ECSPros.Finance.Application.Commands.UpdateSupplier;
using ECSPros.Finance.Application.Queries.GetSupplierDetail;
using ECSPros.Finance.Application.Queries.GetSupplierInvoices;
using ECSPros.Finance.Application.Queries.GetSuppliers;
using ECSPros.Finance.Application.Queries.GetSupplierTransactions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/finance")]
[Authorize]
public class FinanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public FinanceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Tedarikçileri listeler.</summary>
    [HttpGet("suppliers")]
    public async Task<IActionResult> GetSuppliers(
        [FromQuery] string? search,
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetSuppliersQuery(search, activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Tedarikçi detayını döner.</summary>
    [HttpGet("suppliers/{id:guid}")]
    public async Task<IActionResult> GetSupplierDetail(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSupplierDetailQuery(id), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni tedarikçi oluşturur.</summary>
    [HttpPost("suppliers")]
    public async Task<IActionResult> CreateSupplier([FromBody] CreateSupplierRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateSupplierCommand(
            request.Code,
            request.Name,
            request.TaxOffice,
            request.TaxNumber,
            request.Phone,
            request.Email,
            request.Address,
            request.ContactPerson,
            request.Notes), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/finance/suppliers", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Tedarikçi günceller.</summary>
    [HttpPut("suppliers/{id:guid}")]
    public async Task<IActionResult> UpdateSupplier(Guid id, [FromBody] UpdateSupplierRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var result = await _mediator.Send(new UpdateSupplierCommand(
            id,
            request.Name,
            request.TaxOffice,
            request.TaxNumber,
            request.Phone,
            request.Email,
            request.Address,
            request.ContactPerson,
            request.Notes,
            request.IsActive,
            userId), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }

    // ─── Supplier Invoices ─────────────────────────────────────────────────────

    /// <summary>Tedarikçi faturalarını listeler.</summary>
    [HttpGet("supplier-invoices")]
    public async Task<IActionResult> GetSupplierInvoices(
        [FromQuery] Guid? supplierId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetSupplierInvoicesQuery(supplierId, status, page, pageSize), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Tedarikçi faturası oluşturur.</summary>
    [HttpPost("supplier-invoices")]
    public async Task<IActionResult> CreateSupplierInvoice([FromBody] CreateSupplierInvoiceRequest request, CancellationToken ct)
    {
        var items = request.Items.Select(i => new CreateSupplierInvoiceItemDto(
            i.VariantId, i.Description, i.Quantity, i.UnitPrice, i.DiscountRate, i.TaxRate)).ToList();

        var result = await _mediator.Send(new CreateSupplierInvoiceCommand(
            request.SupplierId, request.InvoiceNumber, request.InvoiceDate,
            request.DueDate, request.Notes, items), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created("/api/finance/supplier-invoices", new { success = true, data = new { id = result.Value } });
    }

    // ─── Supplier Deliveries ───────────────────────────────────────────────────

    /// <summary>Tedarikçi teslimatı oluşturur.</summary>
    [HttpPost("supplier-deliveries")]
    public async Task<IActionResult> CreateSupplierDelivery([FromBody] CreateSupplierDeliveryRequest request, CancellationToken ct)
    {
        var items = request.Items.Select(i => new CreateDeliveryItemDto(
            i.VariantId, i.ExpectedQuantity, i.LocationId)).ToList();

        var result = await _mediator.Send(new CreateSupplierDeliveryCommand(
            request.SupplierId, request.InvoiceId, request.DeliveryDate,
            request.DeliveryNoteNumber, request.WarehouseId, request.Notes, items), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created("/api/finance/supplier-deliveries", new { success = true, data = new { id = result.Value } });
    }

    // ─── Supplier Payments ─────────────────────────────────────────────────────

    /// <summary>Tedarikçiye ödeme kaydeder.</summary>
    [HttpPost("supplier-payments")]
    public async Task<IActionResult> CreateSupplierPayment([FromBody] CreateSupplierPaymentRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateSupplierPaymentCommand(
            request.SupplierId, request.PaymentDate, request.Amount,
            request.PaymentType, request.Notes), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created("/api/finance/supplier-payments", new { success = true, data = new { id = result.Value } });
    }

    // ─── Supplier Returns ──────────────────────────────────────────────────────

    /// <summary>Tedarikçiye iade oluşturur.</summary>
    [HttpPost("supplier-returns")]
    public async Task<IActionResult> CreateSupplierReturn([FromBody] CreateSupplierReturnRequest request, CancellationToken ct)
    {
        var items = request.Items.Select(i => new CreateSupplierReturnItemDto(
            i.VariantId, i.Quantity, i.UnitPrice, i.TaxRate)).ToList();

        var result = await _mediator.Send(new CreateSupplierReturnCommand(
            request.SupplierId, request.ReturnDate, request.Reason, request.Notes, items), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created("/api/finance/supplier-returns", new { success = true, data = new { id = result.Value } });
    }

    // ─── Supplier Transactions (Cari Hesap) ────────────────────────────────────

    /// <summary>Tedarikçi cari hesap hareketleri.</summary>
    [HttpGet("suppliers/{id:guid}/transactions")]
    public async Task<IActionResult> GetSupplierTransactions(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetSupplierTransactionsQuery(id, page, pageSize), ct);
        return Ok(new { success = true, data = result.Value });
    }
}

public record CreateSupplierRequest(
    string Code,
    string Name,
    string? TaxOffice,
    string? TaxNumber,
    string? Phone,
    string? Email,
    string? Address,
    string? ContactPerson,
    string? Notes);

public record UpdateSupplierRequest(
    string Name,
    string? TaxOffice,
    string? TaxNumber,
    string? Phone,
    string? Email,
    string? Address,
    string? ContactPerson,
    string? Notes,
    bool IsActive);

public record CreateSupplierInvoiceRequest(
    Guid SupplierId,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    string? Notes,
    List<InvoiceItemRequest> Items);

public record InvoiceItemRequest(
    Guid? VariantId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountRate = 0,
    decimal TaxRate = 0);

public record CreateSupplierDeliveryRequest(
    Guid SupplierId,
    Guid? InvoiceId,
    DateOnly DeliveryDate,
    string? DeliveryNoteNumber,
    Guid WarehouseId,
    string? Notes,
    List<DeliveryItemRequest> Items);

public record DeliveryItemRequest(Guid VariantId, int ExpectedQuantity, Guid? LocationId);

public record CreateSupplierPaymentRequest(
    Guid SupplierId,
    DateOnly PaymentDate,
    decimal Amount,
    string PaymentType,
    string? Notes);

public record CreateSupplierReturnRequest(
    Guid SupplierId,
    DateOnly ReturnDate,
    string Reason,
    string? Notes,
    List<SupplierReturnItemRequest> Items);

public record SupplierReturnItemRequest(Guid VariantId, int Quantity, decimal UnitPrice, decimal TaxRate = 0);
