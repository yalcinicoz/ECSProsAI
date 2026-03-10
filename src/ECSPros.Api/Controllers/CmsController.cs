using ECSPros.Cms.Application.Commands.CreatePage;
using ECSPros.Cms.Application.Commands.UpdatePage;
using ECSPros.Cms.Application.Queries.GetPageDetail;
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

    /// <summary>CMS sayfası detayını döner.</summary>
    [HttpGet("pages/{id:guid}")]
    public async Task<IActionResult> GetPageDetail(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPageDetailQuery(id), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni CMS sayfası oluşturur.</summary>
    [HttpPost("pages")]
    public async Task<IActionResult> CreatePage([FromBody] CreatePageRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreatePageCommand(
            request.FirmPlatformId,
            request.TemplateId,
            request.Code,
            request.NameI18n,
            request.SlugI18n,
            request.PageType,
            request.TargetGender,
            request.TargetCategoryId,
            request.PublishAt,
            request.UnpublishAt), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/cms/pages", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>CMS sayfasını günceller.</summary>
    [HttpPut("pages/{id:guid}")]
    public async Task<IActionResult> UpdatePage(Guid id, [FromBody] UpdatePageRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var result = await _mediator.Send(new UpdatePageCommand(
            id,
            request.NameI18n,
            request.SlugI18n,
            request.MetaTitleI18n,
            request.MetaDescriptionI18n,
            request.IsActive,
            request.PublishAt,
            request.UnpublishAt,
            request.TargetGender,
            userId), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }
}

public record CreatePageRequest(
    Guid FirmPlatformId,
    Guid TemplateId,
    string Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string> SlugI18n,
    string PageType,
    string? TargetGender,
    Guid? TargetCategoryId,
    DateTime? PublishAt,
    DateTime? UnpublishAt);

public record UpdatePageRequest(
    Dictionary<string, string> NameI18n,
    Dictionary<string, string> SlugI18n,
    Dictionary<string, string>? MetaTitleI18n,
    Dictionary<string, string>? MetaDescriptionI18n,
    bool IsActive,
    DateTime? PublishAt,
    DateTime? UnpublishAt,
    string? TargetGender);
