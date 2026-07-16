using ECSPros.Crm.Application.Commands.AddToCart;
using ECSPros.Crm.Application.Commands.ClearCart;
using ECSPros.Crm.Application.Commands.MergeCarts;
using ECSPros.Crm.Application.Commands.RemoveCartItem;
using ECSPros.Crm.Application.Commands.UpdateCartItem;
using ECSPros.Crm.Application.Queries.GetCart;
using ECSPros.Core.Application.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/store/cart")]
public class StoreCartController(IMediator mediator, ICoreDbContext coreDb, IMemoryCache cache) : ControllerBase
{
    /// <summary>B12: platformun "stok kontrolü" anahtarı (Settings.stockControlEnabled, 5 dk cache).</summary>
    private async Task<bool> StokKontroluAcikMiAsync(Guid firmPlatformId, CancellationToken ct) =>
        await cache.GetOrCreateAsync($"stok-kontrolu:{firmPlatformId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var settings = await coreDb.FirmPlatforms.AsNoTracking()
                .Where(p => p.Id == firmPlatformId)
                .Select(p => p.Settings)
                .FirstOrDefaultAsync(ct);
            return settings is not null
                && settings.TryGetValue("stockControlEnabled", out var deger)
                && deger is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.True };
        });
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

        // Fiyat sıfır güvencesi (2026-07-16): fiyatı doğrulanamayan kalem sepete giremez —
        // istemci 0/negatif fiyat gönderirse (varyantta fiyat çözülememiş demektir) reddedilir.
        if (req.Price <= 0)
            return BadRequest(new { success = false, error = "Ürün fiyatı doğrulanamadı; ürün sepete eklenemedi." });

        var result = await mediator.Send(new AddToCartCommand(
            req.FirmPlatformId, req.VariantId, req.Quantity, req.Price,
            req.CurrencyCode, memberId, req.SessionId,
            EnforceStock: await StokKontroluAcikMiAsync(req.FirmPlatformId, ct)), ct);
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
