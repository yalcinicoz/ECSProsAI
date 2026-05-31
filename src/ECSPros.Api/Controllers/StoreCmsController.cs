using ECSPros.Cms.Application.Queries.GetPageDetail;
using ECSPros.Cms.Application.Queries.GetPages;
using ECSPros.Storefront.Application.Queries.GetStoreNavigationMenu;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/store/cms")]
public class StoreCmsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Navigasyon menüsünü code ile döner (müşteriye dönük).
    /// Örnek: GET /api/store/cms/menus/header?firmPlatformId=...
    /// node.nodeType: "category" | "link" | "label"
    /// node.categoryId: category nodeType için ilgili Category.Id
    /// </summary>
    [HttpGet("menus/{code}")]
    public async Task<IActionResult> GetMenu(
        string code,
        [FromQuery] Guid firmPlatformId,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetStoreNavigationMenuQuery(code, firmPlatformId), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("pages")]
    public async Task<IActionResult> GetPages([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetPagesQuery(), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("pages/{id}")]
    public async Task<IActionResult> GetPage(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetPageDetailQuery(id), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }
}
