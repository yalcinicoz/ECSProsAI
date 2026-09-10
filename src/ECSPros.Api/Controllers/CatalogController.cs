using ECSPros.Shared.Kernel.Grid;
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
using ECSPros.Catalog.Application.Commands.CreateProduct;
using ECSPros.Catalog.Application.Commands.CreateProductGroup;
using ECSPros.Catalog.Application.Commands.RemoveAxisSubAttribute;
using ECSPros.Catalog.Application.Commands.RemoveProductGroupAttribute;
using ECSPros.Catalog.Application.Commands.UpdateProductGroupAttribute;
using ECSPros.Catalog.Application.Commands.UpdateAxisSubAttribute;
using ECSPros.Catalog.Application.Commands.SetPrimaryAxis;
using ECSPros.Catalog.Application.Commands.SetProductStatus;
using ECSPros.Catalog.Application.Commands.UpdateProduct;
using ECSPros.Catalog.Application.Commands.UpdateProductGroup;
using ECSPros.Catalog.Application.Queries.GetAttributeTypes;
using ECSPros.Catalog.Application.Queries.GetProductPriceHistory;
using ECSPros.Catalog.Application.Commands.UpdateVariantPrice;
using ECSPros.Catalog.Application.Commands.DeleteVariant;
using ECSPros.Catalog.Application.Commands.DeleteProduct;
using ECSPros.Catalog.Application.Commands.DeleteProductGroup;
using ECSPros.Catalog.Application.Commands.ToggleVariantStatus;
using ECSPros.Catalog.Application.Commands.UpdateProductTags;
using ECSPros.Catalog.Application.Commands.UpdateProductSeo;
using ECSPros.Catalog.Application.Queries.GetProductDetail;
using ECSPros.Catalog.Application.Queries.GetVariantByBarcode;
using ECSPros.Catalog.Application.Commands.DeleteAttributeValue;
using ECSPros.Catalog.Application.Commands.SetProductAxisSubAttributeValues;
using ECSPros.Catalog.Application.Queries.GetProductsByAttributeValue;
using ECSPros.Catalog.Application.Queries.GetProductGroups;
using ECSPros.Catalog.Application.Queries.GetProducts;
using ECSPros.Catalog.Application.Queries.GetProductTags;
using ECSPros.Catalog.Application.Queries.LookupProducts;
using ECSPros.Api.Authorization;
using ECSPros.Api.Services.ErpSource;
using ECSPros.Shared.Kernel.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/catalog")]
[Authorize]
[RequirePermission(Permissions.CatalogProductsView)]   // Y2: sayfa yetkisi
public class CatalogController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ErpSourceSyncService _erpSource;

    public CatalogController(IMediator mediator, ErpSourceSyncService erpSource)
    {
        _mediator = mediator;
        _erpSource = erpSource;
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
        [FromQuery] string? sort = null,
        CancellationToken ct = default)
    {
        // DataGrid F4 (2026-09-08): sort/dir + f.* filtreleri (ProductGrid.Schema beyaz listesi); page/pageSize merkezi clamp (1..250).
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, null /* ürün kartları kanaldan bağımsızdır */, defaultPageSize: 20);
        var result = await _mediator.Send(new GetProductsQuery(search, productGroupId, activeOnly, grid.Page, grid.PageSize, sort, grid), ct);
        // Not: ürün LİSTESİ maliyet taşımaz (yalnız export satırı taşır, o da alan yetkisine bağlı).
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// Ürünleri Excel'e aktarır (DataGrid F4, plan §2.8): gövdede aynı filtre modeli (search/sort/dir/filters + named:
    /// activeOnly, productGroupId, sort[legacy]) + kolon listesi (boş = tümü). Sayfalama uygulanmaz; tavan Grid:ExportMaxRows,
    /// kullanıcı bazlı dakikada Grid:ExportPerMinute.
    /// </summary>
    [HttpPost("products/export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> ExportProducts([FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, [FromBody] ECSPros.Shared.Kernel.Grid.GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<CatalogController> logger, CancellationToken ct)
    {
        var grid = body.ToGridRequest(null /* ürün kartları kanaldan bağımsızdır */);
        var filters = new ProductListFilters(grid.Search, ECSPros.Api.Grid.GridExportEndpoint.Kimlik(body, "productGroupId"),
            ECSPros.Api.Grid.GridExportEndpoint.Bayrak(body, "activeOnly") ?? false);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "products", "urunler", "Ürünler",
            ECSPros.Api.Grid.ProductExportColumns.All,
            max => _mediator.Send(new ExportProductsQuery(filters, grid, max, body.NamedValue("sort")), ct), ct, alanIzinleri: await alanYetkileri.IzinlerAsync(ct));
    }

    /// <summary>Yeni ürün oluşturur.</summary>
    [HttpPost("products")]
    [RequirePermission(Permissions.CatalogProductsManage)]   // Y2
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
    public async Task<IActionResult> GetProduct(
        [FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, string code, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductDetailQuery(code), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });

        // Y6 (K6): maliyet hassas alandır — ürün ve VARYANT maliyetleri yetkisi olmayana null gider.
        var izin = await alanYetkileri.IzinlerAsync(ct);
        var urun = result.Value! with
        {
            BaseCost = izin.Maliyetle(result.Value.BaseCost),
            Variants = result.Value.Variants
                .Select(v => v with { BaseCost = izin.Maliyetle(v.BaseCost) }).ToList(),
        };
        return Ok(new { success = true, data = urun });
    }

    /// <summary>V3 gerçek kaynaktan tek ürünü koduyla idempotent yeniler.</summary>
    [HttpPost("products/{code}/refresh-from-erp")]
    [RequirePermission(Permissions.CatalogProductsManage)]
    public async Task<IActionResult> RefreshProductFromErp(string code, CancellationToken ct)
    {
        var report = await _erpSource.RefreshProductAsync(code, ct);
        if (!report.Success)
            return UnprocessableEntity(new { success = false, error = report.Error, detail = report.Detail });
        var product = await _mediator.Send(new GetProductDetailQuery(code), ct);
        return Ok(new
        {
            success = true,
            data = product.IsSuccess ? product.Value : null,
            refresh = new { report.Changed, report.DryRun, report.DurationMs, report.Detail }
        });
    }

    /// <summary>V3 barkodundan ürün kodunu çözer ve ürünü idempotent yeniler.</summary>
    [HttpPost("variants/by-barcode/{barcode}/refresh-from-erp")]
    [RequirePermission(Permissions.CatalogProductsManage)]
    public async Task<IActionResult> RefreshProductFromErpByBarcode(string barcode, CancellationToken ct)
    {
        var report = await _erpSource.RefreshProductByBarcodeAsync(barcode, ct);
        if (!report.Success)
            return UnprocessableEntity(new { success = false, error = report.Error, detail = report.Detail });
        return Ok(new
        {
            success = true,
            refresh = new { report.Changed, report.DryRun, report.DurationMs, report.Detail }
        });
    }

    /// <summary>Ürünü günceller.</summary>
    [HttpPut("products/{id:guid}")]
    [RequirePermission(Permissions.CatalogProductsManage)]   // Y2
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
    [RequirePermission(Permissions.CatalogProductsManage)]   // Y2
    public async Task<IActionResult> ActivateProduct(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetProductStatusCommand(id, true), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Ürünü pasif eder.</summary>
    [HttpPatch("products/{id:guid}/deactivate")]
    [RequirePermission(Permissions.CatalogProductsManage)]   // Y2
    public async Task<IActionResult> DeactivateProduct(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetProductStatusCommand(id, false), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpDelete("products/{id:guid}")]
    [RequirePermission(Permissions.CatalogProductsManage)]   // Y2
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
    [RequirePermission(Permissions.CatalogProductsManage)]   // Y2
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
    [RequirePermission(Permissions.CatalogProductsManage)]   // Y2
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
    [RequirePermission(Permissions.CatalogSettingsManage)]
    public async Task<IActionResult> UpdateCatalogSetting(string key, [FromBody] UpdateCatalogSettingRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateCatalogSettingCommand(key, request.Value), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Sıralı EAN-13 barkodlar üretir.</summary>
    [HttpPost("barcodes/generate")]
    [RequirePermission(Permissions.CatalogProductsManage)]   // Y2
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
    [RequirePermission(Permissions.CatalogProductsManage)]   // Y2
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
    [RequirePermission(Permissions.CatalogProductsManage)]   // Y2
    public async Task<IActionResult> ToggleVariantStatus(Guid id, [FromBody] ToggleVariantStatusRequest request, CancellationToken ct)
    {
        Guid.TryParse(User.FindFirst("sub")?.Value, out var userId);
        var result = await _mediator.Send(new ToggleVariantStatusCommand(id, request.IsActive, userId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpDelete("variants/{id:guid}")]
    [RequirePermission(Permissions.CatalogProductsManage)]   // Y2
    public async Task<IActionResult> DeleteVariant(Guid id, CancellationToken ct)
    {
        Guid.TryParse(User.FindFirst("sub")?.Value, out var userId);
        var result = await _mediator.Send(new DeleteVariantCommand(id, userId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpPut("variants/{id:guid}/barcode")]
    [RequirePermission(Permissions.CatalogProductsManage)]   // Y2
    public async Task<IActionResult> SetVariantBarcode(Guid id, [FromBody] SetVariantBarcodeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetVariantBarcodeCommand(id, request.Barcode), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Varyanta görsel ekler.</summary>
    [HttpPost("variants/{id:guid}/images")]
    [RequirePermission(Permissions.CatalogProductsManage)]   // Y2
    public async Task<IActionResult> AddVariantImage(Guid id, [FromBody] AddVariantImageRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddVariantImageCommand(id, request.ImageUrl, request.IsMain, request.SortOrder), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/catalog/variants/{id}/images", new { success = true, data = new { id = result.Value } });
    }

    // ─── Attribute Types ───────────────────────────────────────────────────────

    /// <summary>Özellik tiplerini TAM liste olarak (değerleriyle) döner — ürün grubu detayı, özellik
    /// tipi detayı, pazaryeri eşleme ve FilterBuilder bunu bütün hâlinde bekler.
    /// ⚠ Sayfalanmaz; liste EKRANI için /attribute-types/grid kullanın.</summary>
    [HttpGet("attribute-types")]
    public async Task<IActionResult> GetAttributeTypes([FromQuery] bool activeOnly = true, [FromQuery] bool includeCounts = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAttributeTypesQuery(activeOnly, includeCounts), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Özellik tipleri liste ekranı (DataGrid): sayfalı, değer/grup sayılı + f.* filtreleri + sort/dir.</summary>
    [HttpGet("attribute-types/grid")]
    public async Task<IActionResult> GetAttributeTypesGrid([FromQuery] bool activeOnly = false, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, null, defaultPageSize: 20);
        var result = await _mediator.Send(new GetAttributeTypesGridQuery(
            new AttributeTypeFilters(activeOnly, search), grid.Page, grid.PageSize, grid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Özellik tiplerini Excel'e aktarır (DataGrid).</summary>
    [HttpPost("attribute-types/export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> ExportAttributeTypes(
        [FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, [FromBody] GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<CatalogController> logger, CancellationToken ct)
    {
        var filters = new AttributeTypeFilters(body.NamedValue("activeOnly") == "true", body.Search);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "attribute-types", "ozellik-tipleri", "Özellik Tipleri",
            ECSPros.Api.Grid.AttributeTypeExportColumns.All,
            max => _mediator.Send(new ExportAttributeTypesQuery(filters, body.ToGridRequest(null), max), ct),
            ct, alanIzinleri: await alanYetkileri.IzinlerAsync(ct));
    }

    /// <summary>Yeni özellik tipi oluşturur.</summary>
    [HttpPost("attribute-types")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> CreateAttributeType([FromBody] CreateAttributeTypeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateAttributeTypeCommand(request.NameI18n, request.DataType, request.SortOrder, request.UseInFilter), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created("/api/catalog/attribute-types", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Özellik tipini günceller.</summary>
    [HttpPut("attribute-types/{id:guid}")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> UpdateAttributeType(Guid id, [FromBody] UpdateAttributeTypeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateAttributeTypeCommand(id, request.NameI18n, request.DataType, request.SortOrder, request.IsActive, request.UseInFilter), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Özellik tipine değer ekler.</summary>
    [HttpPost("attribute-types/{id:guid}/values")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> AddAttributeValue(Guid id, [FromBody] AddAttributeValueRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateAttributeValueCommand(id, request.ValueI18n, request.SortOrder, request.HexCode), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/catalog/attribute-types/{id}/values", new { success = true, data = new { id = result.Value } });
    }

    // ─── Product Groups ────────────────────────────────────────────────────────

    /// <summary>Ürün gruplarını TAM liste olarak döner (özellik/eksen şemasıyla) — dropdown kaynağı.
    /// ⚠ Sayfalanmaz: ürün listesi grup süzgeci, kanal kategori detayı, komisyon ve FilterBuilder bunu
    /// bütün hâlinde bekler. Liste EKRANI için sayfalı /product-groups/grid ucunu kullanın.</summary>
    [HttpGet("product-groups")]
    public async Task<IActionResult> GetProductGroups([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetProductGroupsQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Ürün grupları liste ekranı (DataGrid): sayfalı + f.* filtreleri + sort/dir.</summary>
    [HttpGet("product-groups/grid")]
    public async Task<IActionResult> GetProductGroupsGrid([FromQuery] bool activeOnly = false, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        // Ürün grubu kanaldan bağımsız bir tanım kaydıdır → kanal kısıtı null.
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, null, defaultPageSize: 20);
        var result = await _mediator.Send(new GetProductGroupsGridQuery(
            new ProductGroupListFilters(activeOnly, search), grid.Page, grid.PageSize, grid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Ürün gruplarını Excel'e aktarır (DataGrid): gövde search/sort/dir/filters/columns + named: activeOnly.</summary>
    [HttpPost("product-groups/export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> ExportProductGroups(
        [FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, [FromBody] GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<CatalogController> logger, CancellationToken ct)
    {
        var filters = new ProductGroupListFilters(body.NamedValue("activeOnly") == "true", body.Search);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "product-groups", "urun-gruplari", "Ürün Grupları",
            ECSPros.Api.Grid.ProductGroupExportColumns.All,
            max => _mediator.Send(new ExportProductGroupsQuery(filters, body.ToGridRequest(null), max), ct),
            ct, alanIzinleri: await alanYetkileri.IzinlerAsync(ct));
    }

    /// <summary>Yeni ürün grubu oluşturur; copyAttributesFromGroupId verilirse kaynak grubun özellik şablonu kopyalanır.</summary>
    [HttpPost("product-groups")]
    [RequirePermission(Permissions.CatalogPlatformManage)]
    public async Task<IActionResult> CreateProductGroup([FromBody] CreateProductGroupRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateProductGroupCommand(request.NameI18n, request.SortOrder, request.CopyAttributesFromGroupId), ct);
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
        var result = await _mediator.Send(new UpdateAttributeValueCommand(valueId, request.NameI18n, request.SortOrder, request.IsActive, request.HexCode), ct);
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
        var result = await _mediator.Send(new AddProductGroupAttributeCommand(id, request.AttributeTypeId, request.IsVariant, request.IsRequired, request.SortOrder, request.DefaultAttributeValueId), ct);
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
        var result = await _mediator.Send(new UpdateProductGroupAttributeCommand(attrId, request.IsVariant, request.IsRequired, request.SortOrder, request.DefaultAttributeValueId), ct);
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
    [RequirePermission(Permissions.CatalogProductsManage)]   // Y2
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
    [RequirePermission(Permissions.CatalogProductsManage)]   // Y2
    public async Task<IActionResult> UpdateProductSeo(Guid id, [FromBody] UpdateSeoRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateProductSeoCommand(
            id, request.Slug, request.MetaTitleI18n, request.MetaDescriptionI18n, request.MetaKeywordsI18n), ct);
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

    // ─── Product Tags ─────────────────────────────────────────────────────────

    /// <summary>Tüm ürünlerdeki benzersiz etiketleri döner (filtre oluşturmak için).</summary>
    [HttpGet("tags")]
    public async Task<IActionResult> GetProductTags(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductTagsQuery(), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kod ve/veya Id listesinden ürünleri toplu çözer (kampanya manuel kapsam vb.).</summary>
    [HttpPost("products/lookup")]
    [RequirePermission(Permissions.CatalogProductsManage)]   // Y2
    public async Task<IActionResult> LookupProducts([FromBody] LookupProductsRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new LookupProductsQuery(request.Codes, request.Ids), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true, data = result.Value });
    }

}

public record LookupProductsRequest(List<string>? Codes, List<Guid>? Ids);

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
    bool UseInFilter = false);

public record UpdateAttributeTypeRequest(
    Dictionary<string, string> NameI18n,
    string DataType = "select",
    int SortOrder = 0,
    bool IsActive = true,
    bool UseInFilter = false);

public record AddAttributeValueRequest(
    Dictionary<string, string> ValueI18n,
    int SortOrder = 0,
    string? HexCode = null);

public record CreateProductGroupRequest(
    Dictionary<string, string> NameI18n,
    int SortOrder = 0,
    Guid? CopyAttributesFromGroupId = null);

public record UpdateProductGroupRequest(
    Dictionary<string, string> NameI18n,
    int SortOrder,
    bool IsActive);

public record AddProductGroupAttributeRequest(
    Guid AttributeTypeId,
    bool IsVariant = false,
    bool IsRequired = false,
    int SortOrder = 0,
    Guid? DefaultAttributeValueId = null);

public record AddAxisSubAttributeRequest(
    Guid AxisAttributeTypeId,
    Guid SubAttributeTypeId,
    bool IsRequired = false,
    int SortOrder = 0);

public record UpdateProductGroupAttributeRequest(bool IsVariant = false, bool IsRequired = false, int SortOrder = 0, Guid? DefaultAttributeValueId = null);

public record UpdateAxisSubAttributeRequest(bool IsRequired = false, int SortOrder = 0);

public record UpdateAttributeValueRequest(Dictionary<string, string> NameI18n, int SortOrder = 0, bool IsActive = true, string? HexCode = null);


public record SetProductAttributesRequest(List<ProductAttributeItemRequest> Attributes);

public record ProductAttributeItemRequest(Guid AttributeTypeId, Guid? AttributeValueId, string? CustomValue = null);

public record AddProductVariantsRequest(List<AddProductVariantItemRequest> Variants);

public record SetVariantBarcodeRequest(string? Barcode);
public record UpdateVariantPriceRequest(decimal? BasePrice, decimal? BaseCost);
public record ToggleVariantStatusRequest(bool IsActive);

public record UpdateCatalogSettingRequest(string Value);
public record AddProductVariantItemRequest(string? Sku, List<VariantAxisValueItemRequest> Attributes);

public record VariantAxisValueItemRequest(Guid AttributeTypeId, Guid AttributeValueId);

public record UpdateTagsRequest(List<string>? Tags);
public record SetAxisSubAttributeValuesRequest(List<AxisSubAttributeValueItemRequest> Values);
public record AxisSubAttributeValueItemRequest(Guid AttributeValueId, Guid SubAttributeTypeId, string Value);

public record UpdateSeoRequest(
    string? Slug,
    Dictionary<string, string>? MetaTitleI18n,
    Dictionary<string, string>? MetaDescriptionI18n,
    Dictionary<string, string>? MetaKeywordsI18n);

