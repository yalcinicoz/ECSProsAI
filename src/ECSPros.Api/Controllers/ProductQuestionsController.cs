using ECSPros.Storefront.Application.Commands.ProductQuestions;
using ECSPros.Storefront.Application.Queries.ProductQuestions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// Satıcıya Soru Sor — panel moderasyonu (2026-09-01): personel soruları görür,
/// cevaplar (cevap = yayına alma), yayından kaldırır/geri alır. Yorum moderasyonu
/// (/api/reviews) ile aynı yetki düzeyi: panel girişi yeterli.
/// </summary>
[ApiController]
[Route("api/product-questions")]
[Authorize]
public class ProductQuestionsController(IMediator mediator) : ControllerBase
{
    private Guid? UserId =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : null;

    /// <summary>Moderasyon listesi — bekleyenler en eski önce; status: pending|answered|hidden.</summary>
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] Guid? firmPlatformId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetQuestionsForModerationQuery(firmPlatformId, status, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Cevapla (yayına girer) — yayındaki cevap da bu uçla güncellenir.</summary>
    [HttpPost("{id:guid}/answer")]
    public async Task<IActionResult> Answer(Guid id, [FromBody] AnswerQuestionRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new AnswerProductQuestionCommand(id, req.Answer ?? "", UserId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = true });
    }

    /// <summary>Yayından kaldır (hidden=true) ya da geri yayınla (hidden=false, yalnız cevaplıysa).</summary>
    [HttpPost("{id:guid}/visibility")]
    public async Task<IActionResult> SetVisibility(Guid id, [FromBody] QuestionVisibilityRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new SetProductQuestionVisibilityCommand(id, req.Hidden), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = true });
    }
}

public record AnswerQuestionRequest(string? Answer);
public record QuestionVisibilityRequest(bool Hidden);
