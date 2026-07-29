using System.Security.Claims;
using ECSPros.Requests.Application.Commands.AddRequestComment;
using ECSPros.Requests.Application.Commands.AssignRequest;
using ECSPros.Requests.Application.Commands.ChangeRequestStatus;
using ECSPros.Requests.Application.Commands.CreateRequest;
using ECSPros.Requests.Application.Commands.UpdateRequest;
using ECSPros.Requests.Application.Queries.GetRequestDetail;
using ECSPros.Requests.Application.Queries.GetRequests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// Proje talepleri (2026-07-23): personelin proje ile ilgili isteklerinin girildiği,
/// süreçlerinin izlendiği modül. Düz [Authorize] = F0 sonrası yalnız panel (admin)
/// kullanıcısı; v1'de ayrı granüler izin yok — tüm personel görür/girer/günceller.
/// </summary>
[ApiController]
[Route("api/requests")]
[Authorize]
public class RequestsController(IMediator mediator) : ControllerBase
{
    private (Guid Id, string Ad) MevcutKullanici()
    {
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id);
        var ad = User.FindFirstValue("full_name") ?? User.FindFirstValue(ClaimTypes.Email) ?? "Bilinmeyen";
        return (id, ad);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null, [FromQuery] string? category = null,
        [FromQuery] string? priority = null, [FromQuery] Guid? assignedTo = null,
        [FromQuery] Guid? requestedBy = null, [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetRequestsQuery(
            page, pageSize, status, category, priority, assignedTo, requestedBy, search), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetRequestDetailQuery(id), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    public record CreateRequestBody(
        string Title, string Description, string Category, string Priority,
        DateOnly? DueDate, List<string>? Attachments);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequestBody body, CancellationToken ct)
    {
        var (userId, userName) = MevcutKullanici();
        var result = await mediator.Send(new CreateRequestCommand(
            body.Title, body.Description ?? string.Empty, body.Category, body.Priority,
            body.DueDate, body.Attachments, userId, userName), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { id = result.Value } });
    }

    public record UpdateRequestBody(
        string Title, string Description, string Category, string Priority, DateOnly? DueDate);

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRequestBody body, CancellationToken ct)
    {
        var (userId, userName) = MevcutKullanici();
        var result = await mediator.Send(new UpdateRequestCommand(
            id, body.Title, body.Description ?? string.Empty, body.Category, body.Priority,
            body.DueDate, userId, userName), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    public record ChangeStatusBody(string Status, string? Comment);

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeStatusBody body, CancellationToken ct)
    {
        var (userId, userName) = MevcutKullanici();
        var result = await mediator.Send(new ChangeRequestStatusCommand(
            id, body.Status, body.Comment, userId, userName), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    public record AssignBody(Guid? AssignedTo, string? AssignedToName);

    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignBody body, CancellationToken ct)
    {
        var (userId, userName) = MevcutKullanici();
        var result = await mediator.Send(new AssignRequestCommand(
            id, body.AssignedTo, body.AssignedToName, userId, userName), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    public record CommentBody(string? Comment, List<string>? Attachments);

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] CommentBody body, CancellationToken ct)
    {
        var (userId, userName) = MevcutKullanici();
        var result = await mediator.Send(new AddRequestCommentCommand(
            id, body.Comment, body.Attachments, userId, userName), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Talep eki yükleme — vitrin görsel yükleme deseninin kopyası
    /// (PagesController.UploadMedia); dosyalar media/talepler/yyyyMM altına yazılır.</summary>
    [HttpPost("media")]
    [RequestSizeLimit(11_000_000)]
    public async Task<IActionResult> UploadMedia(
        IFormFile? file, [FromServices] IConfiguration configuration, CancellationToken ct)
    {
        var uzantilar = new Dictionary<string, string>
        {
            ["image/jpeg"] = ".jpg", ["image/png"] = ".png", ["image/webp"] = ".webp",
            ["image/gif"] = ".gif", ["application/pdf"] = ".pdf",
        };
        if (file is null || file.Length == 0)
            return BadRequest(new { success = false, error = "Dosya gönderilmedi." });
        if (file.Length > 10_000_000)
            return BadRequest(new { success = false, error = "Dosya en fazla 10 MB olabilir." });
        if (!uzantilar.TryGetValue(file.ContentType, out var uzanti))
            return BadRequest(new { success = false, error = "Yalnızca JPEG, PNG, WebP, GIF veya PDF yükleyebilirsiniz." });

        var kok = configuration["Store:MediaRootPath"] ?? "/opt/ECSProsAI/media";
        var altDizin = Path.Combine("talepler", DateTime.UtcNow.ToString("yyyyMM"));
        Directory.CreateDirectory(Path.Combine(kok, altDizin));
        var ad = $"{Guid.NewGuid():N}{uzanti}";
        await using (var hedef = System.IO.File.Create(Path.Combine(kok, altDizin, ad)))
            await file.CopyToAsync(hedef, ct);

        return Ok(new { success = true, data = new { url = $"/media/{altDizin.Replace(Path.DirectorySeparatorChar, '/')}/{ad}" } });
    }
}
