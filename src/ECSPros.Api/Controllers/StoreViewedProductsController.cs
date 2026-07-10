using ECSPros.Storefront.Application.Commands.ClearViewedProducts;
using ECSPros.Storefront.Application.Queries.GetMemberViewedProducts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// E12: Önceden Gezdiklerim — üyenin gezme geçmişi. Kayıt detay sayfası render'ında
/// sunucuda atılır (misafir localStorage fallback'i detay script'inde); burada
/// listeleme (Faz G "son gezilenler" bloğu da kullanır) ve temizleme var.
/// </summary>
[ApiController]
[Route("api/store/viewed-products")]
[Authorize(Policy = "MemberOnly")]
public class StoreViewedProductsController(IMediator mediator) : ControllerBase
{
    private Guid MemberId => Guid.Parse(
        User.FindFirst("sub")?.Value
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!);

    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] Guid firmPlatformId, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetMemberViewedProductsQuery(firmPlatformId, MemberId, limit), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Listeyi Temizle — üyenin platformdaki tüm gezme kayıtları silinir.</summary>
    [HttpDelete]
    public async Task<IActionResult> Clear([FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        var result = await mediator.Send(new ClearViewedProductsCommand(firmPlatformId, MemberId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }
}
