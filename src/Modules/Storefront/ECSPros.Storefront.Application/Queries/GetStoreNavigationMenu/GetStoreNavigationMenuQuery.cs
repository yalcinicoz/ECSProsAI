using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Application.Queries.GetNavigationMenuDetail;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetStoreNavigationMenu;

/// <summary>
/// Store (müşteriye dönük) taraf: code + firmPlatformId ile aktif menüyü döner.
/// Sadece IsActive=true node'ları döner.
/// </summary>
public record GetStoreNavigationMenuQuery(
    string Code,
    Guid FirmPlatformId) : IRequest<Result<NavigationMenuDetailDto>>;

public class GetStoreNavigationMenuQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetStoreNavigationMenuQuery, Result<NavigationMenuDetailDto>>
{
    public async Task<Result<NavigationMenuDetailDto>> Handle(GetStoreNavigationMenuQuery request, CancellationToken ct)
    {
        var menu = await db.NavigationMenus
            .AsNoTracking()
            .Include(m => m.Nodes)
            .FirstOrDefaultAsync(m =>
                m.Code == request.Code &&
                m.FirmPlatformId == request.FirmPlatformId &&
                m.IsActive, ct);

        if (menu is null) return Result.Failure<NavigationMenuDetailDto>("Menü bulunamadı.");

        var flatNodes = menu.Nodes
            .Where(n => !n.IsDeleted && n.IsActive)
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
