using ECSPros.Cms.Application.Commands.CopyPageContent;
using ECSPros.Cms.Application.Commands.CreatePage;
using ECSPros.Cms.Application.Commands.ManageSectionItems;
using ECSPros.Cms.Application.Commands.UpdatePage;
using ECSPros.Cms.Application.Commands.UpdateSectionContent;
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
        [FromQuery] string? pageType = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPagesQuery(firmPlatformId, activeOnly, pageType), ct);
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

    // ─── P2b: bölüm içeriği + SSS öğeleri + platforma kopyalama ───────────────

    private Guid CurrentUserId =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out var uid) ? uid : Guid.Empty;

    /// <summary>rich_text bölümünün HTML içeriğini günceller (P2b).</summary>
    [HttpPut("sections/{id:guid}/content")]
    public async Task<IActionResult> UpdateSectionContent(Guid id, [FromBody] UpdateSectionContentRequest request, CancellationToken ct)
    {
        if (CurrentUserId == Guid.Empty) return Unauthorized();
        var result = await _mediator.Send(new UpdateSectionContentCommand(id, request.Html ?? "", CurrentUserId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Bölüme SSS öğesi ekler (soru=title, cevap=description) (P2b).</summary>
    [HttpPost("sections/{id:guid}/items")]
    public async Task<IActionResult> CreateSectionItem(Guid id, [FromBody] SectionItemRequest request, CancellationToken ct)
    {
        if (CurrentUserId == Guid.Empty) return Unauthorized();
        var result = await _mediator.Send(new CreateSectionItemCommand(
            id, request.TitleI18n, request.DescriptionI18n, request.SortOrder, CurrentUserId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/cms/sections/{id}/items", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>SSS öğesini günceller (P2b).</summary>
    [HttpPut("section-items/{id:guid}")]
    public async Task<IActionResult> UpdateSectionItem(Guid id, [FromBody] SectionItemRequest request, CancellationToken ct)
    {
        if (CurrentUserId == Guid.Empty) return Unauthorized();
        var result = await _mediator.Send(new UpdateSectionItemCommand(
            id, request.TitleI18n, request.DescriptionI18n, request.SortOrder, request.IsActive, CurrentUserId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>SSS öğesini siler (soft delete) (P2b).</summary>
    [HttpDelete("section-items/{id:guid}")]
    public async Task<IActionResult> DeleteSectionItem(Guid id, CancellationToken ct)
    {
        if (CurrentUserId == Guid.Empty) return Unauthorized();
        var result = await _mediator.Send(new DeleteSectionItemCommand(id, CurrentUserId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Sayfa içeriğini aynı Code'lu diğer platform sayfalarına kopyalar (P2b, K20).</summary>
    [HttpPost("pages/{id:guid}/copy-content")]
    public async Task<IActionResult> CopyPageContent(Guid id, [FromBody] CopyPageContentRequest request, CancellationToken ct)
    {
        if (CurrentUserId == Guid.Empty) return Unauthorized();
        var result = await _mediator.Send(new CopyPageContentCommand(id, request.TargetPageIds, CurrentUserId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { copiedSections = result.Value } });
    }
}

public record UpdateSectionContentRequest(string? Html);
public record SectionItemRequest(
    Dictionary<string, string> TitleI18n,
    Dictionary<string, string> DescriptionI18n,
    int SortOrder = 0,
    bool IsActive = true);
public record CopyPageContentRequest(List<Guid> TargetPageIds);

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
