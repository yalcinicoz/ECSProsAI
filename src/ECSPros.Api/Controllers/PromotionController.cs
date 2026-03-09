using ECSPros.Promotion.Application.Queries.GetCampaigns;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/promotion")]
[Authorize]
public class PromotionController : ControllerBase
{
    private readonly IMediator _mediator;

    public PromotionController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Kampanyaları listeler.</summary>
    [HttpGet("campaigns")]
    public async Task<IActionResult> GetCampaigns(
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCampaignsQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }
}
