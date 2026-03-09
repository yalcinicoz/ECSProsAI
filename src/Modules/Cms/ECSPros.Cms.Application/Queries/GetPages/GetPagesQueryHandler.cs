using ECSPros.Cms.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Cms.Application.Queries.GetPages;

public class GetPagesQueryHandler : IRequestHandler<GetPagesQuery, Result<List<PageDto>>>
{
    private readonly ICmsDbContext _context;

    public GetPagesQueryHandler(ICmsDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<PageDto>>> Handle(GetPagesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Pages.AsQueryable();

        if (request.FirmPlatformId.HasValue)
            query = query.Where(p => p.FirmPlatformId == request.FirmPlatformId);

        if (request.ActiveOnly)
            query = query.Where(p => p.IsActive);

        var items = await query
            .OrderBy(p => p.Code)
            .Select(p => new PageDto(p.Id, p.Code, p.NameI18n, p.SlugI18n, p.PageType, p.IsActive, p.PublishAt, p.UnpublishAt))
            .ToListAsync(cancellationToken);

        return Result.Success(items);
    }
}
