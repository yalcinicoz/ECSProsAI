using ECSPros.Shared.Kernel.Authorization;
using ECSPros.Shared.Kernel.Grid;
using ECSPros.Api.Authorization;
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
[RequirePermission(Permissions.StorefrontModerationView)]   // Y2: sayfa yetkisi
public class ReviewsController(IMediator mediator) : ControllerBase
{
    /// <summary>Moderasyon kuyruğu (DataGrid: f.* filtreleri + sort/dir + arama). Y3: kanal kapsamı uygulanır.</summary>
    [HttpGet]
    public async Task<IActionResult> GetForModeration([FromServices] ECSPros.Api.Authorization.IKanalKapsami kanalKapsami,
        [FromQuery] string? status = "pending", [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var kapsam = await kanalKapsami.KanallarAsync(Permissions.StorefrontModerationView, ct);
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, kapsam, defaultPageSize: 20);
        var result = await mediator.Send(new GetReviewsForModerationQuery(
            status, grid.Page, grid.PageSize, kapsam, search, grid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yorumları Excel'e aktarır (DataGrid): gövde search/sort/dir/filters/columns + named: status.
    /// Y3: kapsam gövdeden DEĞİL, kullanıcının yetkisinden çözülür.</summary>
    [HttpPost("export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> Export(
        [FromServices] ECSPros.Api.Authorization.IKanalKapsami kanalKapsami,
        [FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, [FromBody] GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<ReviewsController> logger, CancellationToken ct)
    {
        var kapsam = await kanalKapsami.KanallarAsync(Permissions.StorefrontModerationView, ct);
        var filters = new ReviewModerationFilters(body.NamedValue("status"), body.Search);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "reviews", "yorumlar", "Yorumlar",
            ECSPros.Api.Grid.ReviewModerationExportColumns.All,
            max => mediator.Send(new ExportReviewsForModerationQuery(filters, body.ToGridRequest(kapsam), max), ct),
            ct, alanIzinleri: await alanYetkileri.IzinlerAsync(ct));
    }

    [HttpPost("{id}/approve")]
    [RequirePermission(Permissions.StorefrontModerationManage)]   // Y2
    public async Task<IActionResult> Approve(Guid id, [FromServices] ECSPros.Api.Services.Push.PushEtkilesim push, CancellationToken ct)
    {
        var result = await mediator.Send(new ModerateProductReviewCommand(id, true), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        await push.YorumModereEdildiAsync(id, true, ct);
        return Ok(new { success = true });
    }

    [HttpPost("{id}/reject")]
    [RequirePermission(Permissions.StorefrontModerationManage)]   // Y2
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectReviewRequest? req, [FromServices] ECSPros.Api.Services.Push.PushEtkilesim push, CancellationToken ct)
    {
        var result = await mediator.Send(new ModerateProductReviewCommand(id, false, req?.Reason), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        await push.YorumModereEdildiAsync(id, false, ct);
        return Ok(new { success = true });
    }
}

public record RejectReviewRequest(string? Reason);
