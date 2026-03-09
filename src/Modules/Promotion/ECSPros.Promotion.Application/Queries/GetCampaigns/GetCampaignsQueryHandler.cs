using ECSPros.Promotion.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Queries.GetCampaigns;

public class GetCampaignsQueryHandler : IRequestHandler<GetCampaignsQuery, Result<List<CampaignDto>>>
{
    private readonly IPromotionDbContext _context;

    public GetCampaignsQueryHandler(IPromotionDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<CampaignDto>>> Handle(GetCampaignsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Campaigns.AsQueryable();

        if (request.ActiveOnly)
        {
            var now = DateTime.UtcNow;
            query = query.Where(c => c.IsActive && c.StartsAt <= now && (c.EndsAt == null || c.EndsAt >= now));
        }

        var items = await query
            .OrderByDescending(c => c.Priority)
            .Select(c => new CampaignDto(c.Id, c.Code, c.NameI18n, c.StartsAt, c.EndsAt, c.IsActive, c.Priority, c.ProductSelectionType))
            .ToListAsync(cancellationToken);

        return Result.Success(items);
    }
}
