using ECSPros.Storefront.Application.Commands.DeleteCartRemovedItem;
using ECSPros.Storefront.Application.Commands.RecordCartRemovedItem;
using ECSPros.Storefront.Application.Queries.GetCartRemovedItems;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// 2026-07-17: sepetten çıkarılan ürünlerin üyeye bağlı kalıcı geçmişi — sepet
/// sayfasındaki "Önceden Eklediklerim" (ms-sepet-onceden) bölümü. Misafirde bölüm
/// localStorage ile çalışır (istemci kararı); üye girişinde kaynak bu API'dir.
/// </summary>
[ApiController]
[Route("api/store/cart/removed")]
[Authorize(Policy = "MemberOnly")]
public class StoreCartRemovedController(IMediator mediator) : ControllerBase
{
    private Guid MemberId => Guid.Parse(
        User.FindFirst("sub")?.Value
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!);

    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetCartRemovedItemsQuery(firmPlatformId, MemberId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost]
    public async Task<IActionResult> Record([FromBody] StoreCartRemovedRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new RecordCartRemovedItemCommand(
            req.FirmPlatformId, MemberId, req.VariantId, req.ProductCode ?? "",
            req.Name ?? "", req.ImageUrl, req.Price, req.CurrencyCode), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpDelete("{variantId:guid}")]
    public async Task<IActionResult> Delete(Guid variantId, [FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteCartRemovedItemCommand(firmPlatformId, MemberId, variantId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }
}

public record StoreCartRemovedRequest(
    Guid FirmPlatformId,
    Guid VariantId,
    string? ProductCode,
    string? Name,
    string? ImageUrl,
    decimal Price,
    string? CurrencyCode);
