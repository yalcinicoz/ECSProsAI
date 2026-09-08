using ECSPros.Storefront.Application.Commands.ClearViewedProducts;
using ECSPros.Storefront.Application.Commands.RecordProductView;
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

    /// <summary>B1 (2026-09-07): satırda ürün adı/görsel/fiyat da gelir; <c>light=true</c> eski (kod + tarih) liste.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine(
        [FromQuery] Guid firmPlatformId, [FromQuery] int limit = 50, [FromQuery] bool light = false,
        [FromServices] ECSPros.Api.Services.Store.StoreKartZenginlestirici zengin = null!, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetMemberViewedProductsQuery(firmPlatformId, MemberId, limit), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        if (light) return Ok(new { success = true, data = result.Value });
        var harita = await zengin.GetirAsync(firmPlatformId, result.Value!.Select(v => v.ProductCode), ct);
        var data = result.Value.Select(v =>
        {
            var o = ECSPros.Api.Services.Store.StoreKartZenginlestirici.Ozet(harita, v.ProductCode);
            return new ECSPros.Api.Models.Store.ViewedProductItemDto(v.ProductCode, v.ViewedAt,
                o?.ProductName, o?.ImageUrl, o?.MinPrice, o?.CompareAtPrice, o?.CampaignPrice, o is not null && o.IsActive,
                o?.Price, o?.CampaignName, o?.CampaignBadges);
        }).ToList();
        return Ok(new { success = true, data });
    }

    /// <summary>B2 (2026-09-07, mobil): gezinme kaydı — uygulama ürün detayını açınca çağırır (web'de aynı kayıt
    /// SSR render'ında sunucuda düşer). Üye kimliği gerekir; misafir gezmeleri cihazda tutulur (sunucu kaydı yok).</summary>
    [HttpPost]
    public async Task<IActionResult> Record([FromBody] StoreViewedProductRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ProductCode))
            return BadRequest(new { success = false, error = "productCode gerekli." });
        var result = await mediator.Send(new RecordProductViewCommand(req.FirmPlatformId, MemberId, req.ProductCode.Trim()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
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

public record StoreViewedProductRequest(Guid FirmPlatformId, string ProductCode);
