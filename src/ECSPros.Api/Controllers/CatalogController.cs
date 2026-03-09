using ECSPros.Catalog.Application.Queries.GetCategories;
using ECSPros.Catalog.Application.Queries.GetProductDetail;
using ECSPros.Catalog.Application.Queries.GetProducts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/catalog")]
[Authorize]
public class CatalogController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Kategorileri listeler (belirtilmezse kök kategoriler).</summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories([FromQuery] Guid? parentId, [FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCategoriesQuery(parentId, activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Ürünleri sayfalı listeler.</summary>
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? search,
        [FromQuery] Guid? productGroupId,
        [FromQuery] bool activeOnly = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetProductsQuery(search, productGroupId, activeOnly, page, pageSize), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Ürün detayını kod ile getirir.</summary>
    [HttpGet("products/{code}")]
    public async Task<IActionResult> GetProduct(string code, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductDetailQuery(code), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });

        return Ok(new { success = true, data = result.Value });
    }
}
