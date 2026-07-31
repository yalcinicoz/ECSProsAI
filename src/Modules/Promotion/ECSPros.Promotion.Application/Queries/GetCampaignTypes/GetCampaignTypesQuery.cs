using ECSPros.Promotion.Application.Services;
using ECSPros.Promotion.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Queries.GetCampaignTypes;

// P3: kampanya oluşturma formunun tip seçicisi. SettingsSchema (parametre şablonu) döner —
// platform kampanya formu bu şablondan üretilir (Faz 0, docs/kampanya-tip-sablonlari-taslak.md).
public record GetCampaignTypesQuery(bool ActiveOnly = true) : IRequest<Result<List<CampaignTypeDto>>>;

public record CampaignTypeDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? DescriptionI18n,
    string Scope,
    bool RequiresProducts,
    bool ProductPriceDisplay,
    bool IsStackable,
    bool IsActive,
    int SortOrder,
    List<CampaignSchemaField>? SettingsSchema);

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
                t.Scope, t.RequiresProducts, t.ProductPriceDisplay,
                t.IsStackable, t.IsActive, t.SortOrder, t.SettingsSchema))
            .ToListAsync(ct));
    }
}
