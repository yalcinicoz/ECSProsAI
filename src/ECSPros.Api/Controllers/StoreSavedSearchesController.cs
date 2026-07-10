using ECSPros.Storefront.Application.Commands.CreateSavedSearch;
using ECSPros.Storefront.Application.Commands.DeleteSavedSearch;
using ECSPros.Storefront.Application.Commands.UpdateSavedSearch;
using ECSPros.Storefront.Application.Queries.GetMemberSavedSearches;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// E11: Favori Aramalarım — üyenin kayıtlı aramaları. Aynı arama metni platform
/// başına bir kez kaydedilir; çalıştırma /urunler?search=... ile (istemci tarafı).
/// Bildirim gönderimi Faz H'de (NotifyEnabled o güne dek yalnız rozet).
/// </summary>
[ApiController]
[Route("api/store/saved-searches")]
[Authorize(Policy = "MemberOnly")]
public class StoreSavedSearchesController(IMediator mediator) : ControllerBase
{
    private Guid MemberId => Guid.Parse(
        User.FindFirst("sub")?.Value
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!);

    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetMemberSavedSearchesQuery(firmPlatformId, MemberId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SavedSearchRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateSavedSearchCommand(
            req.FirmPlatformId, MemberId, req.Query, req.Name, req.Filters, req.NotifyEnabled), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPut("{savedSearchId}")]
    public async Task<IActionResult> Update(Guid savedSearchId, [FromBody] SavedSearchRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateSavedSearchCommand(
            savedSearchId, MemberId, req.Query, req.Name, req.NotifyEnabled), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpDelete("{savedSearchId}")]
    public async Task<IActionResult> Delete(Guid savedSearchId, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteSavedSearchCommand(savedSearchId, MemberId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }
}

public record SavedSearchRequest(
    Guid FirmPlatformId,
    string Query,
    string? Name = null,
    Dictionary<string, string>? Filters = null,
    bool NotifyEnabled = false);
