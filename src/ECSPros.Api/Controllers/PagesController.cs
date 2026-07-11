using System.Security.Claims;
using ECSPros.Storefront.Application.Commands.DeletePageBlock;
using ECSPros.Storefront.Application.Commands.PublishPageSnapshot;
using ECSPros.Storefront.Application.Commands.ReorderPageBlocks;
using ECSPros.Storefront.Application.Commands.RollbackPageSnapshot;
using ECSPros.Storefront.Application.Commands.SavePageBlock;
using ECSPros.Storefront.Application.Commands.SavePageBlockItems;
using ECSPros.Storefront.Application.Queries.GetPageBlockDetail;
using ECSPros.Storefront.Application.Queries.GetPageBlocks;
using ECSPros.Storefront.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// G4/G6: vitrin yayın yönetimi (admin). Blok/öğe CRUD endpoint'leri G6'da tamamlanır;
/// Yayınla + rollback + yayın geçmişi burada — canlı okuma tarafının (G4) sözleşmesi
/// bu komutlarla üretilen snapshot'lardır.
/// </summary>
[ApiController]
[Route("api/pages")]
[Authorize]
public class PagesController(IMediator mediator, ECSPros.Storefront.Application.Services.IStorefrontDbContext db) : ControllerBase
{
    public record PublishRequest(Guid FirmPlatformId, string? Note);
    public record RollbackRequest(Guid FirmPlatformId, int TargetVersion, string? Note);
    public record BlockRequest(
        Guid FirmPlatformId, string Placement, string BlockType, string? Template,
        Dictionary<string, string> TitleI18n, Dictionary<string, string>? SubtitleI18n,
        int SortOrder, bool IsActive, DateTime? StartAt, DateTime? EndAt, int Priority,
        string? RuleJson, string? ConfigJson);
    public record ReorderRequest(Guid FirmPlatformId, string Placement, List<Guid> OrderedIds);
    public record ItemsRequest(Guid FirmPlatformId, List<PageBlockItemInput> Items);

    /// <summary>Blok paleti — admin dropdown'ları PageBlockCatalog'dan beslenir (G2 tek kaynak).</summary>
    [HttpGet("catalog")]
    public IActionResult Catalog() => Ok(new
    {
        success = true,
        data = new
        {
            placements = PageBlockCatalog.Placements.Select(p => new { code = p.Code, displayName = p.DisplayName }),
            blockTypes = PageBlockCatalog.BlockTypes.Select(t => new
            {
                code = t.Code,
                displayName = t.DisplayName,
                ruleLevel = t.Rules.ToString(),
                supportsItems = t.SupportsItems,
                templates = t.Templates,
                requiresProductSource = t.RequiresProductSource,
                requiresCollectionSource = t.RequiresCollectionSource,
            }),
            carouselThemes = PageBlockCatalog.CarouselThemes,
        },
    });

    [HttpGet("blocks")]
    public async Task<IActionResult> GetBlocks(
        [FromQuery] Guid firmPlatformId, [FromQuery] string? placement, CancellationToken ct)
    {
        var result = await mediator.Send(new GetPageBlocksQuery(firmPlatformId, placement), ct);
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("blocks/{id:guid}")]
    public async Task<IActionResult> GetBlock(Guid id, [FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetPageBlockDetailQuery(id, firmPlatformId), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("blocks")]
    public async Task<IActionResult> CreateBlock([FromBody] BlockRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(BlokKomutu(null, req), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/pages/blocks/{result.Value}", new { success = true, data = new { id = result.Value } });
    }

    [HttpPut("blocks/{id:guid}")]
    public async Task<IActionResult> UpdateBlock(Guid id, [FromBody] BlockRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(BlokKomutu(id, req), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { id = result.Value } });
    }

    [HttpDelete("blocks/{id:guid}")]
    public async Task<IActionResult> DeleteBlock(Guid id, [FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        var result = await mediator.Send(new DeletePageBlockCommand(id, firmPlatformId), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpPut("blocks/order")]
    public async Task<IActionResult> ReorderBlocks([FromBody] ReorderRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new ReorderPageBlocksCommand(req.FirmPlatformId, req.Placement, req.OrderedIds), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Öğe listesi replace (SaveNavNodes deseni — editör tam listeyi gönderir).</summary>
    [HttpPut("blocks/{id:guid}/items")]
    public async Task<IActionResult> SaveItems(Guid id, [FromBody] ItemsRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new SavePageBlockItemsCommand(id, req.FirmPlatformId, req.Items), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    private static SavePageBlockCommand BlokKomutu(Guid? id, BlockRequest req) => new(
        id, req.FirmPlatformId, req.Placement, req.BlockType, req.Template,
        req.TitleI18n, req.SubtitleI18n, req.SortOrder, req.IsActive,
        req.StartAt, req.EndAt, req.Priority, req.RuleJson, req.ConfigJson);

    public record PreviewRequest(
        Guid FirmPlatformId, string Placement, string? City, string? Gender,
        string? Device, bool IsMember, Guid? MemberGroupId);

    /// <summary>
    /// G12: önizleme — TASLAK veri + kurgu segment üzerinde kural motorunu çalıştırır,
    /// blokları görünür/gizli + nedeniyle listeler (spec: canlı siteyi etkilemez, cache'e
    /// yazmaz; yayınlanmamış değişiklikler burada görünür).
    /// </summary>
    [HttpPost("preview")]
    public async Task<IActionResult> Preview(
        [FromBody] PreviewRequest req,
        [FromServices] ECSPros.Api.Services.Store.IPagePreviewService preview,
        [FromServices] ECSPros.Api.Services.Store.IVisitorSegmentResolver segmentResolver,
        CancellationToken ct)
    {
        if (!PageBlockCatalog.IsValidPlacement(req.Placement))
            return BadRequest(new { success = false, error = "Geçersiz yerleşim." });
        if (req.FirmPlatformId == Guid.Empty)
            return BadRequest(new { success = false, error = "firmPlatformId zorunlu." });

        var segment = await segmentResolver.BuildAsync(
            req.City, req.Gender, req.Device, req.IsMember, req.MemberGroupId, ct);
        var bloklar = await preview.PreviewAsync(req.FirmPlatformId, req.Placement, segment, ct);
        return Ok(new
        {
            success = true,
            data = new
            {
                segment = new
                {
                    city = segment.CityCode,
                    cityName = segment.CityName,
                    region = segment.Region,
                    gender = segment.Gender,
                    device = segment.Device,
                    membership = segment.IsMember ? "member" : "guest",
                    memberGroup = segment.MemberGroupId,
                },
                blocks = bloklar,
            },
        });
    }

    [HttpPost("publish")]
    public async Task<IActionResult> Publish([FromBody] PublishRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new PublishPageSnapshotCommand(
            request.FirmPlatformId, KullaniciId(), request.Note), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { version = result.Value } });
    }

    [HttpPost("rollback")]
    public async Task<IActionResult> Rollback([FromBody] RollbackRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new RollbackPageSnapshotCommand(
            request.FirmPlatformId, request.TargetVersion, KullaniciId(), request.Note), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Yayın geçmişi — spec PublishLog listesi (yeniden eskiye).</summary>
    [HttpGet("publish-logs")]
    public IActionResult PublishLogs([FromQuery] Guid firmPlatformId, [FromQuery] int limit = 50)
    {
        var logs = db.PublishLogs
            .Where(l => l.FirmPlatformId == firmPlatformId)
            .OrderByDescending(l => l.PublishedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(l => new { l.Id, l.Version, l.PreviousVersion, l.PublishedBy, l.PublishedAt, l.Status, l.ErrorMessage, l.Note })
            .ToList();
        return Ok(new { success = true, data = logs });
    }

    /// <summary>Snapshot versiyonları (rollback seçim listesi) — JsonData listede taşınmaz.</summary>
    [HttpGet("snapshots")]
    public IActionResult Snapshots([FromQuery] Guid firmPlatformId, [FromQuery] int limit = 50)
    {
        var versiyonlar = db.PublishedSnapshots
            .Where(s => s.FirmPlatformId == firmPlatformId)
            .OrderByDescending(s => s.Version)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(s => new { s.Id, s.Version, s.PublishedAt, s.PublishedBy, s.IsActive, s.Status, s.Note })
            .ToList();
        return Ok(new { success = true, data = versiyonlar });
    }

    private Guid? KullaniciId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
            ? id : null;
}
