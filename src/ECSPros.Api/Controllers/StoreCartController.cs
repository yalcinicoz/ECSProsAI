using ECSPros.Crm.Application.Commands.AddToCart;
using ECSPros.Crm.Application.Commands.ClearCart;
using ECSPros.Crm.Application.Commands.MergeCarts;
using ECSPros.Crm.Application.Commands.RemoveCartItem;
using ECSPros.Crm.Application.Commands.UpdateCartItem;
using ECSPros.Crm.Application.Queries.GetCart;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/store/cart")]
public class StoreCartController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCart(
        [FromQuery] Guid? cartId,
        [FromQuery] Guid? firmPlatformId,
        [FromQuery] string? sessionId,
        CancellationToken ct)
    {
        Guid? memberId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (sub != null && Guid.TryParse(sub, out var mid)) memberId = mid;
        }

        var result = await mediator.Send(new GetCartQuery(cartId, memberId, sessionId, firmPlatformId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddItem([FromBody] AddToCartRequest req, CancellationToken ct)
    {
        Guid? memberId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (sub != null && Guid.TryParse(sub, out var mid)) memberId = mid;
        }

        var result = await mediator.Send(new AddToCartCommand(
            req.FirmPlatformId, req.VariantId, req.Quantity, req.Price,
            req.CurrencyCode, memberId, req.SessionId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { cartId = result.Value } });
    }

    [HttpPut("{cartId}/items/{itemId}")]
    public async Task<IActionResult> UpdateItem(Guid cartId, Guid itemId, [FromBody] UpdateCartItemRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateCartItemCommand(cartId, itemId, req.Quantity), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpDelete("{cartId}/items/{itemId}")]
    public async Task<IActionResult> RemoveItem(Guid cartId, Guid itemId, CancellationToken ct)
    {
        var result = await mediator.Send(new RemoveCartItemCommand(cartId, itemId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpDelete("{cartId}")]
    public async Task<IActionResult> ClearCart(Guid cartId, CancellationToken ct)
    {
        var result = await mediator.Send(new ClearCartCommand(cartId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpPost("merge")]
    [Authorize(Policy = "MemberOnly")]
    public async Task<IActionResult> MergeCarts([FromBody] MergeCartsRequest req, CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!);
        var result = await mediator.Send(new MergeCartsCommand(req.GuestSessionId, memberId, req.FirmPlatformId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { cartId = result.Value } });
    }
}

public record AddToCartRequest(Guid FirmPlatformId, Guid VariantId, int Quantity, decimal Price, string CurrencyCode, string? SessionId = null);
public record UpdateCartItemRequest(int Quantity);
public record MergeCartsRequest(string GuestSessionId, Guid FirmPlatformId);
