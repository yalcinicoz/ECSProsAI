using ECSPros.Catalog.Application.Queries.GetStoreProductDetail;
using ECSPros.Catalog.Application.Queries.GetStoreProductGroupProducts;
using ECSPros.Catalog.Application.Queries.GetStoreFacets;
using ECSPros.Catalog.Application.Queries.GetStoreProducts;
using ECSPros.Storefront.Application.Queries.GetChannelCategories;
using ECSPros.Storefront.Application.Queries.GetChannelCategoryFacets;
using ECSPros.Storefront.Application.Queries.GetChannelCategoryProducts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/store/catalog")]
public class StoreCatalogController(IMediator mediator, ECSPros.Api.Services.IStoreContext storeContext) : ControllerBase
{
    /// <summary>Ürün grubu ürünlerini listeler (alt gruplar dahil).</summary>
    [HttpGet("product-groups/{id:guid}/products")]
    public async Task<IActionResult> GetProductGroupProducts(
        Guid id,
        [FromQuery] Guid firmPlatformId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        CancellationToken ct = default)
    {
        var platform = await storeContext.GetPlatformAsync(ct);
        var result = await mediator.Send(
            new GetStoreProductGroupProductsQuery(id, firmPlatformId, page, pageSize,
                platform?.StokBitenGoster ?? false, platform?.StokBitenGosterTarih), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Genel ürün listesi (arama + B10 filtre/sıralama; attrs = virgüllü attributeValueId listesi).</summary>
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] Guid firmPlatformId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        [FromQuery] string? attrs = null,
        [FromQuery] decimal? priceMin = null,
        [FromQuery] decimal? priceMax = null,
        [FromQuery] string? sort = null,
        CancellationToken ct = default)
    {
        var platform = await storeContext.GetPlatformAsync(ct);
        var result = await mediator.Send(new GetStoreProductsQuery(
            firmPlatformId, search, page, pageSize,
            ParseGuids(attrs), priceMin, priceMax, sort,
            ApplyStockFilter: true, ShowOutOfStock: platform?.StokBitenGoster ?? false, OutOfStockSince: platform?.StokBitenGosterTarih), ct);
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

    /// <summary>Kanal kategorilerini döner (müşteriye dönük, anonim).</summary>
    [HttpGet("channel-categories")]
    public async Task<IActionResult> GetChannelCategories(
        [FromQuery] Guid firmPlatformId,
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetChannelCategoriesQuery(firmPlatformId, activeOnly), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kanal kategorisine ait ürünleri döner (müşteriye dönük, anonim; B10: search/attrs/fiyat/sort).</summary>
    [HttpGet("channel-categories/{id:guid}/products")]
    public async Task<IActionResult> GetChannelCategoryProducts(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        [FromQuery] string? search = null,
        [FromQuery] string? attrs = null,
        [FromQuery] decimal? priceMin = null,
        [FromQuery] decimal? priceMax = null,
        [FromQuery] string? sort = null,
        CancellationToken ct = default)
    {
        var platform = await storeContext.GetPlatformAsync(ct);
        var result = await mediator.Send(new GetChannelCategoryProductsQuery(
            id, page, pageSize, search, ParseGuids(attrs), priceMin, priceMax, sort,
            platform?.StokBitenGoster ?? false, platform?.StokBitenGosterTarih), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        // B-009: object cast — STJ bildirilen tipten yazar; ChannelCategoryProductsPagedResult'ın
        // additive productTotalCount alanı ancak runtime tipiyle serileşir.
        return Ok(new { success = true, data = (object)result.Value! });
    }

    /// <summary>Virgüllü guid listesini çözer; geçersiz girdiler sessizce atlanır.</summary>
    private static List<Guid>? ParseGuids(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return null;
        var ids = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToList();
        return ids.Count > 0 ? ids : null;
    }

    /// <summary>Genel ürün facet'lerini döner (filtre paneli için).</summary>
    [HttpGet("products/facets")]
    public async Task<IActionResult> GetProductsFacets(
        [FromQuery] Guid firmPlatformId,
        [FromQuery] string? search,
        CancellationToken ct = default)
    {
        var platform = await storeContext.GetPlatformAsync(ct);
        var result = await mediator.Send(new GetStoreFacetsQuery(
            firmPlatformId, search, platform?.StokBitenGoster ?? false, platform?.StokBitenGosterTarih), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kanal kategorisi facet'lerini döner (filtre paneli için).</summary>
    [HttpGet("channel-categories/{id:guid}/facets")]
    public async Task<IActionResult> GetChannelCategoryFacets(
        Guid id,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetChannelCategoryFacetsQuery(id), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }
}
