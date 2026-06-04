using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetChannelCategoryDetail;

public record GetChannelCategoryDetailQuery(Guid Id) : IRequest<Result<ChannelCategoryDetailDto>>;

public record ChannelCategoryDetailDto(
    Guid Id,
    Guid FirmPlatformId,
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
    Dictionary<string, string>? OgTitleI18n,
    List<Guid> ProductGroupIds,
    CoverageDto Coverage);

public record CoverageDto(int AssignedGroupCount, int CoveredGroupCount, List<Guid> UncoveredGroupIds);

public class GetChannelCategoryDetailQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetChannelCategoryDetailQuery, Result<ChannelCategoryDetailDto>>
{
    public async Task<Result<ChannelCategoryDetailDto>> Handle(
        GetChannelCategoryDetailQuery request, CancellationToken ct)
    {
        var cat = await db.ChannelCategories
            .AsNoTracking()
            .Include(c => c.CategoryGroups)
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct);

        if (cat is null) return Result.Failure<ChannelCategoryDetailDto>("Kanal kategorisi bulunamadı.");

        var groupIds = cat.CategoryGroups.Select(g => g.ProductGroupId).ToList();

        // Coverage: kanalda active olan gruplar içinden bu kategoride kapsanmayanlar
        var assignedGroupIds = await db.ChannelProductGroups
            .Where(g => g.FirmPlatformId == cat.FirmPlatformId && g.Status == "active")
            .Select(g => g.ProductGroupId)
            .ToListAsync(ct);

        var coveredGroupIds = await db.ChannelCategoryGroups
            .Where(g => db.ChannelCategories
                .Any(c => c.Id == g.ChannelCategoryId && c.FirmPlatformId == cat.FirmPlatformId && c.Status == "published"))
            .Select(g => g.ProductGroupId)
            .Distinct()
            .ToListAsync(ct);

        var uncovered = assignedGroupIds.Except(coveredGroupIds).ToList();
        var coverage = new CoverageDto(assignedGroupIds.Count, coveredGroupIds.Count, uncovered);

        return Result.Success(new ChannelCategoryDetailDto(
            cat.Id, cat.FirmPlatformId, cat.ParentId, cat.NameI18n,
            cat.Slug, cat.Status, cat.FillType, cat.FilterDef,
            cat.SortOrder, cat.DisplayImageUrl, cat.BadgeLabel,
            cat.MetaTitleI18n, cat.MetaDescriptionI18n, cat.OgImageUrl, cat.OgTitleI18n,
            groupIds, coverage));
    }
}
