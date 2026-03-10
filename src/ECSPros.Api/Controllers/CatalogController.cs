using ECSPros.Catalog.Application.Commands.CreateCategory;
using ECSPros.Catalog.Application.Commands.CreateProduct;
using ECSPros.Catalog.Application.Commands.UpdateCategory;
using ECSPros.Catalog.Application.Commands.UpdateProduct;
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

    // ─── Categories ────────────────────────────────────────────────────────────

    /// <summary>Kategorileri listeler (belirtilmezse kök kategoriler).</summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(
        [FromQuery] Guid? parentId,
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCategoriesQuery(parentId, activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni kategori oluşturur.</summary>
    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateCategoryCommand(
            request.Code, request.NameI18n, request.ParentId, request.FillType, request.SortOrder), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/catalog/categories", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Kategoriyi günceller.</summary>
    [HttpPut("categories/{id:guid}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var updatedBy))
            return Unauthorized();

        var result = await _mediator.Send(new UpdateCategoryCommand(
            id, request.NameI18n, request.ParentId, request.FillType, request.IsActive, request.SortOrder, updatedBy), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }

    // ─── Products ──────────────────────────────────────────────────────────────

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

    /// <summary>Yeni ürün oluşturur (varyantlarıyla birlikte).</summary>
    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request, CancellationToken ct)
    {
        var variants = request.Variants.Select(v => new CreateVariantDto(v.Sku, v.BasePrice, v.BaseCost)).ToList();
        var result = await _mediator.Send(new CreateProductCommand(
            request.ProductGroupId, request.Code, request.NameI18n, request.ShortDescriptionI18n, variants), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/catalog/products/{request.Code}", new { success = true, data = new { id = result.Value } });
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

    /// <summary>Ürünü günceller.</summary>
    [HttpPut("products/{id:guid}")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var updatedBy))
            return Unauthorized();

        var result = await _mediator.Send(new UpdateProductCommand(
            id, request.NameI18n, request.ShortDescriptionI18n, request.IsActive, updatedBy), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }
}

public record CreateCategoryRequest(
    string Code,
    Dictionary<string, string> NameI18n,
    Guid? ParentId,
    string FillType = "manual",
    int SortOrder = 0);

public record CreateProductRequest(
    Guid ProductGroupId,
    string Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? ShortDescriptionI18n,
    List<VariantRequest> Variants);

public record VariantRequest(string Sku, decimal BasePrice, decimal? BaseCost);

public record UpdateCategoryRequest(
    Dictionary<string, string> NameI18n,
    Guid? ParentId,
    string FillType,
    bool IsActive,
    int SortOrder);

public record UpdateProductRequest(
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? ShortDescriptionI18n,
    bool IsActive);
