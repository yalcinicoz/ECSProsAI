using System.Security.Claims;
using ECSPros.Storefront.Application.Commands.PublishPageSnapshot;
using ECSPros.Storefront.Application.Commands.RollbackPageSnapshot;
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
