using ECSPros.Storefront.Application.Commands.AddChannelCategoryProduct;
using ECSPros.Storefront.Application.Commands.CreateChannelCategory;
using ECSPros.Storefront.Application.Commands.CreateNavigationMenu;
using ECSPros.Storefront.Application.Commands.DeleteChannelCategory;
using ECSPros.Storefront.Application.Commands.DeleteNavigationMenu;
using ECSPros.Storefront.Application.Commands.RemoveChannelCategoryProduct;
using ECSPros.Storefront.Application.Commands.SaveChannelCategoryGroups;
using ECSPros.Storefront.Application.Commands.SaveNavNodes;
using ECSPros.Storefront.Application.Commands.SyncChannelCategoryProducts;
using ECSPros.Storefront.Application.Commands.UpdateChannelCategory;
using ECSPros.Storefront.Application.Commands.UpdateNavigationMenu;
using ECSPros.Storefront.Application.Commands.UpsertChannelProductGroup;
using ECSPros.Storefront.Application.Queries.GetChannelCategories;
using ECSPros.Storefront.Application.Queries.GetChannelCategoryDetail;
using ECSPros.Storefront.Application.Queries.GetChannelCategoryProducts;
using ECSPros.Storefront.Application.Queries.GetChannelProductGroups;
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
            req.FilterDef, req.SortOrder, req.DisplayImageUrl, req.BadgeLabel,
            req.MetaTitleI18n, req.MetaDescriptionI18n, req.OgImageUrl, req.OgTitleI18n), ct);
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
        var result = await mediator.Send(new SaveChannelCategoryGroupsCommand(id, req.ProductGroupIds), ct);
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
}

// ─── Request Records ─────────────────────────────────────────────────────────

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
    Dictionary<string, object>? FilterDef,
    int SortOrder,
    string? DisplayImageUrl,
    string? BadgeLabel,
    Dictionary<string, string>? MetaTitleI18n,
    Dictionary<string, string>? MetaDescriptionI18n,
    string? OgImageUrl,
    Dictionary<string, string>? OgTitleI18n);

public record AddChannelCategoryProductRequest(
    Guid ProductId,
    int SortOrder = 0,
    bool IsExcluded = false);

public record SaveChannelCategoryGroupsRequest(List<Guid> ProductGroupIds);

public record UpsertChannelProductGroupRequest(
    Guid FirmPlatformId,
    Guid ProductGroupId,
    string Status = "active");
