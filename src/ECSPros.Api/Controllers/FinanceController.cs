using ECSPros.Finance.Application.Commands.CreateSupplier;
using ECSPros.Finance.Application.Commands.UpdateSupplier;
using ECSPros.Finance.Application.Queries.GetSupplierDetail;
using ECSPros.Finance.Application.Queries.GetSuppliers;
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
