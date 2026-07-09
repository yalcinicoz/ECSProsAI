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

    /// <summary>
    /// C8: hukuki/bilgilendirme sayfaları (mesafeli satış sözleşmesi, ön bilgilendirme…).
    /// codes virgülle ayrılır; verilmezse platformun tüm legal sayfaları döner.
    /// </summary>
    [HttpGet("legal")]
    public async Task<IActionResult> GetLegalPages(
        [FromQuery] Guid firmPlatformId,
        [FromQuery] string? codes,
        CancellationToken ct)
    {
        var kodListesi = string.IsNullOrWhiteSpace(codes)
            ? null
            : codes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var result = await mediator.Send(new ECSPros.Cms.Application.Queries.GetStoreLegalPages.GetStoreLegalPagesQuery(firmPlatformId, kodListesi), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
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
