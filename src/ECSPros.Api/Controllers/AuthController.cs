using ECSPros.Iam.Application.Commands.ChangePassword;
using ECSPros.Iam.Application.Commands.Login;
using ECSPros.Iam.Application.Commands.RefreshToken;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ECSPros.Api.Controllers;

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

    /// <summary>Refresh token ile yeni access token alır.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new RefreshTokenCommand(request.RefreshToken), ct);
        if (result.IsFailure)
            return Unauthorized(new { success = false, error = result.Error });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Token doğrulama — mevcut kullanıcı bilgisi.</summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        var email = User.FindFirst("email")?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value;
        var fullName = User.FindFirst("full_name")?.Value;
        var permissions = User.FindAll("permission").Select(c => c.Value).ToList();
        var mustChangePassword = User.FindFirst("must_change_password")?.Value == "true";

        return Ok(new { success = true, data = new { userId, email, fullName, permissions, mustChangePassword } });
    }

    /// <summary>Mevcut kullanıcı kendi şifresini değiştirir.</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid))
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new ChangePasswordCommand(uid, request.CurrentPassword, request.NewPassword), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }
}

public record LoginRequest(string Username, string Password);
public record RefreshRequest(string RefreshToken);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
