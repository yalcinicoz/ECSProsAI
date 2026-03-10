using ECSPros.Iam.Application.Commands.AssignRole;
using ECSPros.Iam.Application.Commands.ChangePassword;
using ECSPros.Iam.Application.Commands.CreateAdminMenu;
using ECSPros.Iam.Application.Commands.CreateUser;
using ECSPros.Iam.Application.Commands.RevokeAllUserSessions;
using ECSPros.Iam.Application.Commands.RevokeSession;
using ECSPros.Iam.Application.Commands.UpdateAdminMenu;
using ECSPros.Iam.Application.Commands.UpdateUser;
using ECSPros.Iam.Application.Queries.GetAdminMenus;
using ECSPros.Iam.Application.Queries.GetAuditLogs;
using ECSPros.Iam.Application.Queries.GetRoles;
using ECSPros.Iam.Application.Queries.GetUserDetail;
using ECSPros.Iam.Application.Queries.GetUserSessions;
using ECSPros.Iam.Application.Queries.GetUsers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/iam")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ─── Users ─────────────────────────────────────────────────────────────────

    /// <summary>Kullanıcıları sayfalı listeler.</summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search,
        [FromQuery] bool activeOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetUsersQuery(search, activeOnly, page, pageSize), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni kullanıcı oluşturur.</summary>
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateUserCommand(
            request.Username, request.Email, request.Password,
            request.FirstName, request.LastName, request.Department,
            request.JobTitle, request.Phone, request.MustChangePassword), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/iam/users/{result.Value}", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Kullanıcı bilgilerini günceller.</summary>
    [HttpPut("users/{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var updatedBy = Guid.TryParse(User.FindFirst("sub")?.Value, out var uid) ? uid : Guid.Empty;

        var result = await _mediator.Send(new UpdateUserCommand(
            id, request.FirstName, request.LastName, request.Department,
            request.JobTitle, request.Phone, request.IsActive, updatedBy), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }

    /// <summary>Kullanıcı şifresini değiştirir (admin reset).</summary>
    [HttpPost("users/{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ChangePasswordCommand(id, null, request.NewPassword, IsAdminReset: true), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }

    /// <summary>Kullanıcıya rol atar.</summary>
    [HttpPost("users/{id:guid}/roles")]
    public async Task<IActionResult> AssignRole(Guid id, [FromBody] AssignRoleRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AssignRoleCommand(id, request.RoleId), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }

    // ─── Roles ─────────────────────────────────────────────────────────────────

    /// <summary>Rolleri listeler.</summary>
    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRolesQuery(), ct);
        return Ok(new { success = true, data = result.Value });
    }
}

public record CreateUserRequest(
    string Username,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Department,
    string? JobTitle,
    string? Phone,
    bool MustChangePassword = true);

public record UpdateUserRequest(
    string FirstName,
    string LastName,
    string? Department,
    string? JobTitle,
    string? Phone,
    bool IsActive);

public record ResetPasswordRequest(string NewPassword);

public record AssignRoleRequest(Guid RoleId);

public record CreateAdminMenuRequest(
    Guid? ParentId,
    string Code,
    Dictionary<string, string> NameI18n,
    string? Icon,
    string? Route,
    string? PermissionCode,
    int SortOrder = 0
);

public record UpdateAdminMenuRequest(
    Dictionary<string, string> NameI18n,
    string? Icon,
    string? Route,
    string? PermissionCode,
    int SortOrder,
    bool IsActive
);
