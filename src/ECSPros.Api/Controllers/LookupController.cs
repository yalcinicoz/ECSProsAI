using ECSPros.Core.Application.Queries.GetLookupTypes;
using ECSPros.Core.Application.Queries.GetLookupValues;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/lookup")]
[Authorize]
public class LookupController : ControllerBase
{
    private readonly IMediator _mediator;

    public LookupController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Tüm lookup tiplerini listeler.</summary>
    [HttpGet("types")]
    public async Task<IActionResult> GetTypes(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetLookupTypesQuery(), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Belirli bir lookup tipine ait değerleri listeler.</summary>
    [HttpGet("types/{code}/values")]
    public async Task<IActionResult> GetValues(string code, [FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetLookupValuesQuery(code, activeOnly), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });

        return Ok(new { success = true, data = result.Value });
    }
}
