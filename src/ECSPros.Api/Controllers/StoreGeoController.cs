using ECSPros.Crm.Application.Queries.GetGeoLookups;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// C4 (K6): adres hiyerarşisi kademeli lookup'ları (anonim) — teslimat adres formu,
/// profil şehri ve kişiselleştirme şehir seçicisi (G9) aynı kaynaktan beslenir.
/// Mahalle listesi büyük olduğundan yalnız districtId + arama parametresiyle döner
/// (tasarımın aramalı özel select bileşeni kullanılır).
/// </summary>
[ApiController]
[Route("api/store/geo")]
public class StoreGeoController(IMediator mediator) : ControllerBase
{
    [HttpGet("countries")]
    public async Task<IActionResult> GetCountries(CancellationToken ct)
    {
        var result = await mediator.Send(new GetGeoCountriesQuery(), ct);
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("cities")]
    public async Task<IActionResult> GetCities([FromQuery] Guid countryId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetGeoCitiesQuery(countryId), ct);
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("districts")]
    public async Task<IActionResult> GetDistricts([FromQuery] Guid cityId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetGeoDistrictsQuery(cityId), ct);
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("neighborhoods")]
    public async Task<IActionResult> GetNeighborhoods(
        [FromQuery] Guid districtId, [FromQuery] string? search, [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetGeoNeighborhoodsQuery(districtId, search, limit), ct);
        return Ok(new { success = true, data = result.Value });
    }
}
