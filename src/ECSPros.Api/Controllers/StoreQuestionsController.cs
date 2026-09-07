using ECSPros.Shared.Infrastructure.Messaging;
using ECSPros.Storefront.Application.Commands.ProductQuestions;
using ECSPros.Storefront.Application.Queries.ProductQuestions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// Satıcıya Soru Sor (2026-09-01): üye ürün detayından soru sorar (pending doğar,
/// admin cevabıyla yayına girer — moderasyon /api/product-questions'ta). Yayındaki
/// (cevaplanmış) sorular herkese açıktır; ad maskeli anlık görüntüdür (E7 deseni).
/// </summary>
[ApiController]
[Route("api/store/questions")]
public class StoreQuestionsController(
    IMediator mediator,
    IRealtimeNotificationService realtime,
    ILogger<StoreQuestionsController> logger) : ControllerBase
{
    private Guid MemberId => Guid.Parse(
        User.FindFirst("sub")?.Value
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!);

    /// <summary>Ürünün yayındaki (cevaplanmış) soruları — ürün detay + mobil.</summary>
    /// <param name="productCode">Ürün kodu.</param>
    /// <param name="firmPlatformId">Zorunlu. Kanal kimliği.</param>
    /// <param name="limit">Dönen soru sayısı (varsayılan 20, en çok 50).</param>
    [HttpGet("product/{productCode}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetForProduct(
        string productCode, [FromQuery] Guid firmPlatformId, [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetProductQuestionsQuery(firmPlatformId, productCode, limit), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Hesabım → Sorularım: üyenin tüm soruları ve cevapları. B1 (2026-09-07): satırda ürün adı + görsel.</summary>
    [HttpGet("mine")]
    [Authorize(Policy = "MemberOnly")]
    public async Task<IActionResult> GetMine(
        [FromQuery] Guid firmPlatformId,
        [FromServices] ECSPros.Api.Services.Store.StoreKartZenginlestirici zengin = null!, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetMemberQuestionsQuery(firmPlatformId, MemberId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        var harita = await zengin.GetirAsync(firmPlatformId, result.Value!.Select(q => q.ProductCode), ct);
        var data = result.Value.Select(q =>
        {
            var o = ECSPros.Api.Services.Store.StoreKartZenginlestirici.Ozet(harita, q.ProductCode);
            return new ECSPros.Api.Models.Store.MemberQuestionItemDto(q.Id, q.ProductCode, q.Question, q.Answer, q.Status,
                q.MemberName, q.CreatedAt, q.AnsweredAt, o?.ProductName, o?.ImageUrl);
        }).ToList();
        return Ok(new { success = true, data });
    }

    /// <summary>Soru sor — aynı üründe cevap bekleyen sorunuz varken yenisi engellenir.</summary>
    [HttpPost]
    [Authorize(Policy = "MemberOnly")]
    public async Task<IActionResult> Create([FromBody] StoreQuestionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ProductCode))
            return BadRequest(new { success = false, error = "Ürün kodu zorunlu." });

        // Yayında ad maskeli görünür (E7 deseni): "Efe Kaya" → "E*** K***"
        var tamAd = User.FindFirst("full_name")?.Value ?? "Üye";
        var maskeli = string.Join(" ", tamAd.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Length <= 1 ? p : p[0] + new string('*', Math.Min(3, p.Length - 1))));

        var result = await mediator.Send(new CreateProductQuestionCommand(
            req.FirmPlatformId, MemberId, req.ProductCode.Trim(), req.Question ?? "", maskeli), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });

        // Panel anlık bildirimi (topic:questions) — hub hatası soru kaydını asla düşürmez.
        try
        {
            var soruMetni = (req.Question ?? "").Trim();
            await realtime.SendQuestionEventAsync("QuestionCreated", new
            {
                id = result.Value,
                productCode = req.ProductCode.Trim(),
                question = soruMetni.Length > 200 ? soruMetni[..200] + "…" : soruMetni,
                memberName = maskeli,
                createdAt = DateTime.UtcNow
            }, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Soru bildirimi hub'a gönderilemedi: {QuestionId}", result.Value);
        }

        return Ok(new { success = true, data = new { id = result.Value } });
    }
}

public record StoreQuestionRequest(Guid FirmPlatformId, string? ProductCode, string? Question);
