using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetChannelCategories;

public record GetChannelCategoriesQuery(
    Guid FirmPlatformId,
    bool ActiveOnly = false) : IRequest<Result<List<ChannelCategoryListItemDto>>>;

public record ChannelCategoryListItemDto(
    Guid Id,
    Guid? ParentId,
    Dictionary<string, string> NameI18n,
    string Slug,
    string Status,
    string FillType,
    int SortOrder,
    string? DisplayImageUrl,
    string? BadgeLabel,
    int ProductGroupCount);

public class GetChannelCategoriesQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetChannelCategoriesQuery, Result<List<ChannelCategoryListItemDto>>>
{
    public async Task<Result<List<ChannelCategoryListItemDto>>> Handle(
        GetChannelCategoriesQuery request, CancellationToken ct)
    {
        var query = db.ChannelCategories
            .AsNoTracking()
            .Where(c => c.FirmPlatformId == request.FirmPlatformId);

        if (request.ActiveOnly)
            query = query.Where(c => c.Status == "published");

        var items = await query
            .OrderBy(c => c.SortOrder)
            .Select(c => new ChannelCategoryListItemDto(
                c.Id, c.ParentId, c.NameI18n, c.Slug, c.Status, c.FillType,
                c.SortOrder, c.DisplayImageUrl, c.BadgeLabel,
                c.CategoryGroups.Count))
            .ToListAsync(ct);

        return Result.Success(items);
    }
}
