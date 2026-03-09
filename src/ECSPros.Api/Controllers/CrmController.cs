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
}
