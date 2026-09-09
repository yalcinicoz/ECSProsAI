using ECSPros.Iam.Application.Commands.ChangePassword;
using ECSPros.Iam.Application.Commands.GenerateApiToken;
using ECSPros.Iam.Application.Commands.Login;
using ECSPros.Iam.Application.Commands.RefreshToken;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    [EnableRateLimiting("admin-auth")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new LoginCommand(request.Username, request.Password), ct);
        if (result.IsFailure)
            return Unauthorized(new { success = false, error = result.Error });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>API hesabı (makine kimliği) — OAuth2 client_credentials ile access token alır.
    /// Yalnız RequireScope ile korunan partner uçlarına erişir; iç uçları geçemez (type=api_client).</summary>
    [HttpPost("token")]
    [EnableRateLimiting("admin-auth")]
    [AllowAnonymous]
    public async Task<IActionResult> Token([FromBody] ApiTokenRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _mediator.Send(new GenerateApiTokenCommand(request.ClientId, request.ClientSecret, ip), ct);
        if (result.IsFailure)
            return Unauthorized(new { success = false, error = result.Error });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Refresh token ile yeni access token alır.</summary>
    [HttpPost("refresh")]
    [EnableRateLimiting("admin-auth")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new RefreshTokenCommand(request.RefreshToken), ct);
        if (result.IsFailure)
            return Unauthorized(new { success = false, error = result.Error });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Token doğrulama — mevcut kullanıcı bilgisi.</summary>
    /// <summary>Mevcut kullanıcı + EFEKTİF yetkileri (Y1, K3): yetki token'dan değil, her
    /// çağrıda yetki servisinden gelir — panel menüsü/butonları anında doğru olur.
    /// <c>channels</c>: kanal kapsamlı yetkilerde geçerli kanal kimlikleri (kapsamsızlarda yok).</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(
        [FromServices] ECSPros.Iam.Application.Services.IEtkinYetkiServisi yetkiServisi,
        CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        var email = User.FindFirst("email")?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value;
        var fullName = User.FindFirst("full_name")?.Value;
        var mustChangePassword = User.FindFirst("must_change_password")?.Value == "true";
        // K5: süper admin bayrağı — panel menü/buton görünürlüğünde tüm kontrolleri geçer.
        var isSuperAdmin = User.FindFirst("sa")?.Value == "true";

        var permissions = new List<string>();
        var channels = new Dictionary<string, List<Guid>>();
        if (Guid.TryParse(userId, out var uid))
        {
            var yetkiler = await yetkiServisi.GetirAsync(uid, ct);
            isSuperAdmin = isSuperAdmin || yetkiler.SuperAdmin;
            foreach (var key in yetkiler.Keyler)
            {
                permissions.Add(key);
                if (yetkiler.Kanallar(key) is { } kanallar) channels[key] = kanallar.ToList();
            }
        }

        return Ok(new { success = true, data = new { userId, email, fullName, permissions, channels, mustChangePassword, isSuperAdmin } });
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

public record ApiTokenRequest(string ClientId, string ClientSecret);
public record LoginRequest(string Username, string Password);
public record RefreshRequest(string RefreshToken);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
