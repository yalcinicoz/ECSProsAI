using ECSPros.Storefront.Application.Commands.ModerateProductReview;
using ECSPros.Storefront.Application.Queries.GetReviewsForModeration;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>E7: yorum moderasyonu (admin) — kart/detay puanları yalnız approved
/// yorumlardan hesaplandığından onay kuyruğu yayının kapısıdır.</summary>
[ApiController]
[Route("api/reviews")]
[Authorize]
public class ReviewsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetForModeration(
        [FromQuery] string? status = "pending", [FromQuery] int page = 1, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetReviewsForModerationQuery(status, page, 20), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new ModerateProductReviewCommand(id, true), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectReviewRequest? req, CancellationToken ct)
    {
        var result = await mediator.Send(new ModerateProductReviewCommand(id, false, req?.Reason), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }
}

public record RejectReviewRequest(string? Reason);
