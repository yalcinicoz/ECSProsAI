using ECSPros.Promotion.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Queries.GetCampaignTypes;

// P3: kampanya oluşturma formunun tip seçicisi — tipler CampaignEngine'deki
// işleyicilerle eşleşir (percentage_discount/fixed_discount/buy_x_get_y/min_cart_discount)
public record GetCampaignTypesQuery(bool ActiveOnly = true) : IRequest<Result<List<CampaignTypeDto>>>;

public record CampaignTypeDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? DescriptionI18n,
    bool RequiresProducts,
    bool IsStackable,
    bool IsActive,
    int SortOrder);

public class GetCampaignTypesQueryHandler(IPromotionDbContext db)
    : IRequestHandler<GetCampaignTypesQuery, Result<List<CampaignTypeDto>>>
{
    public async Task<Result<List<CampaignTypeDto>>> Handle(GetCampaignTypesQuery request, CancellationToken ct)
    {
        var query = db.CampaignTypes.AsNoTracking();
        if (request.ActiveOnly) query = query.Where(t => t.IsActive);

        return Result.Success(await query
            .OrderBy(t => t.SortOrder)
            .Select(t => new CampaignTypeDto(
                t.Id, t.Code, t.NameI18n, t.DescriptionI18n,
                t.RequiresProducts, t.IsStackable, t.IsActive, t.SortOrder))
            .ToListAsync(ct));
    }
}
