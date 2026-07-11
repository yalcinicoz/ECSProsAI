using ECSPros.Api.Services.Store;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// G9: ziyaretçinin çözülmüş segmenti — nav şehir çipi mevcut seçimi buradan gösterir,
/// G12 admin önizlemesi ve testler segment çözümünü buradan gözler. Anonim çalışır;
/// geçerli üye bearer'ı varsa üye bağlamı (profil/adres şehri, cinsiyet, grup) devreye girer.
/// Kural verisi sızdırmaz — yalnız ziyaretçinin KENDİ segmenti.
/// </summary>
[ApiController]
[Route("api/store/segment")]
public class StoreSegmentController(IVisitorSegmentResolver resolver) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var segment = await resolver.ResolveAsync(
            HttpContext, VisitorSegmentResolver.MemberIdFromClaims(User), ct);
        return Ok(new
        {
            success = true,
            data = new
            {
                city = segment.CityCode,
                cityName = segment.CityName,
                region = segment.Region,
                gender = segment.Gender,
                device = segment.Device,
                membership = segment.IsMember ? "member" : "guest",
                memberGroup = segment.MemberGroupId,
            },
        });
    }
}
