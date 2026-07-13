using ECSPros.Cms.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Cms.Application.Queries.GetPageDetail;

public class GetPageDetailQueryHandler : IRequestHandler<GetPageDetailQuery, Result<PageDetailDto>>
{
    private readonly ICmsDbContext _context;

    public GetPageDetailQueryHandler(ICmsDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PageDetailDto>> Handle(GetPageDetailQuery request, CancellationToken cancellationToken)
    {
        var page = await _context.Pages
            .AsNoTracking()
            .Include(p => p.Sections).ThenInclude(s => s.SectionType)
            .Include(p => p.Sections).ThenInclude(s => s.Items)
            .FirstOrDefaultAsync(p => p.Id == request.PageId, cancellationToken);

        if (page is null)
            return Result.Failure<PageDetailDto>("Sayfa bulunamadı.");

        return Result.Success(new PageDetailDto(
            page.Id,
            page.FirmPlatformId,
            page.TemplateId,
            page.Code,
            page.NameI18n,
            page.SlugI18n,
            page.PageType,
            page.TargetGender,
            page.TargetCategoryId,
            page.MetaTitleI18n,
            page.MetaDescriptionI18n,
            page.IsActive,
            page.PublishAt,
            page.UnpublishAt,
            page.CreatedAt,
            page.Sections
                .OrderBy(s => s.SortOrder)
                .Select(s => new PageSectionDto(
                    s.Id,
                    s.SectionType.Code,
                    s.Name,
                    s.TitleI18n,
                    s.Settings,
                    s.IsActive,
                    s.SortOrder,
                    s.UpdatedAt,
                    s.Items
                        .OrderBy(i => i.SortOrder)
                        .Select(i => new PageSectionItemDto(
                            i.Id, i.ItemType, i.TitleI18n, i.DescriptionI18n, i.IsActive, i.SortOrder))
                        .ToList()))
                .ToList()));
    }
}
