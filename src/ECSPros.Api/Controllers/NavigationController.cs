using ECSPros.Storefront.Application.Commands.CreateNavigationMenu;
using ECSPros.Storefront.Application.Commands.DeleteNavigationMenu;
using ECSPros.Storefront.Application.Commands.SaveNavNodes;
using ECSPros.Storefront.Application.Commands.UpdateNavigationMenu;
using ECSPros.Storefront.Application.Queries.GetNavigationMenuDetail;
using ECSPros.Storefront.Application.Queries.GetNavigationMenus;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/navigation")]
[Authorize]
public class NavigationController(IMediator mediator) : ControllerBase
{
    // ─── Navigation Menus ────────────────────────────────────────────────────

    /// <summary>Navigasyon menülerini listeler.</summary>
    [HttpGet("menus")]
    public async Task<IActionResult> GetMenus(
        [FromQuery] Guid? firmPlatformId,
        [FromQuery] bool activeOnly = false,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetNavigationMenusQuery(firmPlatformId, activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Navigasyon menüsü detayını node ağacıyla döner.</summary>
    [HttpGet("menus/{id:guid}")]
    public async Task<IActionResult> GetMenuDetail(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetNavigationMenuDetailQuery(id), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni navigasyon menüsü oluşturur.</summary>
    [HttpPost("menus")]
    public async Task<IActionResult> CreateMenu([FromBody] CreateMenuRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateNavigationMenuCommand(
            req.FirmPlatformId, req.Code, req.NameI18n, req.MenuType, req.SortOrder), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/navigation/menus/{result.Value}", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Navigasyon menüsü meta bilgilerini günceller.</summary>
    [HttpPut("menus/{id:guid}")]
    public async Task<IActionResult> UpdateMenu(Guid id, [FromBody] UpdateMenuRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateNavigationMenuCommand(
            id, req.NameI18n, req.MenuType, req.IsActive, req.SortOrder), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Menünün tüm node ağacını toplu kaydeder (replace).</summary>
    [HttpPut("menus/{id:guid}/nodes")]
    public async Task<IActionResult> SaveNodes(Guid id, [FromBody] SaveNodesRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new SaveNavNodesCommand(id, req.Nodes), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Navigasyon menüsünü siler.</summary>
    [HttpDelete("menus/{id:guid}")]
    public async Task<IActionResult> DeleteMenu(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteNavigationMenuCommand(id), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }
}

public record CreateMenuRequest(
    Guid FirmPlatformId,
    string Code,
    Dictionary<string, string> NameI18n,
    string MenuType = "header",
    int SortOrder = 0);

public record UpdateMenuRequest(
    Dictionary<string, string> NameI18n,
    string MenuType,
    bool IsActive,
    int SortOrder);

public record SaveNodesRequest(List<NavNodeInput> Nodes);
