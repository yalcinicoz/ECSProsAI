using ECSPros.Pos.Application.Queries.GetPosRegisters;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/pos")]
[Authorize]
public class PosController : ControllerBase
{
    private readonly IMediator _mediator;

    public PosController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>POS kasalarını listeler.</summary>
    [HttpGet("registers")]
    public async Task<IActionResult> GetRegisters([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPosRegistersQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }
}
