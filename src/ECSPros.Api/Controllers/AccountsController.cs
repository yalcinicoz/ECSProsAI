using ECSPros.Accounts.Application.Commands.AddAccountLedger;
using ECSPros.Accounts.Application.Commands.CreateAccountGroup;
using ECSPros.Accounts.Application.Commands.CreateCurrentAccount;
using ECSPros.Accounts.Application.Commands.UpdateAccountGroup;
using ECSPros.Accounts.Application.Commands.UpdateCurrentAccount;
using ECSPros.Accounts.Application.Queries.GetAccountGroups;
using ECSPros.Accounts.Application.Queries.GetCurrentAccountDetail;
using ECSPros.Accounts.Application.Queries.GetCurrentAccounts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/accounts")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IMediator _mediator;
    public AccountsController(IMediator mediator) => _mediator = mediator;

    // ── Groups ────────────────────────────────────────────────────────────────

    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups([FromQuery] bool activeOnly = false, CancellationToken ct = default)
    {
        var r = await _mediator.Send(new GetAccountGroupsQuery(activeOnly), ct);
        return Ok(new { success = true, data = r.Value });
    }

    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateAccountGroupRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new CreateAccountGroupCommand(req.Code, req.Name, req.GroupType, req.Description, req.SortOrder), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Created("", new { success = true, data = new { id = r.Value } });
    }

    [HttpPut("groups/{id:guid}")]
    public async Task<IActionResult> UpdateGroup(Guid id, [FromBody] UpdateAccountGroupRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new UpdateAccountGroupCommand(id, req.Name, req.GroupType, req.Description, req.SortOrder, req.IsActive), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Ok(new { success = true });
    }

    // ── Current Accounts ──────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAccounts(
        [FromQuery] string? accountType, [FromQuery] Guid? groupId,
        [FromQuery] bool? isActive, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default)
    {
        var r = await _mediator.Send(new GetCurrentAccountsQuery(accountType, groupId, isActive, search, page, pageSize), ct);
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
    public async Task<IActionResult> CreateAccount([FromBody] CreateCurrentAccountRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new CreateCurrentAccountCommand(
            req.Code, req.Title, req.AccountType, req.GroupId,
            req.TaxNumber, req.TaxOffice, req.ContactName,
            req.Phone, req.Email, req.Address, req.City, req.Country,
            req.CreditLimit, req.Currency ?? "TRY", req.Notes), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Created("", new { success = true, data = new { id = r.Value } });
    }

    [HttpPost("{id:guid}/ledgers")]
    public async Task<IActionResult> AddLedger(Guid id, [FromBody] AddLedgerRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new AddAccountLedgerCommand(id, req.Currency, req.Description), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Created("", new { success = true, data = new { id = r.Value } });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAccount(Guid id, [FromBody] UpdateCurrentAccountRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new UpdateCurrentAccountCommand(
            id, req.Title, req.AccountType, req.GroupId,
            req.TaxNumber, req.TaxOffice, req.ContactName,
            req.Phone, req.Email, req.Address, req.City, req.Country,
            req.CreditLimit, req.Currency ?? "TRY", req.Notes, req.IsActive), ct);
        if (r.IsFailure) return BadRequest(new { success = false, error = r.Error });
        return Ok(new { success = true });
    }
}

public record AddLedgerRequest(string Currency, string? Description);
public record CreateAccountGroupRequest(string Code, string Name, string GroupType, string? Description, int SortOrder);
public record UpdateAccountGroupRequest(string Name, string GroupType, string? Description, int SortOrder, bool IsActive);
public record CreateCurrentAccountRequest(
    string Code, string Title, string AccountType, Guid? GroupId,
    string? TaxNumber, string? TaxOffice, string? ContactName,
    string? Phone, string? Email, string? Address, string? City, string? Country,
    decimal CreditLimit, string? Currency, string? Notes);
public record UpdateCurrentAccountRequest(
    string Title, string AccountType, Guid? GroupId,
    string? TaxNumber, string? TaxOffice, string? ContactName,
    string? Phone, string? Email, string? Address, string? City, string? Country,
    decimal CreditLimit, string? Currency, string? Notes, bool IsActive);
