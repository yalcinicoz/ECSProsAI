using ECSPros.Promotion.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Queries.GetCampaignDetail;

// F1: kampanya detay/düzenleme formu — tüm alanlar + ürün kapsamı (manuel ürün id'leri).
public record GetCampaignDetailQuery(Guid Id) : IRequest<Result<CampaignDetailDto>>;

public record CampaignDetailDto(
    Guid Id,
    Guid FirmPlatformId,
    Guid CampaignTypeId,
    string CampaignTypeCode,
    string Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? DescriptionI18n,
    string? BadgeLabel,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive,
    int Priority,
    Dictionary<string, object> Settings,
    string FillType,
    Dictionary<string, object>? FilterDef,
    List<Guid> ManualProductIds);

public class GetCampaignDetailQueryHandler(IPromotionDbContext db)
    : IRequestHandler<GetCampaignDetailQuery, Result<CampaignDetailDto>>
{
    public async Task<Result<CampaignDetailDto>> Handle(GetCampaignDetailQuery request, CancellationToken ct)
    {
        var c = await db.Campaigns.AsNoTracking()
            .Include(x => x.CampaignType)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (c is null) return Result.Failure<CampaignDetailDto>("Kampanya bulunamadı.");

        var manualIds = await db.CampaignProducts.AsNoTracking()
            .Where(p => p.CampaignId == c.Id && p.AddedType == "manual" && p.ProductId.HasValue)
            .Select(p => p.ProductId!.Value)
            .ToListAsync(ct);

        return Result.Success(new CampaignDetailDto(
            c.Id, c.FirmPlatformId, c.CampaignTypeId, c.CampaignType.Code, c.Code,
            c.NameI18n, c.DescriptionI18n, c.BadgeLabel, c.StartsAt, c.EndsAt,
            c.IsActive, c.Priority, c.Settings, c.FillType, c.FilterDef, manualIds));
    }
}
