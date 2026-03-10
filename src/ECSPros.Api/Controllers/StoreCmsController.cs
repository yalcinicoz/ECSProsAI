using ECSPros.Cms.Application.Queries.GetPageDetail;
using ECSPros.Cms.Application.Queries.GetPages;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/store/cms")]
public class StoreCmsController(IMediator mediator) : ControllerBase
{
    [HttpGet("pages")]
    public async Task<IActionResult> GetPages([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetPagesQuery(), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("pages/{id}")]
    public async Task<IActionResult> GetPage(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetPageDetailQuery(id), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }
}
