using ECSPros.Shared.Kernel.Authorization;
using ECSPros.Api.Authorization;
using ECSPros.Storefront.Application.Commands.DeleteCardMessage;
using ECSPros.Storefront.Application.Commands.UpsertCardMessage;
using ECSPros.Storefront.Application.Queries.GetCardMessages;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// Ürün Kartı F2 (2026-08-09): kart mesajları CRUD — panel Storefront → Ürün Kartı →
/// Kart Mesajları sekmesi. Mesajlar kartın değişken alanlarında (1/2/3) rotasyonla döner.
/// </summary>
[ApiController]
[Route("api/storefront/card-messages")]
[Authorize]
[RequirePermission(Permissions.StorefrontContentView)]   // Y2: sayfa yetkisi
[KanalKapsamiKontrol(Permissions.StorefrontContentView)]   // Y3: kanal parametresi kapsam dışıysa 404
public class CardMessagesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetCardMessagesQuery(firmPlatformId), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kart mesajı liste ekranı (DataGrid, 2026-09-09): sayfalı + f.* filtreleri + sort/dir.
    /// ⚠ Düz <c>GET</c> kanalın TAM listesini döner ve sayfalanmaz.</summary>
    [HttpGet("grid")]
    public async Task<IActionResult> Grid(
        [FromServices] IKanalKapsami kanalKapsami,
        [FromQuery] Guid firmPlatformId, [FromQuery] int? slot = null,
        [FromQuery] bool activeOnly = false, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var kanalKisiti = await kanalKapsami.KanallarAsync(Permissions.StorefrontContentView, ct);   // Y3
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, kanalKisiti, defaultPageSize: 50);
        var result = await mediator.Send(new GetCardMessagesGridQuery(
            new CardMessageFiltreleri(firmPlatformId, slot, activeOnly, search),
            grid.Page, grid.PageSize, grid, kanalKisiti), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kart mesajlarını Excel'e aktarır (DataGrid) — liste ile AYNI kanal kapsamı.</summary>
    [HttpPost("export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> Export(
        [FromServices] IAlanYetkileri alanYetkileri, [FromServices] IKanalKapsami kanalKapsami,
        [FromBody] GridExportRequest body, [FromServices] IConfiguration config,
        [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<CardMessagesController> logger, CancellationToken ct)
    {
        var kanalKisiti = await kanalKapsami.KanallarAsync(Permissions.StorefrontContentView, ct);   // Y3
        var filtreler = new CardMessageFiltreleri(
            ECSPros.Api.Grid.GridExportEndpoint.Kimlik(body, "firmPlatformId") ?? Guid.Empty,
            int.TryParse(body.NamedValue("slot"), out var s) ? s : null,
            body.NamedValue("activeOnly") == "true", body.Search);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger,
            "card-messages", "kart-mesajlari", "Kart Mesajları",
            ECSPros.Api.Grid.CardMessageExportColumns.All,
            max => mediator.Send(new ExportCardMessagesQuery(
                filtreler, body.ToGridRequest(kanalKisiti), max, kanalKisiti), ct),
            ct, alanIzinleri: await alanYetkileri.IzinlerAsync(ct));
    }

    [HttpPost]
    [RequirePermission(Permissions.StorefrontContentManage)]   // Y2
    public async Task<IActionResult> Create([FromBody] CardMessageRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(request.ToCommand(null), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created(string.Empty, new { success = true, data = new { id = result.Value } });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.StorefrontContentManage)]   // Y2
    public async Task<IActionResult> Update(Guid id, [FromBody] CardMessageRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(request.ToCommand(id), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.StorefrontContentManage)]   // Y2
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteCardMessageCommand(id), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }
}

public record CardMessageRequest(
    Guid FirmPlatformId,
    int Slot,
    Dictionary<string, string> MessageI18n,
    string? Icon,
    string? Color,
    string ScopeType,
    List<Guid>? ScopeCategoryIds,
    List<string>? ScopeProductCodes,
    DateTime? StartDate,
    DateTime? EndDate,
    int SortOrder = 0,
    bool IsActive = true)
{
    public UpsertCardMessageCommand ToCommand(Guid? id) => new(
        id, FirmPlatformId, Slot, MessageI18n, Icon, Color, ScopeType,
        ScopeCategoryIds, ScopeProductCodes, StartDate, EndDate, SortOrder, IsActive);
}
