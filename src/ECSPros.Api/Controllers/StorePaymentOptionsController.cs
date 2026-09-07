using ECSPros.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// A9 (2026-09-07, mobil): kanalın ödeme yöntemleri + kapıda ödeme bedeli/üst sınırı — panel "Kanallar"
/// ayarından (IPaymentOptionsProvider, 1 dk önbellek). Checkout'taki sunucu doğrulaması AYNI kaynağı
/// kullanır; istemci gömülü config yerine bunu gösterir. Anonim (misafir checkout da kullanır).
/// </summary>
[ApiController]
[Route("api/store/payment-options")]
[AllowAnonymous]
public class StorePaymentOptionsController(IPaymentOptionsProvider paymentOptions) : ControllerBase
{
    /// <returns><c>{ methods: ["kart","kapida-nakit","kapida-kart"], codFee: 50, codLimit: 3000 }</c>
    /// — codLimit 0 = üst sınır yok.</returns>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        if (firmPlatformId == Guid.Empty)
            return BadRequest(new { success = false, error = "firmPlatformId gerekli (query veya X-Firm-Platform başlığı)." });
        var o = await paymentOptions.GetAsync(firmPlatformId, ct);
        return Ok(new { success = true, data = new { methods = o.EnabledMethods, codFee = o.CodServiceFee, codLimit = o.CodMaxOrderTotal } });
    }
}
