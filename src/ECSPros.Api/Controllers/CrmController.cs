using ECSPros.Crm.Application.Commands.CreateMember;
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
