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
public class StoreCatalogController(IMediator mediator, ECSPros.Api.Services.IStoreContext storeContext, ECSPros.Api.Services.UrunKategoriHaritasi kategoriHaritasi) : ControllerBase
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

    /// <summary>Genel ürün listesi — arama, filtre, fiyat aralığı ve sıralama destekler.</summary>
    /// <param name="firmPlatformId">Zorunlu. Kanal kimliği (bootstrap yanıtındaki id).</param>
    /// <param name="search">Serbest metin arama (ürün kodu, ad ya da renk/beden gibi özellik değeri).</param>
    /// <param name="page">Sayfa numarası (1'den başlar).</param>
    /// <param name="pageSize">Sayfa boyutu (varsayılan 24).</param>
    /// <param name="attrs">Virgüllü attributeValueId listesi (facets yanıtındaki değer id'leri).
    /// Yaprak kanal-kategori id'si de bu listeye konur — sunucu ayırır.</param>
    /// <param name="priceMin">Alt fiyat sınırı (kartta gösterilen efektif fiyata göre).</param>
    /// <param name="priceMax">Üst fiyat sınırı.</param>
    /// <param name="sort">Geçerli değerler: default · price_asc · price_desc · newest · rating_desc ·
    /// reviews_desc · favorites_desc · cart_desc · views_desc · sales_desc.
    /// "Popüler ürünler" için sales_desc (çok satılan) ya da views_desc (çok bakılan) kullanın.</param>
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
        // Popüler aramalar (2026-09-01): yalnız ilk sayfa sayılır (sayfalama tekrarları şişirmesin)
        if (page == 1 && !string.IsNullOrWhiteSpace(search))
            await HttpContext.RequestServices.GetRequiredService<ECSPros.Api.Services.Store.AramaTerimIzleyici>()
                .KaydetAsync(firmPlatformId, search, Request.Headers.UserAgent.ToString(), ct);
        // 2026-08-15: attrs içinde yaprak KATEGORİ id'si de gelebilir (liste sayfası Kategori
        // filtresi) — haritayla ayrılır, kategori seçimi ürün-id kısıtına çevrilir (additive).
        var harita = await kategoriHaritasi.GetAsync(firmPlatformId, ct);
        var (kategoriler, ozellikler) = harita?.Ayir(ParseGuids(attrs)) ?? ([], ParseGuids(attrs));
        var result = await mediator.Send(new GetStoreProductsQuery(
            firmPlatformId, search, page, pageSize,
            ozellikler, priceMin, priceMax, sort,
            ProductIds: harita?.UrunIdleri(kategoriler),
            ApplyStockFilter: true, ShowOutOfStock: platform?.StokBitenGoster ?? false, OutOfStockSince: platform?.StokBitenGosterTarih), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Popüler arama terimleri — son 30 günün gerçek aramalarından (en az 3 kez aranmış),
    /// veri birikene kadar liste tohum terimlerle tamamlanır. Arama kutusu açılışında
    /// "Popüler Aramalar" chip'lerini beslemek içindir; 5 dk sunucu önbelleği vardır.</summary>
    /// <param name="firmPlatformId">Zorunlu. Kanal kimliği (bootstrap yanıtındaki id).</param>
    /// <param name="limit">Dönen terim sayısı (varsayılan 10, en çok 20).</param>
    [HttpGet("popular-searches")]
    public async Task<IActionResult> GetPopularSearches(
        [FromQuery] Guid firmPlatformId,
        [FromQuery] int limit = 10,
        [FromServices] ECSPros.Api.Services.Store.PopulerAramaServisi populer = null!,
        CancellationToken ct = default)
    {
        if (firmPlatformId == Guid.Empty)
            return BadRequest(new { success = false, error = "firmPlatformId gerekli." });
        var terimler = await populer.GetirAsync(firmPlatformId, limit, ct);
        return Ok(new { success = true, data = new { terms = terimler } });
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

    /// <summary>Kanal kategorisine ait ürünleri döner (müşteriye dönük, anonim).</summary>
    /// <param name="id">Kanal kategori id'si (channel-categories yanıtından).</param>
    /// <param name="page">Sayfa numarası (1'den başlar).</param>
    /// <param name="pageSize">Sayfa boyutu (varsayılan 24).</param>
    /// <param name="search">Kategori içinde serbest metin arama.</param>
    /// <param name="attrs">Virgüllü attributeValueId listesi (kategori facets yanıtındaki değer id'leri).</param>
    /// <param name="priceMin">Alt fiyat sınırı (efektif fiyata göre).</param>
    /// <param name="priceMax">Üst fiyat sınırı.</param>
    /// <param name="sort">Geçerli değerler: default · price_asc · price_desc · newest · rating_desc ·
    /// reviews_desc · favorites_desc · cart_desc · views_desc · sales_desc.</param>
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
        // 2026-08-15: attrs içindeki yaprak kategori id'leri → ürün-id kısıtı (bkz. GetProducts)
        var harita = platform is null ? null : await kategoriHaritasi.GetAsync(platform.Id, ct);
        var (kategoriler, ozellikler) = harita?.Ayir(ParseGuids(attrs)) ?? ([], ParseGuids(attrs));
        var result = await mediator.Send(new GetChannelCategoryProductsQuery(
            id, page, pageSize, search, ozellikler, priceMin, priceMax, sort,
            platform?.StokBitenGoster ?? false, platform?.StokBitenGosterTarih,
            RestrictProductIds: harita?.UrunIdleri(kategoriler)), ct);
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
