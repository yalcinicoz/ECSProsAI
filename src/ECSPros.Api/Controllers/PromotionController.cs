using ECSPros.Promotion.Application.Commands.CreateCampaign;
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

    /// <summary>Yeni kampanya oluşturur.</summary>
    [HttpPost("campaigns")]
    public async Task<IActionResult> CreateCampaign([FromBody] CreateCampaignRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateCampaignCommand(
            request.CampaignTypeId,
            request.Code,
            request.NameI18n,
            request.StartsAt,
            request.EndsAt,
            request.Priority,
            request.ProductSelectionType,
            request.Settings ?? new Dictionary<string, object>()), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/promotion/campaigns", new { success = true, data = new { id = result.Value } });
    }
}

public record CreateCampaignRequest(
    Guid CampaignTypeId,
    string Code,
    Dictionary<string, string> NameI18n,
    DateTime StartsAt,
    DateTime? EndsAt,
    int Priority,
    string ProductSelectionType,
    Dictionary<string, object>? Settings);
