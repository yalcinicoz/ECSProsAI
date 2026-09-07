using ECSPros.Order.Application.Queries.GetGuestOrderTracking;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECSPros.Api.Controllers;

/// <summary>
/// C2 (2026-09-07): üyeliksiz sipariş takibi — sipariş no + alıcı telefonu. Anonim; numara taramasına
/// karşı "store-sensitive" oran sınırı (30/dk-IP) ve tek tip hata mesajı.
/// </summary>
[ApiController]
[Route("api/store/orders")]
[AllowAnonymous]
[EnableRateLimiting("store-sensitive")]
public class StoreOrderTrackingController(IMediator mediator) : ControllerBase
{
    /// <summary>GET /api/store/orders/track?orderNumber=MIS0000059&amp;phone=05xx...&amp;firmPlatformId=</summary>
    [HttpGet("track")]
    public async Task<IActionResult> Track(
        [FromQuery] string orderNumber, [FromQuery] string phone, [FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        if (firmPlatformId == Guid.Empty)
            return BadRequest(new { success = false, error = "firmPlatformId gerekli (query veya X-Firm-Platform başlığı)." });
        var result = await mediator.Send(new GetGuestOrderTrackingQuery(firmPlatformId, orderNumber ?? "", phone ?? ""), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Aynı işlev, POST gövdesiyle (telefonun URL'de görünmemesi için).</summary>
    [HttpPost("track")]
    public async Task<IActionResult> TrackPost([FromBody] GuestOrderTrackRequest req, CancellationToken ct)
    {
        if (req.FirmPlatformId == Guid.Empty)
            return BadRequest(new { success = false, error = "firmPlatformId gerekli (gövde veya X-Firm-Platform başlığı)." });
        var result = await mediator.Send(new GetGuestOrderTrackingQuery(req.FirmPlatformId, req.OrderNumber ?? "", req.Phone ?? ""), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }
}

public record GuestOrderTrackRequest(Guid FirmPlatformId, string? OrderNumber, string? Phone);
