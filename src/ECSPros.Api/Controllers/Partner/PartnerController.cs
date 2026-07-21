using System.Text.Json;
using ECSPros.Api.Authorization;
using ECSPros.Catalog.Application.Commands.SubmitPartnerProduct;
using ECSPros.Catalog.Application.Queries.GetPartnerGroupSchema;
using ECSPros.Catalog.Application.Queries.GetPartnerSubmissions;
using ECSPros.Catalog.Application.Queries.GetProductGroups;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers.Partner;

/// <summary>
/// Partner API façade (§0/§3.5) — /api/partner/v1. Kaba taneli, görev odaklı; YALNIZ API hesabı
/// token'ıyla (type=api_client) + scope ile erişilir. İç uçlardan (admin panel/storefront) ayrıdır
/// ve ayrı bir swagger dokümanında yayınlanır.
/// </summary>
[ApiController]
[Route("api/partner/v1")]
public class PartnerController : ControllerBase
{
    private readonly IMediator _mediator;

    public PartnerController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Token introspection — çağıran API hesabının kimliği ve etkin scope'ları.
    /// Entegratör token'ının hangi yetkilerle geldiğini doğrulamak için.</summary>
    [HttpGet("me")]
    [Authorize(Policy = "ApiClientOnly")]
    public IActionResult Me()
    {
        var data = new
        {
            clientId = User.FindFirst("client_id")?.Value,
            name = User.FindFirst("name")?.Value,
            ownerType = User.FindFirst("owner_type")?.Value,
            ownerId = User.FindFirst("owner_id")?.Value,
            scopes = User.FindAll("scope").Select(c => c.Value).OrderBy(s => s).ToList()
        };
        return Ok(new { success = true, data });
    }

    /// <summary>Keşif: ürün grupları + varyant eksenleri + ürün-seviyesi izinli özellikler.
    /// Gönderilecek ürün paketinde geçerli `group` kodunu ve eksen/özellik kodlarını verir (§3.6).</summary>
    [HttpGet("groups")]
    [RequireScope("catalog.read")]
    public async Task<IActionResult> Groups(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductGroupsQuery(ActiveOnly: true), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        var data = result.Value.Select(g => new
        {
            code = g.Code,
            name = g.NameI18n,
            variantAxes = g.Attributes.Where(a => a.IsVariant)
                .OrderBy(a => a.SortOrder)
                .Select(a => new { code = a.AttributeTypeCode, name = a.AttributeTypeNameI18n, primary = a.IsPrimaryAxis, required = a.IsRequired })
                .ToList(),
            attributes = g.Attributes.Where(a => !a.IsVariant)
                .OrderBy(a => a.SortOrder)
                .Select(a => new { code = a.AttributeTypeCode, name = a.AttributeTypeNameI18n, required = a.IsRequired })
                .ToList()
        }).ToList();

        return Ok(new { success = true, data });
    }

    /// <summary>Keşif: bir grubun eksen/özellikleri + her biri için izin verilen DEĞER HAVUZU.
    /// Gönderilecek pakette bu değerler kullanılmalı (Kapı 1 buna göre doğrular, §3.8).</summary>
    [HttpGet("groups/{code}")]
    [RequireScope("catalog.read")]
    public async Task<IActionResult> GroupSchema(string code, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPartnerGroupSchemaQuery(code), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Ürün kartını tek pakette gönder (§3.8). Kapı 1 doğrulaması geçerse `pending`
    /// gönderim oluşur (onaya düşer); patlarsa gerekçe listesiyle 422 döner.</summary>
    [HttpPost("products")]
    [RequireScope("catalog.write")]
    public async Task<IActionResult> SubmitProduct([FromBody] PartnerProductBody body, CancellationToken ct)
    {
        if (!TryGetOwnerId(out var supplierId))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { success = false, error = "Bu API hesabı bir tedarikçiye bağlı değil (owner yok)." });

        var canSetPrice = HasScope("pricing.write");
        var rawJson = JsonSerializer.Serialize(body);
        var result = await _mediator.Send(
            new SubmitPartnerProductCommand(supplierId, GetApiClientId(), canSetPrice, body, rawJson), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        var r = result.Value;
        if (!r.Accepted)
            return UnprocessableEntity(new { success = false, errors = r.Errors });

        return Ok(new { success = true, data = new { submissionId = r.SubmissionId, supplierProductCode = r.SupplierProductCode, status = r.Status } });
    }

    /// <summary>Owner-scoped: çağıran tedarikçinin ürün gönderimleri + durumları.</summary>
    [HttpGet("products")]
    [RequireScope("catalog.read")]
    public async Task<IActionResult> MyProducts([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (!TryGetOwnerId(out var supplierId))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { success = false, error = "Bu API hesabı bir tedarikçiye bağlı değil (owner yok)." });

        var result = await _mediator.Send(new GetPartnerSubmissionsQuery(supplierId, status, page, pageSize), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    private bool TryGetOwnerId(out Guid ownerId)
        => Guid.TryParse(User.FindFirst("owner_id")?.Value, out ownerId);

    private Guid? GetApiClientId()
        => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : null;

    private bool HasScope(string scope)
        => User.FindAll("scope").Any(c => c.Value == scope);
}
