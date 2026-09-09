using ECSPros.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// M4 (2026-09-09, mobil isteği 4): durum kodu → etiket/renk sözlüğü — TEK uç, versiyonlu.
///
/// Neden tek uç: her yanıta etiket gömmek sözleşmeyi şişirir ve istemciler arasında tutarsızlaşır
/// (mobil ekibin tercihi de buydu). İstemci açılışta bir kez çeker, <c>version</c>'ı saklar ve
/// sonraki isteklerde <c>If-None-Match</c> ile 304 alır. Sürüm katalog içeriğinden türetilir
/// (<see cref="DurumEtiketleri.Surum"/>) — yeni durum eklenince kendiliğinden değişir.
///
/// Etiketler VİTRİN dilindedir (müşteri); panel kendi haritasını kullanır.
/// Sipariş/iade uçları aynı etiketi satırda da döner (<c>statusLabel</c>) — çevrimdışı ilk açılışta
/// istemci sözlüğü henüz çekmemiş olabilir diye.
/// </summary>
[ApiController]
[Route("api/store/lookups")]
[AllowAnonymous]
public class StoreLookupsController : ControllerBase
{
    /// <returns>
    /// <c>{ version, families: { orderStatus:[{code,label,color,variant,description}], paymentStatus, paymentMethod,
    /// returnStatus, reviewStatus, questionStatus }, timelines: { orderStatus:[{code,label}], returnStatus:[...] } }</c>
    /// </returns>
    [HttpGet]
    public IActionResult Get()
    {
        var surum = DurumEtiketleri.Surum;
        var etag = $"\"{surum}\"";

        // İstemci aynı sürümü tutuyorsa gövde gönderilmez.
        if (Request.Headers.IfNoneMatch.Any(v => v == etag || v == "W/" + etag))
        {
            Response.Headers.ETag = etag;
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "public, max-age=3600";

        return Ok(new
        {
            success = true,
            data = new
            {
                version = surum,
                families = DurumEtiketleri.Vitrin.Aileler.ToDictionary(
                    a => a.Key,
                    a => a.Value.Select(e => new
                    {
                        code = e.Kod, label = e.Etiket, color = e.Renk, variant = e.Varyant, description = e.Aciklama,
                    }).ToList()),
                timelines = new
                {
                    orderStatus = DurumEtiketleri.SiparisAkisAdimlari.Select(a => new { code = a.Kod, label = a.Etiket }),
                    returnStatus = DurumEtiketleri.IadeAkisAdimlari.Select(a => new { code = a.Kod, label = a.Etiket }),
                },
            },
        });
    }
}
