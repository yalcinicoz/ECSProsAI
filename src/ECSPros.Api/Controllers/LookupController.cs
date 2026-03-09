using ECSPros.Core.Application.Commands.CreateLookupType;
using ECSPros.Core.Application.Commands.CreateLookupValue;
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

    /// <summary>Yeni lookup tipi oluşturur.</summary>
    [HttpPost("types")]
    public async Task<IActionResult> CreateType([FromBody] CreateLookupTypeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateLookupTypeCommand(request.Code, request.NameI18n, request.Description), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/lookup/types", new { success = true, data = new { id = result.Value } });
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

    /// <summary>Lookup tipine yeni değer ekler.</summary>
    [HttpPost("types/{code}/values")]
    public async Task<IActionResult> CreateValue(string code, [FromBody] CreateLookupValueRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateLookupValueCommand(
            code, request.Code, request.NameI18n, request.Color, request.Icon,
            request.IsDefault, request.SortOrder), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/lookup/types/{code}/values", new { success = true, data = new { id = result.Value } });
    }
}

public record CreateLookupTypeRequest(string Code, Dictionary<string, string> NameI18n, string? Description);
public record CreateLookupValueRequest(
    string Code,
    Dictionary<string, string> NameI18n,
    string? Color,
    string? Icon,
    bool IsDefault = false,
    int SortOrder = 0);
