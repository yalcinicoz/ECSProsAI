using ECSPros.Cms.Application.Queries.GetPages;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/cms")]
[Authorize]
public class CmsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CmsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>CMS sayfalarını listeler.</summary>
    [HttpGet("pages")]
    public async Task<IActionResult> GetPages(
        [FromQuery] Guid? firmPlatformId,
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPagesQuery(firmPlatformId, activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }
}
