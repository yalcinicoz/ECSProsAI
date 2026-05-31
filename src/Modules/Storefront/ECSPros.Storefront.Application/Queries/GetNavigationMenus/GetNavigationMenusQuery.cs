using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetNavigationMenus;

public record GetNavigationMenusQuery(
    Guid? FirmPlatformId = null,
    bool ActiveOnly = false) : IRequest<Result<List<NavigationMenuSummaryDto>>>;

public record NavigationMenuSummaryDto(
    Guid Id,
    Guid FirmPlatformId,
    string Code,
    Dictionary<string, string> NameI18n,
    string MenuType,
    bool IsActive,
    int SortOrder);

public class GetNavigationMenusQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetNavigationMenusQuery, Result<List<NavigationMenuSummaryDto>>>
{
    public async Task<Result<List<NavigationMenuSummaryDto>>> Handle(GetNavigationMenusQuery request, CancellationToken ct)
    {
        var query = db.NavigationMenus.AsQueryable();

        if (request.FirmPlatformId.HasValue)
            query = query.Where(m => m.FirmPlatformId == request.FirmPlatformId);

        if (request.ActiveOnly)
            query = query.Where(m => m.IsActive);

        var items = await query
            .OrderBy(m => m.SortOrder)
            .Select(m => new NavigationMenuSummaryDto(
                m.Id, m.FirmPlatformId, m.Code, m.NameI18n, m.MenuType, m.IsActive, m.SortOrder))
            .ToListAsync(ct);

        return Result.Success(items);
    }
}
