using ECSPros.Shared.Kernel.Authorization;
using ECSPros.Api.Authorization;
using ECSPros.Storefront.Application.Commands.DeleteCardMessage;
using ECSPros.Storefront.Application.Commands.UpsertCardMessage;
using ECSPros.Storefront.Application.Queries.GetCardMessages;
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
