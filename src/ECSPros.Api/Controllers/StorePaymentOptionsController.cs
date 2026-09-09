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
    /// <returns>
    /// <c>{ methods: [{code,label,description,fee}], methodCodes: ["kart",...], codFee: 50, codLimit: 3000 }</c>
    /// — codLimit 0 = üst sınır yok.
    /// <para>M4 (2026-09-09, mobil): <c>methods</c> artık düz kod dizisi değil, etiketli nesne dizisi —
    /// istemci ödeme ekranını gömülü metin tutmadan kurar. Etiketler
    /// <see cref="DurumEtiketleri.Vitrin.OdemeYontemi"/>'nden gelir; <c>fee</c> kapıda ödeme bedelidir
    /// (kartta 0). ⚠ Kırılmayı önlemek için eski düz dizi <c>methodCodes</c> adıyla korunur — web
    /// ve eski mobil sürümler ondan okumaya devam edebilir.</para>
    /// </returns>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        if (firmPlatformId == Guid.Empty)
            return BadRequest(new { success = false, error = "firmPlatformId gerekli (query veya X-Firm-Platform başlığı)." });
        var o = await paymentOptions.GetAsync(firmPlatformId, ct);

        // Sıra kanalın ayarındaki sıra değil, kataloğun sırası (kart → kapıda nakit → kapıda kart):
        // ödeme ekranı her kanalda aynı düzende görünsün.
        var methods = DurumEtiketleri.Vitrin.OdemeYontemi
            .Where(y => o.YontemAcik(y.Kod))
            .Select(y => new
            {
                code = y.Kod,
                label = y.Etiket,
                description = y.Aciklama,
                fee = y.Kod.StartsWith("kapida", StringComparison.Ordinal) ? o.CodServiceFee : 0m,
                maxOrderTotal = y.Kod.StartsWith("kapida", StringComparison.Ordinal) ? o.CodMaxOrderTotal : 0m,
            })
            .ToList();

        return Ok(new
        {
            success = true,
            data = new
            {
                methods,
                methodCodes = o.EnabledMethods,   // geriye dönük: eski "methods" dizisinin aynısı
                codFee = o.CodServiceFee,
                codLimit = o.CodMaxOrderTotal,
            },
        });
    }
}
