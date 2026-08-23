using ECSPros.Storefront.Application.Commands.AddChannelCategoryProduct;
using ECSPros.Storefront.Application.Commands.CreateChannelCategory;
using ECSPros.Storefront.Application.Commands.CreateNavigationMenu;
using ECSPros.Storefront.Application.Commands.DeleteChannelCategory;
using ECSPros.Storefront.Application.Commands.DeleteNavigationMenu;
using ECSPros.Storefront.Application.Commands.RemoveChannelCategoryProduct;
using ECSPros.Storefront.Application.Commands.SaveChannelCategoryGroups;
using ECSPros.Storefront.Application.Commands.SaveNavNodes;
using ECSPros.Storefront.Application.Commands.SetChannelProductFeatured;
using ECSPros.Storefront.Application.Commands.SetChannelVariantPrice;
using ECSPros.Storefront.Application.Commands.SyncChannelCategoryProducts;
using ECSPros.Storefront.Application.Commands.UpdateChannelCategory;
using ECSPros.Storefront.Application.Commands.UpdateNavigationMenu;
using ECSPros.Storefront.Application.Commands.UpsertChannelProductGroup;
using ECSPros.Storefront.Application.Queries.GetChannelCategories;
using ECSPros.Storefront.Application.Queries.GetChannelCategoryDetail;
using ECSPros.Storefront.Application.Queries.GetChannelCategoryProducts;
using ECSPros.Storefront.Application.Queries.GetChannelProductGroups;
using ECSPros.Storefront.Application.Queries.GetChannelVariantPricing;
using ECSPros.Storefront.Application.Queries.GetNavigationMenuDetail;
using ECSPros.Storefront.Application.Queries.GetNavigationMenus;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/navigation")]
[Authorize]
public class NavigationController(IMediator mediator) : ControllerBase
{
    // ─── Navigation Menus ────────────────────────────────────────────────────

    [HttpGet("menus")]
    public async Task<IActionResult> GetMenus(
        [FromQuery] Guid? firmPlatformId,
        [FromQuery] bool activeOnly = false,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetNavigationMenusQuery(firmPlatformId, activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("menus/{id:guid}")]
    public async Task<IActionResult> GetMenuDetail(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetNavigationMenuDetailQuery(id), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("menus")]
    public async Task<IActionResult> CreateMenu([FromBody] CreateMenuRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateNavigationMenuCommand(
            req.FirmPlatformId, req.Code, req.NameI18n, req.MenuType, req.SortOrder), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/navigation/menus/{result.Value}", new { success = true, data = new { id = result.Value } });
    }

    [HttpPut("menus/{id:guid}")]
    public async Task<IActionResult> UpdateMenu(Guid id, [FromBody] UpdateMenuRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateNavigationMenuCommand(
            id, req.NameI18n, req.MenuType, req.IsActive, req.SortOrder), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpPut("menus/{id:guid}/nodes")]
    public async Task<IActionResult> SaveNodes(Guid id, [FromBody] SaveNodesRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new SaveNavNodesCommand(id, req.Nodes), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpDelete("menus/{id:guid}")]
    public async Task<IActionResult> DeleteMenu(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteNavigationMenuCommand(id), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ─── Channel Categories ──────────────────────────────────────────────────

    [HttpGet("channel-categories")]
    public async Task<IActionResult> GetChannelCategories(
        [FromQuery] Guid firmPlatformId,
        [FromQuery] bool activeOnly = false,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetChannelCategoriesQuery(firmPlatformId, activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("channel-categories/{id:guid}")]
    public async Task<IActionResult> GetChannelCategoryDetail(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetChannelCategoryDetailQuery(id), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("channel-categories")]
    public async Task<IActionResult> CreateChannelCategory(
        [FromBody] CreateChannelCategoryRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateChannelCategoryCommand(
            req.FirmPlatformId, req.ParentId, req.NameI18n, req.Slug,
            req.FillType, req.FilterDef, req.SortOrder,
            req.DisplayImageUrl, req.BadgeLabel), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/navigation/channel-categories/{result.Value}",
            new { success = true, data = new { id = result.Value } });
    }

    [HttpPut("channel-categories/{id:guid}")]
    public async Task<IActionResult> UpdateChannelCategory(
        Guid id, [FromBody] UpdateChannelCategoryRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateChannelCategoryCommand(
            id, req.ParentId, req.NameI18n, req.Slug, req.Status, req.FillType,
            req.ListingMode ?? "product",
            req.FilterDef, req.SortOrder, req.DisplayImageUrl, req.BadgeLabel,
            req.MetaTitleI18n, req.MetaDescriptionI18n, req.OgImageUrl, req.OgTitleI18n, req.GoogleCategoryId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpDelete("channel-categories/{id:guid}")]
    public async Task<IActionResult> DeleteChannelCategory(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteChannelCategoryCommand(id), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpGet("channel-categories/{id:guid}/products")]
    public async Task<IActionResult> GetChannelCategoryProducts(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetChannelCategoryProductsQuery(id, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("channel-categories/{id:guid}/products")]
    public async Task<IActionResult> AddChannelCategoryProduct(
        Guid id, [FromBody] AddChannelCategoryProductRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new AddChannelCategoryProductCommand(
            id, req.ProductId, req.SortOrder, req.IsExcluded), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpDelete("channel-categories/{id:guid}/products/{productId:guid}")]
    public async Task<IActionResult> RemoveChannelCategoryProduct(
        Guid id, Guid productId, CancellationToken ct)
    {
        var result = await mediator.Send(new RemoveChannelCategoryProductCommand(id, productId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpPut("channel-categories/{id:guid}/groups")]
    public async Task<IActionResult> SaveChannelCategoryGroups(
        Guid id, [FromBody] SaveChannelCategoryGroupsRequest req, CancellationToken ct)
    {
        var groups = req.Groups.Select(g =>
            new GroupInput(g.ProductGroupId, g.ShowcaseProductId)).ToList();
        var result = await mediator.Send(new SaveChannelCategoryGroupsCommand(id, groups), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpPost("channel-categories/{id:guid}/sync")]
    public async Task<IActionResult> SyncChannelCategoryProducts(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new SyncChannelCategoryProductsCommand(id), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { addedCount = result.Value } });
    }

    // ─── Channel Product Groups ──────────────────────────────────────────────

    [HttpGet("channel-product-groups")]
    public async Task<IActionResult> GetChannelProductGroups(
        [FromQuery] Guid firmPlatformId,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetChannelProductGroupsQuery(firmPlatformId, status), ct);
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("channel-product-groups")]
    public async Task<IActionResult> UpsertChannelProductGroup(
        [FromBody] UpsertChannelProductGroupRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpsertChannelProductGroupCommand(
            req.FirmPlatformId, req.ProductGroupId, req.Status), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { id = result.Value } });
    }

    // ─── Channel Variant Pricing ─────────────────────────────────────────────

    /// <summary>Kanal bazlı ürün fiyatlandırmasını getirir.</summary>
    [HttpGet("channel-variants/{firmPlatformId:guid}/products/{productId:guid}/pricing")]
    public async Task<IActionResult> GetChannelVariantPricing(Guid firmPlatformId, Guid productId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetChannelVariantPricingQuery(firmPlatformId, productId), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kanal varyant fiyatı oluşturur veya günceller (upsert).</summary>
    [HttpPut("channel-variants/{firmPlatformId:guid}/variants/{variantId:guid}/price")]
    public async Task<IActionResult> SetChannelVariantPrice(
        Guid firmPlatformId, Guid variantId, [FromBody] SetChannelVariantPriceRequest req, CancellationToken ct)
    {
        Guid.TryParse(User.FindFirst("sub")?.Value, out var changedBy);
        var changedByName = User.FindFirst("full_name")?.Value ?? User.FindFirst("email")?.Value;

        var result = await mediator.Send(new SetChannelVariantPriceCommand(
            firmPlatformId, variantId, req.PriceType, req.PriceMultiplier,
            req.Price, req.CompareAtPrice, req.IsActive,
            changedBy, changedByName, req.FirmPlatformCode), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { id = result.Value } });
    }

    /// <summary>B11: kanal ürününün öne çıkarma durumunu döner.</summary>
    [HttpGet("channel-products/{firmPlatformId:guid}/products/{productId:guid}/featured")]
    public async Task<IActionResult> GetChannelProductFeatured(
        Guid firmPlatformId, Guid productId, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ECSPros.Storefront.Application.Queries.GetChannelProductFeatured.GetChannelProductFeaturedQuery(
                firmPlatformId, productId), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>B11 (K8): kanal ürününe tarih aralıklı "öne çıkar" bayrağı atar; featuredFrom null = kaldır.</summary>
    [HttpPut("channel-products/{firmPlatformId:guid}/products/{productId:guid}/featured")]
    public async Task<IActionResult> SetChannelProductFeatured(
        Guid firmPlatformId, Guid productId, [FromBody] SetChannelProductFeaturedRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new SetChannelProductFeaturedCommand(
            firmPlatformId, productId, req.FeaturedFrom, req.FeaturedUntil), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { id = result.Value } });
    }

    // ─── Kanal Ürünleri (satış görünürlüğü M2/M3) — toplu yönetim ─────────────

    /// <summary>Kanal ürünleri toplu yönetim listesi (seçili mi / durdurma durumu; arama+durum+sayfa).</summary>
    [HttpGet("channel-products/{firmPlatformId:guid}/manage")]
    public async Task<IActionResult> GetChannelProductsAdmin(
        Guid firmPlatformId, [FromQuery] string? search, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new ECSPros.Storefront.Application.Queries.GetChannelProductsAdmin.GetChannelProductsAdminQuery(
                firmPlatformId, search, status, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>"Tüm eşleşenleri seç" — filtreye uyan tüm ürün Id'leri (toplu işlem için).</summary>
    [HttpGet("channel-products/{firmPlatformId:guid}/manage/ids")]
    public async Task<IActionResult> GetChannelProductIdsAdmin(
        Guid firmPlatformId, [FromQuery] string? search, [FromQuery] string? status, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ECSPros.Storefront.Application.Queries.GetChannelProductsAdmin.GetChannelProductIdsAdminQuery(
                firmPlatformId, search, status), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>M2: verilen ürünleri kanala alır (selected=true) / kanaldan çıkarır (false).</summary>
    [HttpPost("channel-products/{firmPlatformId:guid}/bulk-select")]
    public async Task<IActionResult> BulkSetChannelProductSelection(
        Guid firmPlatformId, [FromBody] BulkSelectRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ECSPros.Storefront.Application.Commands.BulkSetChannelProductSelection.BulkSetChannelProductSelectionCommand(
                firmPlatformId, req.ProductIds ?? new(), req.Selected), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { affected = result.Value } });
    }

    /// <summary>M3: verilen ürünlerin satışını durdurur (from/until) veya durdurmayı temizler (from null).</summary>
    [HttpPost("channel-products/{firmPlatformId:guid}/bulk-stop")]
    public async Task<IActionResult> BulkSetChannelProductStop(
        Guid firmPlatformId, [FromBody] BulkStopRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ECSPros.Storefront.Application.Commands.BulkSetChannelProductStop.BulkSetChannelProductStopCommand(
                firmPlatformId, req.ProductIds ?? new(), req.From, req.Until), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { affected = result.Value } });
    }

    // ─── F1 Kanal kapsamı (docs/satis-kanali-ortak-kurgu.md §3.1) ────────────────

    /// <summary>Kanal kapsam tanımı + manuel eklenen/hariç tutulan ürünler + son sync bilgisi.</summary>
    [HttpGet("channel-products/{firmPlatformId:guid}/scope")]
    public async Task<IActionResult> GetChannelScope(Guid firmPlatformId, CancellationToken ct)
    {
        var result = await mediator.Send(new ECSPros.Storefront.Application.Queries.GetChannelScope.GetChannelScopeQuery(firmPlatformId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kapsam tanımını kaydeder (all|filter|mixed + filtre) ve hemen günceller. Yanıt: eşleşen ürün sayısı.</summary>
    [HttpPut("channel-products/{firmPlatformId:guid}/scope")]
    public async Task<IActionResult> UpsertChannelScope(Guid firmPlatformId, [FromBody] ChannelScopeRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new ECSPros.Storefront.Application.Commands.UpsertChannelScope.UpsertChannelScopeCommand(
            firmPlatformId, req.FillType, req.FilterDef), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { matched = result.Value } });
    }

    /// <summary>Kaydetmeden filtreyi çalıştırır: eşleşen / toplam ürün sayısı.</summary>
    [HttpPost("channel-products/{firmPlatformId:guid}/scope/preview")]
    public async Task<IActionResult> PreviewChannelScope(Guid firmPlatformId, [FromBody] ChannelScopeRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new ECSPros.Storefront.Application.Queries.PreviewChannelScope.PreviewChannelScopeQuery(
            firmPlatformId, req.FillType, req.FilterDef), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kapsamı yeniden hesaplar (filter|mixed kanal).</summary>
    [HttpPost("channel-products/{firmPlatformId:guid}/scope/sync")]
    public async Task<IActionResult> SyncChannelScope(Guid firmPlatformId, CancellationToken ct)
    {
        var result = await mediator.Send(new ECSPros.Storefront.Application.Commands.SyncChannelScope.SyncChannelScopeCommand(firmPlatformId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { matched = result.Value } });
    }

    /// <summary>Kapsamda manuel işlem: include (kapsama ekle) | exclude (kalıcı hariç tut) | clear (manuel kararı kaldır).</summary>
    [HttpPost("channel-products/{firmPlatformId:guid}/scope/manual")]
    public async Task<IActionResult> SetChannelScopeManual(Guid firmPlatformId, [FromBody] ChannelScopeManualRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new ECSPros.Storefront.Application.Commands.SetChannelScopeManual.SetChannelScopeManualCommand(
            firmPlatformId, req.ProductIds ?? new(), req.Action), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { affected = result.Value } });
    }
}

// ─── Request Records ─────────────────────────────────────────────────────────

public record ChannelScopeRequest(string FillType, Dictionary<string, object>? FilterDef);
public record ChannelScopeManualRequest(List<Guid>? ProductIds, string Action);

public record SetChannelProductFeaturedRequest(DateTime? FeaturedFrom, DateTime? FeaturedUntil);

public record BulkSelectRequest(List<Guid>? ProductIds, bool Selected);
public record BulkStopRequest(List<Guid>? ProductIds, DateTime? From, DateTime? Until);

public record CreateMenuRequest(
    Guid FirmPlatformId,
    string Code,
    Dictionary<string, string> NameI18n,
    string MenuType = "header",
    int SortOrder = 0);

public record UpdateMenuRequest(
    Dictionary<string, string> NameI18n,
    string MenuType,
    bool IsActive,
    int SortOrder);

public record SaveNodesRequest(List<NavNodeInput> Nodes);

public record CreateChannelCategoryRequest(
    Guid FirmPlatformId,
    Guid? ParentId,
    Dictionary<string, string> NameI18n,
    string Slug,
    string FillType = "manual",
    Dictionary<string, object>? FilterDef = null,
    int SortOrder = 0,
    string? DisplayImageUrl = null,
    string? BadgeLabel = null);

public record UpdateChannelCategoryRequest(
    Guid? ParentId,
    Dictionary<string, string> NameI18n,
    string Slug,
    string Status,
    string FillType,
    string? ListingMode,
    Dictionary<string, object>? FilterDef,
    int SortOrder,
    string? DisplayImageUrl,
    string? BadgeLabel,
    Dictionary<string, string>? MetaTitleI18n,
    Dictionary<string, string>? MetaDescriptionI18n,
    string? OgImageUrl,
    Dictionary<string, string>? OgTitleI18n,
    string? GoogleCategoryId = null);

public record AddChannelCategoryProductRequest(
    Guid ProductId,
    int SortOrder = 0,
    bool IsExcluded = false);

public record GroupRequestItem(Guid ProductGroupId, Guid? ShowcaseProductId);

public record SaveChannelCategoryGroupsRequest(List<GroupRequestItem> Groups);

public record UpsertChannelProductGroupRequest(
    Guid FirmPlatformId,
    Guid ProductGroupId,
    string Status = "active");

public record SetChannelVariantPriceRequest(
    string? PriceType,
    decimal? PriceMultiplier,
    decimal? Price,
    decimal? CompareAtPrice,
    bool IsActive = true,
    string? FirmPlatformCode = null);
