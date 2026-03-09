using ECSPros.Iam.Application.Commands.Login;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Iam.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Kullanıcı girişi — JWT access token + refresh token döner.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new LoginCommand(request.Username, request.Password), ct);
        if (result.IsFailure)
            return Unauthorized(new { success = false, error = result.Error });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Token doğrulama — mevcut kullanıcı bilgisi.</summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst("sub")?.Value;
        var email = User.FindFirst("email")?.Value
                 ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var fullName = User.FindFirst("full_name")?.Value;
        var permissions = User.FindAll("permission").Select(c => c.Value).ToList();

        return Ok(new { success = true, data = new { userId, email, fullName, permissions } });
    }
}

public record LoginRequest(string Username, string Password);
