using ECSPros.Catalog.Application.Commands.AddAxisSubAttribute;
using ECSPros.Catalog.Application.Commands.GenerateBarcodes;
using ECSPros.Catalog.Application.Commands.UpdateCatalogSetting;
using ECSPros.Catalog.Application.Queries.GetCatalogSettings;
using ECSPros.Catalog.Application.Commands.AddProductVariants;
using ECSPros.Catalog.Application.Commands.SetProductAttributes;
using ECSPros.Catalog.Application.Commands.SetVariantBarcode;
using ECSPros.Catalog.Application.Commands.AddProductGroupAttribute;
using ECSPros.Catalog.Application.Commands.AddVariantImage;
using ECSPros.Catalog.Application.Commands.CreateAttributeType;
using ECSPros.Catalog.Application.Commands.UpdateAttributeType;
using ECSPros.Catalog.Application.Commands.UpdateAttributeValue;
using ECSPros.Catalog.Application.Commands.CreateAttributeValue;
using ECSPros.Catalog.Application.Commands.CreateCategory;
using ECSPros.Catalog.Application.Commands.CreateProduct;
using ECSPros.Catalog.Application.Commands.CreateProductGroup;
using ECSPros.Catalog.Application.Commands.RemoveAxisSubAttribute;
using ECSPros.Catalog.Application.Commands.RemoveProductGroupAttribute;
using ECSPros.Catalog.Application.Commands.UpdateProductGroupAttribute;
using ECSPros.Catalog.Application.Commands.UpdateAxisSubAttribute;
using ECSPros.Catalog.Application.Commands.SetAttributeValueProperties;
using ECSPros.Catalog.Application.Commands.SetFirmPlatformVariantPrice;
using ECSPros.Catalog.Application.Commands.SetPrimaryAxis;
using ECSPros.Catalog.Application.Commands.SetProductStatus;
using ECSPros.Catalog.Application.Commands.AddCategoryProduct;
using ECSPros.Catalog.Application.Commands.RemoveCategoryProduct;
using ECSPros.Catalog.Application.Commands.SyncCategoryProducts;
using ECSPros.Catalog.Application.Commands.UpdateCategory;
using ECSPros.Catalog.Application.Queries.GetCategoryDetail;
using ECSPros.Catalog.Application.Queries.GetCategoryProducts;
using ECSPros.Catalog.Application.Commands.UpdateProduct;
using ECSPros.Catalog.Application.Commands.UpdateProductGroup;
using ECSPros.Catalog.Application.Queries.GetAttributeTypes;
using ECSPros.Catalog.Application.Queries.GetCategories;
using ECSPros.Catalog.Application.Queries.GetFirmPlatformPricing;
using ECSPros.Catalog.Application.Queries.GetProductPriceHistory;
using ECSPros.Catalog.Application.Commands.UpdateVariantPrice;
using ECSPros.Catalog.Application.Commands.DeleteVariant;
using ECSPros.Catalog.Application.Commands.DeleteProduct;
using ECSPros.Catalog.Application.Commands.DeleteProductGroup;
using ECSPros.Catalog.Application.Commands.ToggleVariantStatus;
using ECSPros.Catalog.Application.Commands.CreateImageSet;
using ECSPros.Catalog.Application.Commands.UpdateImageSet;
using ECSPros.Catalog.Application.Queries.GetImageSets;
using ECSPros.Catalog.Application.Commands.UpdateProductTags;
using ECSPros.Catalog.Application.Commands.UpdateProductSeo;
using ECSPros.Catalog.Application.Queries.GetProductDetail;
using ECSPros.Catalog.Application.Queries.GetVariantByBarcode;
using ECSPros.Catalog.Application.Commands.DeleteAttributeValue;
using ECSPros.Catalog.Application.Commands.SetProductAxisSubAttributeValues;
using ECSPros.Catalog.Application.Commands.CreateFilterColor;
using ECSPros.Catalog.Application.Commands.UpdateFilterColor;
using ECSPros.Catalog.Application.Commands.DeleteFilterColor;
using ECSPros.Catalog.Application.Commands.SetAttributeValueFilterColors;
using ECSPros.Catalog.Application.Queries.GetFilterColors;
using ECSPros.Catalog.Application.Queries.GetProductsByAttributeValue;
using ECSPros.Catalog.Application.Queries.GetProductGroups;
using ECSPros.Catalog.Application.Queries.GetProducts;
using ECSPros.Api.Authorization;
using ECSPros.Shared.Kernel.Authorization;
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

    /// <summary>Global kategori listesini döner.</summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(
        [FromQuery] Guid? parentId,
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCategoriesQuery(parentId, activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kategori detayını döner (fillType, filterRules dahil).</summary>
    [HttpGet("categories/{id:guid}")]
    public async Task<IActionResult> GetCategory(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCategoryDetailQuery(id), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
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
            id, request.NameI18n, request.ParentId, request.FillType,
            request.FilterRules, request.IsActive, request.SortOrder, updatedBy), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }

    /// <summary>Kategorideki ürünleri listeler (admin).</summary>
    [HttpGet("categories/{id:guid}/products")]
    public async Task<IActionResult> GetCategoryProducts(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCategoryProductsQuery(id, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kategoriye manuel ürün ekler.</summary>
    [HttpPost("categories/{id:guid}/products")]
    public async Task<IActionResult> AddCategoryProduct(
        Guid id, [FromBody] AddCategoryProductRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddCategoryProductCommand(
            id, request.ProductId, request.SortOrder, request.IsPinned), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Kategoriden ürün çıkarır.</summary>
    [HttpDelete("categories/{id:guid}/products/{productId:guid}")]
    public async Task<IActionResult> RemoveCategoryProduct(
        Guid id, Guid productId, CancellationToken ct)
    {
        var result = await _mediator.Send(new RemoveCategoryProductCommand(id, productId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>
    /// FilterRules'u çalıştırır, CategoryProducts tablosunu günceller.
    /// Sadece fillType=filter veya mixed olan kategoriler için geçerlidir.
    /// </summary>
    [HttpPost("categories/{id:guid}/sync")]
    public async Task<IActionResult> SyncCategoryProducts(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new SyncCategoryProductsCommand(id), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { addedCount = result.Value } });
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

    /// <summary>Yeni ürün oluşturur.</summary>
    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request, CancellationToken ct)
    {
        var variants = request.Variants?.Select(v => new CreateVariantDto(v.Sku, v.BasePrice, v.BaseCost)).ToList();
        var result = await _mediator.Send(new CreateProductCommand(
            request.ProductGroupId, request.Code, request.NameI18n, request.ShortDescriptionI18n,
            request.DescriptionI18n, request.BasePrice, request.BaseCost, request.TaxRate, variants), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/catalog/products/{result.Value.Code}", new { success = true, data = new { id = result.Value.Id, code = result.Value.Code } });
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
        var updatedByName = User.FindFirst("full_name")?.Value ?? User.FindFirst("email")?.Value;

        var result = await _mediator.Send(new UpdateProductCommand(
            id, request.NameI18n, request.ShortDescriptionI18n, request.DescriptionI18n,
            request.BasePrice, request.BaseCost, request.TaxRate, request.IsActive,
            request.SupplierId, request.SupplierProductCode, updatedBy, updatedByName), ct);

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

    [HttpDelete("products/{id:guid}")]
    public async Task<IActionResult> DeleteProduct(Guid id, CancellationToken ct)
    {
        Guid.TryParse(User.FindFirst("sub")?.Value, out var userId);
        var result = await _mediator.Send(new DeleteProductCommand(id, userId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Ürün özellik değerlerini toplu kaydeder.</summary>
    [HttpPut("products/{id:guid}/attributes")]
    public async Task<IActionResult> SetProductAttributes(Guid id, [FromBody] SetProductAttributesRequest request, CancellationToken ct)
    {
        var items = request.Attributes
            .Select(a => new ProductAttributeItem(a.AttributeTypeId, a.AttributeValueId, a.CustomValue))
            .ToList();
        var result = await _mediator.Send(new SetProductAttributesCommand(id, items), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ─── Variants ──────────────────────────────────────────────────────────────

    /// <summary>Ürüne varyant kombinasyonları ekler.</summary>
    [HttpPost("products/{id:guid}/variants")]
    public async Task<IActionResult> AddProductVariants(Guid id, [FromBody] AddProductVariantsRequest request, CancellationToken ct)
    {
        var items = request.Variants.Select(v =>
            new AddProductVariantItem(
                v.Sku,
                v.Attributes.Select(a => new VariantAxisValueItem(a.AttributeTypeId, a.AttributeValueId)).ToList()
            )).ToList();
        var result = await _mediator.Send(new AddProductVariantsCommand(id, items), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { added = result.Value } });
    }

    // ─── Settings ──────────────────────────────────────────────────────────────

    /// <summary>Katalog ayarlarını listeler.</summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetCatalogSettings(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCatalogSettingsQuery(), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Katalog ayarını günceller.</summary>
    [HttpPut("settings/{key}")]
    public async Task<IActionResult> UpdateCatalogSetting(string key, [FromBody] UpdateCatalogSettingRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateCatalogSettingCommand(key, request.Value), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ─── Image Sets ────────────────────────────────────────────────────────────

    [HttpGet("image-sets")]
    public async Task<IActionResult> GetImageSets([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetImageSetsQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("image-sets")]
    public async Task<IActionResult> CreateImageSet([FromBody] CreateImageSetRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateImageSetCommand(
            request.Code, request.Name, request.IsDefault, request.FallbackSetId, request.SortPriority), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/catalog/image-sets/{result.Value}", new { success = true, data = new { id = result.Value } });
    }

    [HttpPut("image-sets/{id:guid}")]
    public async Task<IActionResult> UpdateImageSet(Guid id, [FromBody] CatalogUpdateImageSetRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateImageSetCommand(
            id, request.Name, request.IsDefault, request.FallbackSetId, request.SortPriority, request.IsActive), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Sıralı EAN-13 barkodlar üretir.</summary>
    [HttpPost("barcodes/generate")]
    public async Task<IActionResult> GenerateBarcodes([FromQuery] int count = 1, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GenerateBarcodesCommand(count), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Barkod ile varyant arar.</summary>
    [HttpGet("variants/by-barcode/{barcode}")]
    public async Task<IActionResult> GetVariantByBarcode(string barcode, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetVariantByBarcodeQuery(barcode), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Varyantın barkodunu günceller.</summary>
    [HttpPut("variants/{id:guid}/price")]
    public async Task<IActionResult> UpdateVariantPrice(Guid id, [FromBody] UpdateVariantPriceRequest request, CancellationToken ct)
    {
        Guid.TryParse(User.FindFirst("sub")?.Value, out var userId);
        var userName = User.FindFirst("full_name")?.Value;
        var result = await _mediator.Send(new UpdateVariantPriceCommand(id, request.BasePrice, request.BaseCost, userId, userName), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpPatch("variants/{id:guid}/status")]
    public async Task<IActionResult> ToggleVariantStatus(Guid id, [FromBody] ToggleVariantStatusRequest request, CancellationToken ct)
    {
        Guid.TryParse(User.FindFirst("sub")?.Value, out var userId);
        var result = await _mediator.Send(new ToggleVariantStatusCommand(id, request.IsActive, userId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpDelete("variants/{id:guid}")]
    public async Task<IActionResult> DeleteVariant(Guid id, CancellationToken ct)
    {
        Guid.TryParse(User.FindFirst("sub")?.Value, out var userId);
        var result = await _mediator.Send(new DeleteVariantCommand(id, userId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpPut("variants/{id:guid}/barcode")]
    public async Task<IActionResult> SetVariantBarcode(Guid id, [FromBody] SetVariantBarcodeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetVariantBarcodeCommand(id, request.Barcode), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

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
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> CreateAttributeType([FromBody] CreateAttributeTypeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateAttributeTypeCommand(request.NameI18n, request.DataType, request.SortOrder, request.RequiresFilterColor), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created("/api/catalog/attribute-types", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Özellik tipini günceller.</summary>
    [HttpPut("attribute-types/{id:guid}")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> UpdateAttributeType(Guid id, [FromBody] UpdateAttributeTypeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateAttributeTypeCommand(id, request.NameI18n, request.SortOrder, request.IsActive, request.RequiresFilterColor), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Özellik tipine değer ekler.</summary>
    [HttpPost("attribute-types/{id:guid}/values")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> AddAttributeValue(Guid id, [FromBody] AddAttributeValueRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateAttributeValueCommand(id, request.ValueI18n, request.SortOrder, request.FilterColorIds), ct);
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
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> CreateProductGroup([FromBody] CreateProductGroupRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateProductGroupCommand(request.NameI18n, request.SortOrder), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created("/api/catalog/product-groups", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Ürünsüz ürün grubunu siler.</summary>
    [HttpDelete("product-groups/{id:guid}")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> DeleteProductGroup(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var deletedBy = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty;
        var result = await _mediator.Send(new DeleteProductGroupCommand(id, deletedBy), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Ürün grubunu günceller.</summary>
    [HttpPut("product-groups/{id:guid}")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> UpdateProductGroup(Guid id, [FromBody] UpdateProductGroupRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateProductGroupCommand(id, request.NameI18n, request.SortOrder, request.IsActive), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Varyant eksenine alt özellik ekler.</summary>
    [HttpPost("product-groups/{id:guid}/axis-sub-attributes")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> AddAxisSubAttribute(Guid id, [FromBody] AddAxisSubAttributeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddAxisSubAttributeCommand(
            id, request.AxisAttributeTypeId, request.SubAttributeTypeId, request.IsRequired, request.SortOrder), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/catalog/product-groups/{id}/axis-sub-attributes", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Varyant ekseninden alt özellik kaldırır.</summary>
    [HttpDelete("product-groups/{groupId:guid}/axis-sub-attributes/{subAttrId:guid}")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> RemoveAxisSubAttribute(Guid groupId, Guid subAttrId, CancellationToken ct)
    {
        var result = await _mediator.Send(new RemoveAxisSubAttributeCommand(subAttrId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Özellik değerini günceller (ad çevirileri, sıra, durum).</summary>
    [HttpPut("attribute-values/{valueId:guid}")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> UpdateAttributeValue(Guid valueId, [FromBody] UpdateAttributeValueRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateAttributeValueCommand(valueId, request.NameI18n, request.SortOrder, request.IsActive), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Özellik değerinin alt özellik değerlerini kaydeder (upsert).</summary>
    [HttpPut("attribute-values/{valueId:guid}/properties")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> SetAttributeValueProperties(Guid valueId, [FromBody] SetAttributeValuePropertiesRequest request, CancellationToken ct)
    {
        var items = request.Properties.Select(p => new AttributeValuePropertyItem(p.SubAttributeTypeId, p.Value)).ToList();
        var result = await _mediator.Send(new SetAttributeValuePropertiesCommand(valueId, items), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpGet("attribute-values/{valueId:guid}/products")]
    public async Task<IActionResult> GetProductsByAttributeValue(Guid valueId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductsByAttributeValueQuery(valueId), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Hiçbir üründe kullanılmayan özellik değerini siler.</summary>
    [HttpDelete("attribute-values/{valueId:guid}")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> DeleteAttributeValue(Guid valueId, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId))
            return Unauthorized();
        var result = await _mediator.Send(new DeleteAttributeValueCommand(valueId, userId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Ürün grubuna özellik ekler.</summary>
    [HttpPost("product-groups/{id:guid}/attributes")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> AddProductGroupAttribute(Guid id, [FromBody] AddProductGroupAttributeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddProductGroupAttributeCommand(id, request.AttributeTypeId, request.IsVariant, request.IsRequired, request.SortOrder), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/catalog/product-groups/{id}/attributes", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Varyant eksenini ana eksen olarak işaretler.</summary>
    [HttpPatch("product-groups/{groupId:guid}/attributes/{attrId:guid}/set-primary-axis")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> SetPrimaryAxis(Guid groupId, Guid attrId, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetPrimaryAxisCommand(groupId, attrId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Ürün grubu özelliğini günceller (isVariant, isRequired, sortOrder).</summary>
    [HttpPut("product-groups/{groupId:guid}/attributes/{attrId:guid}")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> UpdateProductGroupAttribute(Guid groupId, Guid attrId, [FromBody] UpdateProductGroupAttributeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateProductGroupAttributeCommand(attrId, request.IsVariant, request.IsRequired, request.SortOrder), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Ürün grubu özelliğini kaldırır.</summary>
    [HttpDelete("product-groups/{groupId:guid}/attributes/{attrId:guid}")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> RemoveProductGroupAttribute(Guid groupId, Guid attrId, CancellationToken ct)
    {
        var result = await _mediator.Send(new RemoveProductGroupAttributeCommand(attrId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Eksen alt özelliğini günceller (isRequired, sortOrder).</summary>
    [HttpPut("product-groups/{groupId:guid}/axis-sub-attributes/{subAttrId:guid}")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> UpdateAxisSubAttribute(Guid groupId, Guid subAttrId, [FromBody] UpdateAxisSubAttributeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateAxisSubAttributeCommand(subAttrId, request.IsRequired, request.SortOrder), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ─── Tags & SEO ────────────────────────────────────────────────────────────

    [HttpPut("products/{id:guid}/tags")]
    public async Task<IActionResult> UpdateProductTags(Guid id, [FromBody] UpdateTagsRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateProductTagsCommand(id, request.Tags ?? []), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpPut("products/{id:guid}/axis-sub-attribute-values")]
    [RequirePermission(Permissions.CatalogProductsManage)]
    public async Task<IActionResult> SetProductAxisSubAttributeValues(Guid id, [FromBody] SetAxisSubAttributeValuesRequest request, CancellationToken ct)
    {
        var items = request.Values.Select(v => new AxisSubAttributeValueItem(v.AttributeValueId, v.SubAttributeTypeId, v.Value)).ToList();
        var result = await _mediator.Send(new SetProductAxisSubAttributeValuesCommand(id, items), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpPut("products/{id:guid}/seo")]
    public async Task<IActionResult> UpdateProductSeo(Guid id, [FromBody] UpdateSeoRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateProductSeoCommand(
            id, request.Slug, request.MetaTitleI18n, request.MetaDescriptionI18n, request.MetaKeywordsI18n), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ─── Filter Colors ─────────────────────────────────────────────────────────

    [HttpGet("filter-colors")]
    public async Task<IActionResult> GetFilterColors(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetFilterColorsQuery(), ct);
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("filter-colors")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> CreateFilterColor([FromBody] CreateFilterColorRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateFilterColorCommand(
            request.Code, request.NameI18n, request.HexCode, request.SortOrder), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/catalog/filter-colors/{result.Value}", new { success = true, data = new { id = result.Value } });
    }

    [HttpPut("filter-colors/{id:guid}")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> UpdateFilterColor(Guid id, [FromBody] UpdateFilterColorRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateFilterColorCommand(
            id, request.Code, request.NameI18n, request.HexCode, request.SortOrder, request.IsActive), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpDelete("filter-colors/{id:guid}")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> DeleteFilterColor(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteFilterColorCommand(id), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpPut("attribute-values/{valueId:guid}/filter-colors")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> SetAttributeValueFilterColors(Guid valueId, [FromBody] SetAttributeValueFilterColorsRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetAttributeValueFilterColorsCommand(valueId, request.FilterColorIds), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ─── Price History ─────────────────────────────────────────────────────────

    [HttpGet("products/{productId:guid}/price-history")]
    public async Task<IActionResult> GetProductPriceHistory(Guid productId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductPriceHistoryQuery(productId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
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
        Guid.TryParse(User.FindFirst("sub")?.Value, out var changedBy);
        var changedByName = User.FindFirst("full_name")?.Value ?? User.FindFirst("email")?.Value;

        var result = await _mediator.Send(new SetFirmPlatformVariantPriceCommand(
            platformId, variantId, request.PriceType, request.PriceMultiplier,
            request.Price, request.CompareAtPrice, request.IsActive,
            changedBy, changedByName, request.FirmPlatformCode), ct);
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
    string? Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? ShortDescriptionI18n = null,
    Dictionary<string, string>? DescriptionI18n = null,
    decimal BasePrice = 0,
    decimal? BaseCost = null,
    int TaxRate = 18,
    List<VariantRequest>? Variants = null);

public record VariantRequest(string Sku, decimal BasePrice, decimal? BaseCost);

public record UpdateCategoryRequest(
    Dictionary<string, string> NameI18n,
    Guid? ParentId,
    string FillType,
    Dictionary<string, object>? FilterRules,
    bool IsActive,
    int SortOrder);

public record AddCategoryProductRequest(
    Guid ProductId,
    int SortOrder = 0,
    bool IsPinned = false);

public record UpdateProductRequest(
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? ShortDescriptionI18n,
    Dictionary<string, string>? DescriptionI18n,
    bool IsActive,
    decimal BasePrice = 0,
    decimal? BaseCost = null,
    int TaxRate = 18,
    Guid? SupplierId = null,
    string? SupplierProductCode = null);

public record AddVariantImageRequest(string ImageUrl, bool IsMain, int SortOrder = 0);

public record CreateAttributeTypeRequest(
    Dictionary<string, string> NameI18n,
    string DataType,
    int SortOrder = 0,
    bool RequiresFilterColor = false);

public record UpdateAttributeTypeRequest(
    Dictionary<string, string> NameI18n,
    int SortOrder = 0,
    bool IsActive = true,
    bool RequiresFilterColor = false);

public record AddAttributeValueRequest(
    Dictionary<string, string> ValueI18n,
    int SortOrder = 0,
    List<Guid>? FilterColorIds = null);

public record CreateProductGroupRequest(
    Dictionary<string, string> NameI18n,
    int SortOrder = 0);

public record UpdateProductGroupRequest(
    Dictionary<string, string> NameI18n,
    int SortOrder,
    bool IsActive);

public record AddProductGroupAttributeRequest(
    Guid AttributeTypeId,
    bool IsVariant = false,
    bool IsRequired = false,
    int SortOrder = 0);

public record AddAxisSubAttributeRequest(
    Guid AxisAttributeTypeId,
    Guid SubAttributeTypeId,
    bool IsRequired = false,
    int SortOrder = 0);

public record UpdateProductGroupAttributeRequest(bool IsVariant = false, bool IsRequired = false, int SortOrder = 0);

public record UpdateAxisSubAttributeRequest(bool IsRequired = false, int SortOrder = 0);

public record UpdateAttributeValueRequest(Dictionary<string, string> NameI18n, int SortOrder = 0, bool IsActive = true);

public record SetAttributeValuePropertiesRequest(
    List<ValuePropertyItemRequest> Properties);

public record ValuePropertyItemRequest(Guid SubAttributeTypeId, string Value);

public record SetProductAttributesRequest(List<ProductAttributeItemRequest> Attributes);

public record ProductAttributeItemRequest(Guid AttributeTypeId, Guid? AttributeValueId, string? CustomValue = null);

public record AddProductVariantsRequest(List<AddProductVariantItemRequest> Variants);

public record SetVariantBarcodeRequest(string? Barcode);
public record UpdateVariantPriceRequest(decimal? BasePrice, decimal? BaseCost);
public record ToggleVariantStatusRequest(bool IsActive);

public record UpdateCatalogSettingRequest(string Value);
public record CreateImageSetRequest(string Code, string Name, bool IsDefault, Guid? FallbackSetId, int SortPriority = 0);
public record CatalogUpdateImageSetRequest(string Name, bool IsDefault, Guid? FallbackSetId, int SortPriority, bool IsActive);

public record AddProductVariantItemRequest(string? Sku, List<VariantAxisValueItemRequest> Attributes);

public record VariantAxisValueItemRequest(Guid AttributeTypeId, Guid AttributeValueId);

public record SetVariantPriceRequest(
    string? PriceType,
    decimal? PriceMultiplier,
    decimal? Price,
    decimal? CompareAtPrice,
    bool IsActive = true,
    string? FirmPlatformCode = null);

public record UpdateTagsRequest(List<string>? Tags);
public record CreateFilterColorRequest(string Code, Dictionary<string, string> NameI18n, string? HexCode, int SortOrder = 0);
public record UpdateFilterColorRequest(string Code, Dictionary<string, string> NameI18n, string? HexCode, int SortOrder, bool IsActive);
public record SetAttributeValueFilterColorsRequest(List<Guid> FilterColorIds);
public record SetAxisSubAttributeValuesRequest(List<AxisSubAttributeValueItemRequest> Values);
public record AxisSubAttributeValueItemRequest(Guid AttributeValueId, Guid SubAttributeTypeId, string Value);

public record UpdateSeoRequest(
    string? Slug,
    Dictionary<string, string>? MetaTitleI18n,
    Dictionary<string, string>? MetaDescriptionI18n,
    Dictionary<string, string>? MetaKeywordsI18n);
