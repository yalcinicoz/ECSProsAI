using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.SaveNavNodes;

/// <summary>
/// Bir menünün tüm NavNode ağacını toplu olarak kaydeder (replace).
/// Mevcut node'lar soft-delete edilir, yeni ağaç sıfırdan oluşturulur.
/// </summary>
public record SaveNavNodesCommand(
    Guid MenuId,
    List<NavNodeInput> Nodes) : IRequest<Result<bool>>;

public record NavNodeInput(
    Dictionary<string, string>? NameOverrideI18n,
    string NodeType,
    Guid? ChannelCategoryId,
    string? Slug,
    string? CustomUrl,
    string? ImageUrl,
    string? BadgeLabel,
    string? Icon,
    bool OpenInNewTab,
    bool IsActive,
    int SortOrder,
    Dictionary<string, string>? SeoTitleI18n = null,
    Dictionary<string, string>? SeoDescriptionI18n = null,
    string? CanonicalUrl = null,
    string? OgImageUrl = null,
    Dictionary<string, string>? OgTitleI18n = null,
    List<NavNodeInput>? Children = null);

public class SaveNavNodesCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<SaveNavNodesCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(SaveNavNodesCommand request, CancellationToken ct)
    {
        var menu = await db.NavigationMenus.FirstOrDefaultAsync(m => m.Id == request.MenuId, ct);
        if (menu is null) return Result.Failure<bool>("Menü bulunamadı.");

        var existing = await db.NavNodes
            .Where(n => n.NavigationMenuId == request.MenuId)
            .ToListAsync(ct);

        foreach (var node in existing)
        {
            node.IsDeleted = true;
            node.DeletedAt = DateTime.UtcNow;
        }

        var now = DateTime.UtcNow;
        CreateNodes(db, request.Nodes, request.MenuId, null, now);

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }

    private static void CreateNodes(
        IStorefrontDbContext db,
        List<NavNodeInput>? inputs,
        Guid menuId,
        Guid? parentId,
        DateTime now)
    {
        if (inputs is null) return;

        foreach (var input in inputs)
        {
            var entity = new NavNode
            {
                Id = Guid.NewGuid(),
                NavigationMenuId = menuId,
                ParentNavNodeId = parentId,
                ChannelCategoryId = input.ChannelCategoryId,
                NameOverrideI18n = input.NameOverrideI18n,
                NodeType = input.NodeType,
                Slug = input.Slug,
                CustomUrl = input.CustomUrl,
                ImageUrl = input.ImageUrl,
                BadgeLabel = input.BadgeLabel,
                Icon = input.Icon,
                OpenInNewTab = input.OpenInNewTab,
                IsActive = input.IsActive,
                SortOrder = input.SortOrder,
                SeoTitleI18n = input.SeoTitleI18n,
                SeoDescriptionI18n = input.SeoDescriptionI18n,
                CanonicalUrl = input.CanonicalUrl,
                OgImageUrl = input.OgImageUrl,
                OgTitleI18n = input.OgTitleI18n,
                CreatedAt = now
            };

            db.NavNodes.Add(entity);

            CreateNodes(db, input.Children, menuId, entity.Id, now);
        }
    }
}
