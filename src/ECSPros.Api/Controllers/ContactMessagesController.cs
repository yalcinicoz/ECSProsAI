using ECSPros.Storefront.Application.Commands.UpdateContactMessageStatus;
using ECSPros.Storefront.Application.Queries.GetContactMessages;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>P5: iletişim formu gelen kutusu (admin) — F3 mesajları DB'ye düşer, burada okunur.</summary>
[ApiController]
[Route("api/contact-messages")]
[Authorize]
public class ContactMessagesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? status = null,
        [FromQuery] Guid? firmPlatformId = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetContactMessagesQuery(status, firmPlatformId, search, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateContactMessageStatusRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateContactMessageStatusCommand(id, req.Status), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }
}

public record UpdateContactMessageStatusRequest(string Status);
