using ECSPros.Storefront.Application.Commands.ModerateCollection;
using ECSPros.Storefront.Application.Queries.GetCollectionsForModeration;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>E6: koleksiyon moderasyonu (admin) — onay ekranı zorunlu (plan Bölüm 7/20):
/// Faz G "Koleksiyonlar bloğu" yalnız approved+public koleksiyonları gösterebilir.</summary>
[ApiController]
[Route("api/collections")]
[Authorize]
public class CollectionsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetForModeration(
        [FromQuery] string? status = "pending", [FromQuery] int page = 1, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetCollectionsForModerationQuery(status, page, 20), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new ModerateCollectionCommand(id, true), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new ModerateCollectionCommand(id, false), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }
}
