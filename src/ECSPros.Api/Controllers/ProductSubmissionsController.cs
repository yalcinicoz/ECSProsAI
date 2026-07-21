using System.Security.Claims;
using ECSPros.Api.Authorization;
using ECSPros.Catalog.Application.Commands.ApproveProductSubmission;
using ECSPros.Catalog.Application.Commands.RejectProductSubmission;
using ECSPros.Catalog.Application.Queries.GetAdminProductSubmissions;
using ECSPros.Catalog.Application.Queries.GetProductSubmissionDetail;
using ECSPros.Shared.Kernel.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// İÇ uç (admin panel) — tedarikçi ürün gönderimlerinin (Partner façade'dan gelen) incelenmesi ve
/// Kapı 2 kararı (onay/red). Partner değil; personel yetkisiyle (catalog.products.manage).
/// </summary>
[ApiController]
[Route("api/catalog/product-submissions")]
[RequirePermission(Permissions.CatalogProductsManage)]
public class ProductSubmissionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductSubmissionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Gönderim listesi (durum/tedarikçi filtreli, sayfalı).</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] Guid? supplierId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAdminProductSubmissionsQuery(status, supplierId, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Gönderim detayı — tam ham gövde (inceleme için).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductSubmissionDetailQuery(id), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Onayla → canlı Product oluşturulur (§3.8 Kapı 2).</summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ApproveProductSubmissionCommand(id, CurrentUserId()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Reddet (gerekçeli) → canlıya çıkmaz.</summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new RejectProductSubmissionCommand(id, request.Reason, CurrentUserId()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    private Guid? CurrentUserId()
        => Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value, out var id) ? id : null;
}

public record RejectRequest(string Reason);
