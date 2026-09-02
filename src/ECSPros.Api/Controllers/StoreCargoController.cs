using ECSPros.Core.Application.Queries.GetCargoOptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// Teslimat adımı kargo seçenekleri (2026-07-22, anonim): adres mahallesine atanmış
/// kargolar mahalle önceliğiyle; atama yoksa tüm aktif kargolar genel öncelikle.
/// Misafir akışı da kullanır — kimlik gerektirmez, yalnız görüntüleme verisi döner.
/// </summary>
[ApiController]
[Route("api/store/cargo-options")]
public class StoreCargoController(IMediator mediator) : ControllerBase
{
    /// <param name="paymentMethod">Opsiyonel (2026-09-02): kart | kapida-nakit | kapida-kart —
    /// verilirse liste bu yönteme uygun entegrasyonlarla süzülür (panel "Kargoya Ver" önerisi).</param>
    [HttpGet]
    public async Task<IActionResult> GetOptions(
        [FromQuery] Guid firmPlatformId, [FromQuery] Guid? neighborhoodId,
        [FromQuery] string? paymentMethod, CancellationToken ct)
    {
        if (firmPlatformId == Guid.Empty)
            return BadRequest(new { success = false, error = "firmPlatformId zorunlu." });
        var result = await mediator.Send(new GetCargoOptionsQuery(firmPlatformId, neighborhoodId, paymentMethod), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }
}
