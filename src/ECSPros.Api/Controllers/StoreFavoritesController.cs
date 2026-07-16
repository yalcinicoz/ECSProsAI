using ECSPros.Storefront.Application.Commands.AddFavorite;
using ECSPros.Storefront.Application.Commands.RemoveFavorite;
using ECSPros.Storefront.Application.Queries.GetMemberFavorites;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// E5: Favoriler — kalp butonları (liste/detay/sepet) buna bağlanır. Anahtar ProductCode;
/// ekleme idempotent, çıkarma kayıtsızsa da başarı döner (toggle UX).
/// </summary>
[ApiController]
[Route("api/store/favorites")]
[Authorize(Policy = "MemberOnly")]
public class StoreFavoritesController(IMediator mediator) : ControllerBase
{
    private Guid MemberId => Guid.Parse(
        User.FindFirst("sub")?.Value
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!);

    /// <summary>Üyenin favori ürün kodları (yeni → eski) — kalp işaretleme için hafif liste.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetMemberFavoritesQuery(firmPlatformId, MemberId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] StoreFavoriteRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new AddFavoriteCommand(
            req.FirmPlatformId, MemberId, req.ProductCode, req.VariantId, req.ColorValueId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpDelete("{productCode}")]
    public async Task<IActionResult> Remove(string productCode, [FromQuery] Guid firmPlatformId,
        [FromQuery] Guid? colorValueId, CancellationToken ct)
    {
        var result = await mediator.Send(new RemoveFavoriteCommand(firmPlatformId, MemberId, productCode, colorValueId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }
}

public record StoreFavoriteRequest(Guid FirmPlatformId, string ProductCode, Guid? VariantId = null, Guid? ColorValueId = null);
