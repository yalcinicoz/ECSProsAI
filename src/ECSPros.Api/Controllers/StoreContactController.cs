using ECSPros.Storefront.Application.Commands.CreateContactMessage;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// F3: iletişim formu — mesaj DB'ye kaydedilir (kullanıcı kararı; admin listesi ileri iş).
/// Misafir de gönderebilir; bearer token verilmişse MemberId kaydedilir.
/// </summary>
[ApiController]
[Route("api/store/contact")]
public class StoreContactController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create([FromBody] ContactMessageRequest req, CancellationToken ct)
    {
        Guid? memberId = null;
        var sub = User.FindFirst("sub")?.Value
                  ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(sub, out var kimlik)) memberId = kimlik;

        var result = await mediator.Send(new CreateContactMessageCommand(
            req.FirmPlatformId, req.Name, req.Email, req.Message, req.Phone, req.Subject, memberId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }
}

public record ContactMessageRequest(
    Guid FirmPlatformId,
    string Name,
    string Email,
    string Message,
    string? Phone = null,
    string? Subject = null);
