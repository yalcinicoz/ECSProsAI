using ECSPros.Pos.Application.Commands.CloseSession;
using ECSPros.Pos.Application.Commands.OpenSession;
using ECSPros.Pos.Application.Queries.GetPosRegisters;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

    /// <summary>POS oturumu açar.</summary>
    [HttpPost("sessions/open")]
    public async Task<IActionResult> OpenSession([FromBody] OpenSessionRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid))
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new OpenSessionCommand(
            request.RegisterId,
            uid,
            request.OpeningCash,
            request.Notes), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/pos/sessions/{result.Value}", new { success = true, data = new { sessionId = result.Value } });
    }

    /// <summary>POS oturumunu kapatır.</summary>
    [HttpPost("sessions/{sessionId:guid}/close")]
    public async Task<IActionResult> CloseSession(Guid sessionId, [FromBody] CloseSessionRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CloseSessionCommand(
            sessionId,
            request.ClosingCash,
            request.Notes), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }
}

public record OpenSessionRequest(Guid RegisterId, decimal OpeningCash, string? Notes);
public record CloseSessionRequest(decimal ClosingCash, string? Notes);
