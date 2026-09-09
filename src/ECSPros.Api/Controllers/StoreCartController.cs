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
        [FromQuery] string? excludedVariantIds,
        CancellationToken ct)
    {
        Guid? memberId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (sub != null && Guid.TryParse(sub, out var mid)) memberId = mid;
        }

        // 2026-08-03: sepet sayfasındaki checkbox seçimi — dışlanan varyantlar kampanya
        // hesabına girmez (kalem listesi değişmez); virgülle ayrılmış Guid listesi.
        List<Guid>? dislananlar = null;
        if (!string.IsNullOrWhiteSpace(excludedVariantIds))
            dislananlar = excludedVariantIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .ToList();

        var result = await mediator.Send(new GetCartQuery(cartId, memberId, sessionId, firmPlatformId, dislananlar), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        // A7 (2026-09-07, mobil): sepet satırı yoksa data'sız yanıt yerine BOŞ SEPET nesnesi — zarf kuralı
        // ("data her zaman var") korunur; Id = Guid.Empty "henüz sepet yok" demektir (ilk kalem ekleme oluşturur).
        var sepet = result.Value ?? new ECSPros.Crm.Application.Queries.GetCart.CartDto(
            Guid.Empty, memberId, sessionId, firmPlatformId ?? Guid.Empty, "TRY", [], 0m);
        return Ok(new { success = true, data = sepet });
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

        // M1 (2026-09-09): istemci fiyatı ARTIK ŞART DEĞİL — fiyatı sunucu çözer (komut içinde),
        // çözülemezse orada reddedilir. Eski "price<=0 ise reddet" kontrolü kaldırıldı: mobil
        // artık fiyat göndermeyebilir, gönderdiği de yok sayılır.
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

/// <param name="Price">YOK SAYILIR (M1, 2026-09-09) — fiyatı sunucu belirler. Alan eski istemciler
/// için sözleşmede kaldı; göndermeye gerek yok.</param>
public record AddToCartRequest(Guid FirmPlatformId, Guid VariantId, int Quantity, string CurrencyCode,
    decimal Price = 0, string? SessionId = null);
public record UpdateCartItemRequest(int Quantity);
public record MergeCartsRequest(string GuestSessionId, Guid FirmPlatformId);
