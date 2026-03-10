using ECSPros.Catalog.Application.Queries.GetStoreCatalog;
using ECSPros.Catalog.Application.Queries.GetStoreProductDetail;
using ECSPros.Catalog.Application.Queries.GetStoreProducts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/store/catalog")]
public class StoreCatalogController(IMediator mediator) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories([FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetStoreCatalogQuery(firmPlatformId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] Guid firmPlatformId,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetStoreProductsQuery(firmPlatformId, categoryId, search, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("products/{code}")]
    public async Task<IActionResult> GetProduct(string code, [FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetStoreProductDetailQuery(code, firmPlatformId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }
}
