using ECSPros.Catalog.Application.Queries.GetStoreCatalog;
using ECSPros.Catalog.Application.Queries.GetStoreCategoryProducts;
using ECSPros.Catalog.Application.Queries.GetStoreProductDetail;
using ECSPros.Catalog.Application.Queries.GetStoreProductGroupProducts;
using ECSPros.Catalog.Application.Queries.GetStoreProducts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/store/catalog")]
public class StoreCatalogController(IMediator mediator) : ControllerBase
{
    /// <summary>Global kategori ağacını döner (tüm kanallar için ortak).</summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var result = await mediator.Send(new GetStoreCatalogQuery(), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// Kategori ürünlerini listeler.
    /// fillType=manual → sadece el ile eklenenler
    /// fillType=filter  → FilterRules dinamik sorgu
    /// fillType=mixed   → ikisinin birleşimi
    /// </summary>
    [HttpGet("categories/{id:guid}/products")]
    public async Task<IActionResult> GetCategoryProducts(
        Guid id,
        [FromQuery] Guid firmPlatformId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetStoreCategoryProductsQuery(id, firmPlatformId, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Ürün grubu ürünlerini listeler (alt gruplar dahil).</summary>
    [HttpGet("product-groups/{id:guid}/products")]
    public async Task<IActionResult> GetProductGroupProducts(
        Guid id,
        [FromQuery] Guid firmPlatformId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetStoreProductGroupProductsQuery(id, firmPlatformId, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Genel ürün listesi (arama + kategori filtresi).</summary>
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

    /// <summary>Ürün detayını döner.</summary>
    [HttpGet("products/{code}")]
    public async Task<IActionResult> GetProduct(string code, [FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetStoreProductDetailQuery(code, firmPlatformId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }
}
