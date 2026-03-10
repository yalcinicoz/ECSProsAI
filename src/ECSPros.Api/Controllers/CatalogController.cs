using ECSPros.Catalog.Application.Commands.AddProductGroupAttribute;
using ECSPros.Catalog.Application.Commands.AddVariantImage;
using ECSPros.Catalog.Application.Commands.CreateAttributeType;
using ECSPros.Catalog.Application.Commands.CreateAttributeValue;
using ECSPros.Catalog.Application.Commands.CreateCategory;
using ECSPros.Catalog.Application.Commands.CreateProduct;
using ECSPros.Catalog.Application.Commands.CreateProductGroup;
using ECSPros.Catalog.Application.Commands.SetFirmPlatformVariantPrice;
using ECSPros.Catalog.Application.Commands.SetProductStatus;
using ECSPros.Catalog.Application.Commands.UpdateCategory;
using ECSPros.Catalog.Application.Commands.UpdateProduct;
using ECSPros.Catalog.Application.Commands.UpdateProductGroup;
using ECSPros.Catalog.Application.Queries.GetAttributeTypes;
using ECSPros.Catalog.Application.Queries.GetCategories;
using ECSPros.Catalog.Application.Queries.GetFirmPlatformPricing;
using ECSPros.Catalog.Application.Queries.GetProductDetail;
using ECSPros.Catalog.Application.Queries.GetProductGroups;
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

    /// <summary>Ürünü aktif eder.</summary>
    [HttpPatch("products/{id:guid}/activate")]
    public async Task<IActionResult> ActivateProduct(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetProductStatusCommand(id, true), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Ürünü pasif eder.</summary>
    [HttpPatch("products/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateProduct(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetProductStatusCommand(id, false), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ─── Variants ──────────────────────────────────────────────────────────────

    /// <summary>Varyanta görsel ekler.</summary>
    [HttpPost("variants/{id:guid}/images")]
    public async Task<IActionResult> AddVariantImage(Guid id, [FromBody] AddVariantImageRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddVariantImageCommand(id, request.ImageUrl, request.IsMain, request.SortOrder), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/catalog/variants/{id}/images", new { success = true, data = new { id = result.Value } });
    }

    // ─── Attribute Types ───────────────────────────────────────────────────────

    /// <summary>Özellik tiplerini listeler.</summary>
    [HttpGet("attribute-types")]
    public async Task<IActionResult> GetAttributeTypes([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAttributeTypesQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni özellik tipi oluşturur.</summary>
    [HttpPost("attribute-types")]
    public async Task<IActionResult> CreateAttributeType([FromBody] CreateAttributeTypeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateAttributeTypeCommand(request.Code, request.NameI18n, request.DataType, request.SortOrder), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created("/api/catalog/attribute-types", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Özellik tipine değer ekler.</summary>
    [HttpPost("attribute-types/{id:guid}/values")]
    public async Task<IActionResult> AddAttributeValue(Guid id, [FromBody] AddAttributeValueRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateAttributeValueCommand(id, request.Code, request.ValueI18n, request.SortOrder), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/catalog/attribute-types/{id}/values", new { success = true, data = new { id = result.Value } });
    }

    // ─── Product Groups ────────────────────────────────────────────────────────

    /// <summary>Ürün gruplarını listeler.</summary>
    [HttpGet("product-groups")]
    public async Task<IActionResult> GetProductGroups([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetProductGroupsQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni ürün grubu oluşturur.</summary>
    [HttpPost("product-groups")]
    public async Task<IActionResult> CreateProductGroup([FromBody] CreateProductGroupRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateProductGroupCommand(request.Code, request.NameI18n, request.ParentId, request.SortOrder), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created("/api/catalog/product-groups", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Ürün grubunu günceller.</summary>
    [HttpPut("product-groups/{id:guid}")]
    public async Task<IActionResult> UpdateProductGroup(Guid id, [FromBody] UpdateProductGroupRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateProductGroupCommand(id, request.NameI18n, request.ParentId, request.SortOrder, request.IsActive), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Ürün grubuna özellik ekler.</summary>
    [HttpPost("product-groups/{id:guid}/attributes")]
    public async Task<IActionResult> AddProductGroupAttribute(Guid id, [FromBody] AddProductGroupAttributeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddProductGroupAttributeCommand(id, request.AttributeTypeId, request.IsVariant, request.IsRequired, request.SortOrder), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/catalog/product-groups/{id}/attributes", new { success = true, data = new { id = result.Value } });
    }

    // ─── Firm Platform Pricing ─────────────────────────────────────────────────

    /// <summary>Platform bazlı ürün fiyatlandırmasını getirir.</summary>
    [HttpGet("firm-platforms/{platformId:guid}/products/{productId:guid}/pricing")]
    public async Task<IActionResult> GetFirmPlatformPricing(Guid platformId, Guid productId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetFirmPlatformPricingQuery(platformId, productId), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Platform varyant fiyatı oluşturur veya günceller (upsert).</summary>
    [HttpPut("firm-platforms/{platformId:guid}/variants/{variantId:guid}/price")]
    public async Task<IActionResult> SetFirmPlatformVariantPrice(
        Guid platformId, Guid variantId, [FromBody] SetVariantPriceRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetFirmPlatformVariantPriceCommand(
            platformId, variantId, request.PriceType, request.PriceMultiplier, request.Price, request.CompareAtPrice, request.IsActive), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { id = result.Value } });
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

public record AddVariantImageRequest(string ImageUrl, bool IsMain, int SortOrder = 0);

public record CreateAttributeTypeRequest(
    string Code,
    Dictionary<string, string> NameI18n,
    string DataType,
    int SortOrder = 0);

public record AddAttributeValueRequest(string Code, Dictionary<string, string> ValueI18n, int SortOrder = 0);

public record CreateProductGroupRequest(
    string Code,
    Dictionary<string, string> NameI18n,
    Guid? ParentId,
    int SortOrder = 0);

public record UpdateProductGroupRequest(
    Dictionary<string, string> NameI18n,
    Guid? ParentId,
    int SortOrder,
    bool IsActive);

public record AddProductGroupAttributeRequest(
    Guid AttributeTypeId,
    bool IsVariant = false,
    bool IsRequired = false,
    int SortOrder = 0);

public record SetVariantPriceRequest(
    string? PriceType,
    decimal? PriceMultiplier,
    decimal? Price,
    decimal? CompareAtPrice,
    bool IsActive = true);
