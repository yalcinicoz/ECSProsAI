using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetNavigationMenuDetail;

public record GetNavigationMenuDetailQuery(Guid Id) : IRequest<Result<NavigationMenuDetailDto>>;

public record NavigationMenuDetailDto(
    Guid Id,
    Guid FirmPlatformId,
    string Code,
    Dictionary<string, string> NameI18n,
    string MenuType,
    bool IsActive,
    int SortOrder,
    List<NavNodeDto> Nodes);

public record NavNodeDto(
    Guid Id,
    Guid? ParentNavNodeId,
    Guid? ChannelCategoryId,
    Dictionary<string, string>? NameOverrideI18n,
    string NodeType,
    string? Slug,
    string? CustomUrl,
    string? ImageUrl,
    string? BadgeLabel,
    string? Icon,
    bool OpenInNewTab,
    bool IsActive,
    int SortOrder,
    Dictionary<string, string>? SeoTitleI18n,
    Dictionary<string, string>? SeoDescriptionI18n,
    string? CanonicalUrl,
    string? OgImageUrl,
    Dictionary<string, string>? OgTitleI18n,
    List<NavNodeDto> Children);

public class GetNavigationMenuDetailQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetNavigationMenuDetailQuery, Result<NavigationMenuDetailDto>>
{
    public async Task<Result<NavigationMenuDetailDto>> Handle(GetNavigationMenuDetailQuery request, CancellationToken ct)
    {
        var menu = await db.NavigationMenus
            .AsNoTracking()
            .Include(m => m.Nodes)
            .FirstOrDefaultAsync(m => m.Id == request.Id, ct);

        if (menu is null) return Result.Failure<NavigationMenuDetailDto>("Menü bulunamadı.");

        var flatNodes = menu.Nodes
            .Where(n => !n.IsDeleted)
            .OrderBy(n => n.SortOrder)
            .ToList();

        return Result.Success(new NavigationMenuDetailDto(
            menu.Id, menu.FirmPlatformId, menu.Code, menu.NameI18n,
            menu.MenuType, menu.IsActive, menu.SortOrder,
            BuildTree(flatNodes, null)));
    }

    private static List<NavNodeDto> BuildTree(
        List<Domain.Entities.NavNode> all, Guid? parentId)
    {
        return all
            .Where(n => n.ParentNavNodeId == parentId)
            .Select(n => new NavNodeDto(
                n.Id, n.ParentNavNodeId, n.ChannelCategoryId, n.NameOverrideI18n,
                n.NodeType, n.Slug, n.CustomUrl, n.ImageUrl, n.BadgeLabel,
                n.Icon, n.OpenInNewTab, n.IsActive, n.SortOrder,
                n.SeoTitleI18n, n.SeoDescriptionI18n, n.CanonicalUrl,
                n.OgImageUrl, n.OgTitleI18n,
                BuildTree(all, n.Id)))
            .ToList();
    }
}
