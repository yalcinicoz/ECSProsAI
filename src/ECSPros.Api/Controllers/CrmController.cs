using ECSPros.Crm.Application.Commands.CreateMember;
using ECSPros.Crm.Application.Commands.UpdateMember;
using ECSPros.Crm.Application.Queries.GetMemberDetail;
using ECSPros.Crm.Application.Queries.GetMembers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/crm")]
[Authorize]
public class CrmController : ControllerBase
{
    private readonly IMediator _mediator;

    public CrmController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Üyeleri sayfalı listeler.</summary>
    [HttpGet("members")]
    public async Task<IActionResult> GetMembers(
        [FromQuery] string? search,
        [FromQuery] bool activeOnly = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetMembersQuery(search, activeOnly, page, pageSize), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Üye detayını döner.</summary>
    [HttpGet("members/{id:guid}")]
    public async Task<IActionResult> GetMemberDetail(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMemberDetailQuery(id), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni üye oluşturur.</summary>
    [HttpPost("members")]
    public async Task<IActionResult> CreateMember([FromBody] CreateMemberRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateMemberCommand(
            request.MemberGroupId, request.FirstName, request.LastName,
            request.Email, request.Phone, request.Password,
            request.Gender, request.BirthDate,
            request.CompanyName, request.TaxNumber, request.TaxOffice), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/crm/members/{result.Value}", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Üye bilgilerini günceller.</summary>
    [HttpPut("members/{id:guid}")]
    public async Task<IActionResult> UpdateMember(Guid id, [FromBody] UpdateMemberRequest request, CancellationToken ct)
    {
        var updatedBy = Guid.TryParse(User.FindFirst("sub")?.Value, out var uid) ? uid : Guid.Empty;

        var result = await _mediator.Send(new UpdateMemberCommand(
            id, request.FirstName, request.LastName,
            request.Email, request.Phone, request.Gender, request.BirthDate,
            request.TaxOffice, request.TaxNumber, request.CompanyName,
            request.IsActive, request.MemberGroupId, updatedBy), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }
}

public record CreateMemberRequest(
    Guid MemberGroupId,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? Password,
    string? Gender,
    DateOnly? BirthDate,
    string? CompanyName,
    string? TaxNumber,
    string? TaxOffice);

public record UpdateMemberRequest(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? Gender,
    DateOnly? BirthDate,
    string? TaxOffice,
    string? TaxNumber,
    string? CompanyName,
    bool IsActive,
    Guid? MemberGroupId);
