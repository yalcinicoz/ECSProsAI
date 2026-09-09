using ECSPros.Shared.Kernel.Authorization;
using ECSPros.Api.Authorization;
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
[RequirePermission(Permissions.IamUsersView)]   // Y2: sayfa yetkisi
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ─── Kişisel tercihler (DataGrid F5 — kaydedilmiş görünümler; Users.Preferences jsonb) ─────────────────────
    /// <summary>Giriş yapan kullanıcının panel tercihleri (tüm sözlük; örn. grids.orders.views).</summary>
    [HttpGet("users/me/preferences")]
    public async Task<IActionResult> GetMyPreferences(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid))
            return Unauthorized(new { success = false, error = "Kullanıcı kimliği okunamadı." });
        var result = await _mediator.Send(new ECSPros.Iam.Application.Queries.GetMyPreferences.GetMyPreferencesQuery(uid), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Tek tercih anahtarını yazar (value null → siler). Gövde: { key, value }.</summary>
    [HttpPut("users/me/preferences")]
    [RequirePermission(Permissions.IamUsersManage)]   // Y2
    public async Task<IActionResult> SetMyPreference([FromBody] SetMyPreferenceRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid))
            return Unauthorized(new { success = false, error = "Kullanıcı kimliği okunamadı." });
        var result = await _mediator.Send(new ECSPros.Iam.Application.Queries.GetMyPreferences.SetMyPreferenceCommand(uid, req.Key, req.Value), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    public record SetMyPreferenceRequest(string Key, System.Text.Json.JsonElement? Value);

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
        // DataGrid (2026-09-08): f.* filtreleri + sort/dir + role (UserGrid.Schema); eski parametreler korunur, sayfa boyu merkezi clamp
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, null /* kullanıcılar/denetim logları kanaldan bağımsızdır */, defaultPageSize: pageSize);
        var result = await _mediator.Send(new GetUsersQuery(search, activeOnly, grid.Page, grid.PageSize, grid), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kullanıcıları Excel'e aktarır (DataGrid): gövde search/sort/dir/filters/columns + named: activeOnly.</summary>
    [HttpPost("users/export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> ExportUsers([FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, [FromBody] ECSPros.Shared.Kernel.Grid.GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<UsersController> logger, CancellationToken ct)
    {
        var filters = new UserListFilters(body.Search, ECSPros.Api.Grid.GridExportEndpoint.Bayrak(body, "activeOnly") ?? false);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "users", "kullanicilar", "Kullanıcılar",
            ECSPros.Api.Grid.UserExportColumns.All, max => _mediator.Send(new ExportUsersQuery(filters, body.ToGridRequest(null /* kullanıcılar/denetim logları kanaldan bağımsızdır */), max), ct), ct, alanIzinleri: await alanYetkileri.IzinlerAsync(ct));
    }

    /// <summary>Yeni kullanıcı oluşturur.</summary>
    [HttpPost("users")]
    [RequirePermission(Permissions.IamUsersManage)]   // Y2
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
    [RequirePermission(Permissions.IamUsersManage)]   // Y2
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
    [RequirePermission(Permissions.IamUsersManage)]   // Y2
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ChangePasswordCommand(id, null, request.NewPassword, IsAdminReset: true), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }

    /// <summary>Kullanıcıya rol atar.</summary>
    [HttpPost("users/{id:guid}/roles")]
    [RequirePermission(Permissions.IamUsersManage)]   // Y2
    public async Task<IActionResult> AssignRole(Guid id, [FromBody] AssignRoleRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AssignRoleCommand(id, request.RoleId), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }

    // ─── Audit Logs ────────────────────────────────────────────────────────────

    /// <summary>Denetim loglarını sayfalı listeler (panel).</summary>
    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] Guid? userId, [FromQuery] string? entityType, [FromQuery] string? action,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        // DataGrid (2026-09-08): global arama + f.* filtreleri + sort/dir (AuditLogGrid.Schema); adlandırılmış parametreler korunur
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, null /* kullanıcılar/denetim logları kanaldan bağımsızdır */, defaultPageSize: pageSize);
        var result = await _mediator.Send(new GetAuditLogsQuery(
            userId, entityType, null, action, from, to, grid.Page, grid.PageSize, search, grid), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Denetim loglarını Excel'e aktarır (DataGrid): gövde search/sort/dir/filters/columns + named: userId/entityType/action/from/to.</summary>
    [HttpPost("audit-logs/export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> ExportAuditLogs([FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, [FromBody] ECSPros.Shared.Kernel.Grid.GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<UsersController> logger, CancellationToken ct)
    {
        var g = ECSPros.Api.Grid.GridExportEndpoint.Kimlik(body, "userId");
        var filters = new AuditLogListFilters(g, body.NamedValue("entityType"), null, body.NamedValue("action"),
            ECSPros.Api.Grid.GridExportEndpoint.Tarih(body, "from"), ECSPros.Api.Grid.GridExportEndpoint.Tarih(body, "to"), body.Search);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "audit-logs", "denetim-loglari", "Denetim Logları",
            ECSPros.Api.Grid.AuditLogExportColumns.All, max => _mediator.Send(new ExportAuditLogsQuery(filters, body.ToGridRequest(null /* kullanıcılar/denetim logları kanaldan bağımsızdır */), max), ct), ct, alanIzinleri: await alanYetkileri.IzinlerAsync(ct));
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
