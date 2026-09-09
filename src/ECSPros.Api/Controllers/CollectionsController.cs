using ECSPros.Shared.Kernel.Authorization;
using ECSPros.Shared.Kernel.Grid;
using ECSPros.Api.Authorization;
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
[RequirePermission(Permissions.StorefrontContentView)]   // Y2: sayfa yetkisi
[KanalKapsamiKontrol(Permissions.StorefrontContentView)]   // Y3: kanal parametresi kapsam dışıysa 404
public class CollectionsController(IMediator mediator) : ControllerBase
{
    /// <summary>Koleksiyon moderasyon kuyruğu (DataGrid: f.* filtreleri + sort/dir + arama).
    /// Y3 (2026-09-09): kanal kapsamı artık LİSTEYE de uygulanıyor — önceden yalnız kanal
    /// parametresi denetleniyordu, kapsam dışı kanalın koleksiyonu kuyrukta görünebiliyordu.</summary>
    [HttpGet]
    public async Task<IActionResult> GetForModeration(
        [FromServices] ECSPros.Api.Authorization.IKanalKapsami kanalKapsami,
        [FromQuery] string? status = "pending", [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var kapsam = await kanalKapsami.KanallarAsync(Permissions.StorefrontContentView, ct);
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, kapsam, defaultPageSize: 20);
        var result = await mediator.Send(new GetCollectionsForModerationQuery(
            status, grid.Page, grid.PageSize, search, kapsam, grid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Koleksiyonları Excel'e aktarır (DataGrid). Y3: kapsam kullanıcının yetkisinden çözülür.</summary>
    [HttpPost("export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> Export(
        [FromServices] ECSPros.Api.Authorization.IKanalKapsami kanalKapsami,
        [FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, [FromBody] GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<CollectionsController> logger, CancellationToken ct)
    {
        var kapsam = await kanalKapsami.KanallarAsync(Permissions.StorefrontContentView, ct);
        var filters = new CollectionModerationFilters(body.NamedValue("status"), body.Search);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "collections", "koleksiyonlar", "Koleksiyonlar",
            ECSPros.Api.Grid.CollectionModerationExportColumns.All,
            max => mediator.Send(new ExportCollectionsForModerationQuery(filters, body.ToGridRequest(kapsam), max), ct),
            ct, alanIzinleri: await alanYetkileri.IzinlerAsync(ct));
    }

    [HttpPost("{id}/approve")]
    [RequirePermission(Permissions.StorefrontContentManage)]   // Y2
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new ModerateCollectionCommand(id, true), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpPost("{id}/reject")]
    [RequirePermission(Permissions.StorefrontContentManage)]   // Y2
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new ModerateCollectionCommand(id, false), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }
}
