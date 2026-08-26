using ECSPros.Iam.Application.Commands.LoginSupplierUser;
using ECSPros.Iam.Application.Commands.RefreshSupplierUserToken;
using ECSPros.Iam.Application.Commands.RevokeSupplierUserSession;
using ECSPros.Iam.Application.Queries.GetSupplierUser;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECSPros.Api.Controllers;

/// <summary>
/// Pazaryeri satıcı paneli (satici/) kimlik uçları — SupplierUser (4. kimlik türü, type=supplier_user).
/// İç admin uçlarından (AdminOnly) ve makine Partner API'sinden (api_client) ayrıdır.
/// Panelin ince-taneli veri uçları ileride /api/supplier/* altına gelir (S2+).
/// </summary>
[ApiController]
[Route("api/supplier/auth")]
public class SupplierAuthController(IMediator mediator) : ControllerBase
{
    private string? IstemciIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? IstemciUa()
    {
        var ua = Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(ua) ? null : ua[..Math.Min(ua.Length, 500)];
    }

    [HttpPost("login")]
    [EnableRateLimiting("admin-auth")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] SupplierLoginRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new LoginSupplierUserCommand(req.Email ?? string.Empty, req.Password ?? string.Empty, IstemciIp(), IstemciUa()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("admin-auth")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] SupplierRefreshRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new RefreshSupplierUserTokenCommand(req.RefreshToken), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] SupplierLogoutRequest? req, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(req?.RefreshToken))
            await mediator.Send(new RevokeSupplierUserSessionCommand(req.RefreshToken), ct);
        return Ok(new { success = true });
    }

    [HttpGet("me")]
    [Authorize(Policy = "SupplierOnly")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var id = Guid.Parse(User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException());
        var result = await mediator.Send(new GetSupplierUserQuery(id), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }
}

public record SupplierLoginRequest(string? Email, string? Password);
public record SupplierRefreshRequest(string RefreshToken);
public record SupplierLogoutRequest(string? RefreshToken = null);
