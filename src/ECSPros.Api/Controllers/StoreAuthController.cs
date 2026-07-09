using ECSPros.Api.Services;
using ECSPros.Crm.Application.Commands.LoginMember;
using ECSPros.Crm.Application.Commands.RefreshMemberToken;
using ECSPros.Crm.Application.Commands.RegisterMember;
using ECSPros.Crm.Application.Commands.RevokeMemberSession;
using ECSPros.Crm.Application.Queries.GetMemberDetail;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/store/auth")]
public class StoreAuthController(IMediator mediator) : ControllerBase
{
    /// <summary>D1: access token'ı SSR kimliği için HttpOnly cookie'ye de yazar.
    /// Secure yalnız HTTPS istekte (origin Cloudflare Flexible arkasında HTTP çalışır;
    /// localhost testleri de HTTP). JS localStorage akışı değişmez.</summary>
    private void UyeCerezYaz(MemberLoginResponse veri) =>
        Response.Cookies.Append(StoreMemberSession.CookieAdi, veri.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Expires = veri.ExpiresAt,
            Path = "/"
        });

    private void UyeCerezSil() =>
        Response.Cookies.Delete(StoreMemberSession.CookieAdi, new CookieOptions { Path = "/" });

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterMemberRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new RegisterMemberCommand(req.Email, req.Password, req.FirstName, req.LastName, req.Phone), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { memberId = result.Value } });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginMemberRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new LoginMemberCommand(req.Email, req.Password), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        UyeCerezYaz(result.Value!);
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshMemberRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new RefreshMemberTokenCommand(req.RefreshToken), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        UyeCerezYaz(result.Value!);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>D1/D6: çıkış — refresh oturumu iptal edilir (varsa) + SSR cookie'si silinir.
    /// Anonim erişilebilir: access token süresi dolmuş olsa da çıkış tamamlanabilmeli.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutMemberRequest? req, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(req?.RefreshToken))
            await mediator.Send(new RevokeMemberSessionCommand(req.RefreshToken), ct);
        UyeCerezSil();
        return Ok(new { success = true });
    }

    [HttpGet("me")]
    [Authorize(Policy = "MemberOnly")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());
        var result = await mediator.Send(new GetMemberDetailQuery(memberId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }
}

public record RegisterMemberRequest(string Email, string Password, string FirstName, string LastName, string? Phone = null);
public record LoginMemberRequest(string Email, string Password);
public record RefreshMemberRequest(string RefreshToken);
public record LogoutMemberRequest(string? RefreshToken = null);
