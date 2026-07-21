using ECSPros.Api.Authorization;
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
}
