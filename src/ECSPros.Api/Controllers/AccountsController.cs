using ECSPros.Shared.Kernel.Authorization;
using ECSPros.Shared.Kernel.Grid;
using ECSPros.Api.Authorization;
using ECSPros.Accounts.Application.Commands.AddAccountLedger;
using ECSPros.Accounts.Application.Commands.CreateAccountGroup;
using ECSPros.Accounts.Application.Commands.CreateCurrentAccount;
using ECSPros.Accounts.Application.Commands.UpdateAccountGroup;
using ECSPros.Accounts.Application.Commands.UpdateCurrentAccount;
using ECSPros.Accounts.Application.Queries.GetAccountGroups;
using ECSPros.Accounts.Application.Queries.GetCurrentAccountDetail;
using ECSPros.Accounts.Application.Queries.GetCurrentAccounts;
using ECSPros.Iam.Application.Commands.CreateSupplierUser;
using ECSPros.Iam.Application.Queries.GetSupplierUsers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/accounts")]
[Authorize]
[RequirePermission(Permissions.AccountsView)]   // Y2: sayfa yetkisi
public class AccountsController : ControllerBase
{
    private readonly IMediator _mediator;
    public AccountsController(IMediator mediator) => _mediator = mediator;

    // ── Groups ────────────────────────────────────────────────────────────────

    /// <summary>Cari gruplarını TAM liste olarak döner — cari listesinin grup süzgeci bunu bekler.
    /// ⚠ Sayfalanmaz; liste EKRANI için /groups/grid kullanın.</summary>
    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups([FromQuery] bool activeOnly = false, CancellationToken ct = default)
    {
        var r = await _mediator.Send(new GetAccountGroupsQuery(activeOnly), ct);
        return Ok(new { success = true, data = r.Value });
    }

    /// <summary>Cari grupları liste ekranı (DataGrid): sayfalı + f.* filtreleri + sort/dir.</summary>
    [HttpGet("groups/grid")]
    public async Task<IActionResult> GetGroupsGrid([FromQuery] bool activeOnly = false, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, null, defaultPageSize: 20);
        var r = await _mediator.Send(new GetAccountGroupsGridQuery(
            new AccountGroupFilters(activeOnly, search), grid.Page, grid.PageSize, grid), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Ok(new { success = true, data = r.Value });
    }

    /// <summary>Cari gruplarını Excel'e aktarır (DataGrid).</summary>
    [HttpPost("groups/export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> ExportGroups(
        [FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, [FromBody] GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<AccountsController> logger, CancellationToken ct)
    {
        var filters = new AccountGroupFilters(body.NamedValue("activeOnly") == "true", body.Search);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "account-groups", "cari-gruplari", "Cari Grupları",
            ECSPros.Api.Grid.AccountGroupExportColumns.All,
            max => _mediator.Send(new ExportAccountGroupsQuery(filters, body.ToGridRequest(null), max), ct),
            ct, alanIzinleri: await alanYetkileri.IzinlerAsync(ct));
    }

    [HttpPost("groups")]
    [RequirePermission(Permissions.AccountsManage)]   // Y2
    public async Task<IActionResult> CreateGroup([FromBody] CreateAccountGroupRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new CreateAccountGroupCommand(req.Code, req.Name, req.GroupType, req.Description, req.SortOrder), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Created("", new { success = true, data = new { id = r.Value } });
    }

    [HttpPut("groups/{id:guid}")]
    [RequirePermission(Permissions.AccountsManage)]   // Y2
    public async Task<IActionResult> UpdateGroup(Guid id, [FromBody] UpdateAccountGroupRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new UpdateAccountGroupCommand(id, req.Name, req.GroupType, req.Description, req.SortOrder, req.IsActive), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Ok(new { success = true });
    }

    // ── Current Accounts ──────────────────────────────────────────────────────

    /// <summary>Cari hesapları sayfalı listeler (DataGrid: f.* filtreleri + sort/dir + arama).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAccounts(
        [FromQuery] string? accountType, [FromQuery] Guid? groupId,
        [FromQuery] bool? isActive, [FromQuery] string? search, [FromQuery] string? ownerType,
        CancellationToken ct = default)
    {
        // Cari hesap kanaldan bağımsızdır (firma geneli muhasebe kaydı) → kanal kısıtı null.
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, null, defaultPageSize: 30);
        var r = await _mediator.Send(new GetCurrentAccountsQuery(
            accountType, groupId, isActive, search, grid.Page, grid.PageSize, ownerType, grid), ct);
        return Ok(new { success = true, data = r.Value });
    }

    /// <summary>Cari hesapları Excel'e aktarır (DataGrid): gövde search/sort/dir/filters/columns + named: accountType/ownerType/isActive.</summary>
    [HttpPost("export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> ExportAccounts(
        [FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, [FromBody] GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<AccountsController> logger, CancellationToken ct)
    {
        var aktif = body.NamedValue("isActive");
        var filters = new CurrentAccountFilters(
            body.NamedValue("accountType"), null,
            string.IsNullOrWhiteSpace(aktif) ? null : aktif == "true",
            body.Search, body.NamedValue("ownerType"));
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "accounts", "cari-hesaplar", "Cari Hesaplar",
            ECSPros.Api.Grid.CurrentAccountExportColumns.All,
            max => _mediator.Send(new ExportCurrentAccountsQuery(filters, body.ToGridRequest(null), max), ct),
            ct, alanIzinleri: await alanYetkileri.IzinlerAsync(ct));
    }

    /// <summary>Cari hesabın defterleri + sayfalı hareket dökümü.</summary>
    [HttpGet("{id:guid}/transactions")]
    public async Task<IActionResult> GetAccountTransactions(
        Guid id, [FromQuery] Guid? ledgerId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var r = await _mediator.Send(new ECSPros.Accounts.Application.Queries.GetAccountTransactions.GetAccountTransactionsQuery(id, ledgerId, page, pageSize), ct);
        if (r.IsFailure) return NotFound(new { success = false, error = r.Error });
        return Ok(new { success = true, data = r.Value });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAccountDetail(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetCurrentAccountDetailQuery(id), ct);
        if (r.IsFailure) return NotFound(new { success = false, error = r.Error });
        return Ok(new { success = true, data = r.Value });
    }

    [HttpPost]
    [RequirePermission(Permissions.AccountsManage)]   // Y2
    public async Task<IActionResult> CreateAccount([FromBody] CreateCurrentAccountRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new CreateCurrentAccountCommand(
            req.Code, req.Title, req.AccountType, req.GroupId,
            req.TaxNumber, req.TaxOffice, req.ContactName,
            req.Phone, req.Email, req.Address, req.City, req.Country,
            req.CreditLimit, req.Currency ?? "TRY", req.Notes, req.SupplierKind ?? "normal"), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Created("", new { success = true, data = new { id = r.Value } });
    }

    [HttpPost("{id:guid}/ledgers")]
    [RequirePermission(Permissions.AccountsManage)]   // Y2
    public async Task<IActionResult> AddLedger(Guid id, [FromBody] AddLedgerRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new AddAccountLedgerCommand(id, req.Currency, req.Description), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Created("", new { success = true, data = new { id = r.Value } });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.AccountsManage)]   // Y2
    public async Task<IActionResult> UpdateAccount(Guid id, [FromBody] UpdateCurrentAccountRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new UpdateCurrentAccountCommand(
            id, req.Title, req.AccountType, req.GroupId,
            req.TaxNumber, req.TaxOffice, req.ContactName,
            req.Phone, req.Email, req.Address, req.City, req.Country,
            req.CreditLimit, req.Currency ?? "TRY", req.Notes, req.IsActive, req.SupplierKind ?? "normal"), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Ok(new { success = true });
    }

    // ── Satıcı panel kullanıcıları (pazaryeri tedarikçisi) ─────────────────────
    // SupplierUser IAM modülünde; cari kartın marketplace olduğu doğrulaması burada
    // (Accounts sorgusu → IAM komutu orkestrasyonu) yapılır ki modüller ayrık kalsın.

    [HttpGet("{id:guid}/supplier-users")]
    public async Task<IActionResult> GetSupplierUsers(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetSupplierUsersQuery(id), ct);
        return Ok(new { success = true, data = r.Value });
    }

    [HttpPost("{id:guid}/supplier-users")]
    [RequirePermission(Permissions.AccountsManage)]   // Y2
    public async Task<IActionResult> CreateSupplierUser(Guid id, [FromBody] CreateSupplierUserRequest req, CancellationToken ct)
    {
        var acc = await _mediator.Send(new GetCurrentAccountDetailQuery(id), ct);
        if (acc.IsFailure) return NotFound(new { success = false, error = acc.Error });
        var a = acc.Value!;
        var isSupplier = a.AccountType is "supplier" or "both";
        if (!isSupplier || a.SupplierKind != "marketplace")
            return BadRequest(new { success = false, error = "Panel kullanıcısı yalnız pazaryeri satıcısı (marketplace tedarikçi) cari kartına açılabilir." });
        if (!a.IsActive)
            return BadRequest(new { success = false, error = "Pasif cari karta kullanıcı açılamaz." });

        var r = await _mediator.Send(new CreateSupplierUserCommand(id, req.Email, req.Password, req.FullName), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Created("", new { success = true, data = new { id = r.Value } });
    }
}

public record CreateSupplierUserRequest(string Email, string Password, string FullName);
public record AddLedgerRequest(string Currency, string? Description);
public record CreateAccountGroupRequest(string Code, string Name, string GroupType, string? Description, int SortOrder);
public record UpdateAccountGroupRequest(string Name, string GroupType, string? Description, int SortOrder, bool IsActive);
public record CreateCurrentAccountRequest(
    string Code, string Title, string AccountType, Guid? GroupId,
    string? TaxNumber, string? TaxOffice, string? ContactName,
    string? Phone, string? Email, string? Address, string? City, string? Country,
    decimal CreditLimit, string? Currency, string? Notes, string? SupplierKind);
public record UpdateCurrentAccountRequest(
    string Title, string AccountType, Guid? GroupId,
    string? TaxNumber, string? TaxOffice, string? ContactName,
    string? Phone, string? Email, string? Address, string? City, string? Country,
    decimal CreditLimit, string? Currency, string? Notes, bool IsActive, string? SupplierKind);
