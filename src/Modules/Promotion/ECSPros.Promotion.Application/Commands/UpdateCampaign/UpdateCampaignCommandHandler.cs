using ECSPros.Catalog.Application.Services;
using ECSPros.Promotion.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Commands.UpdateCampaign;

public class UpdateCampaignCommandHandler(
    IPromotionDbContext context,
    ICatalogDbContext catDb,
    IChannelPricingService pricing,
    IStockService stock)
    : IRequestHandler<UpdateCampaignCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateCampaignCommand request, CancellationToken ct)
    {
        var campaign = await context.Campaigns
            .Include(c => c.CampaignType)
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct);
        if (campaign is null) return Result.Failure<bool>("Kampanya bulunamadı.");

        var hata = CampaignSettingsValidator.Validate(campaign.CampaignType?.SettingsSchema, request.Settings);
        if (hata is not null) return Result.Failure<bool>(hata);

        var fill = request.FillType is "manual" or "filter" or "mixed" ? request.FillType : "all";

        campaign.NameI18n = request.NameI18n;
        campaign.DescriptionI18n = request.DescriptionI18n;
        campaign.BadgeLabel = request.BadgeLabel;
        campaign.BadgeColor = request.BadgeColor;
        campaign.StartsAt = ToUtc(request.StartsAt);
        campaign.EndsAt = request.EndsAt is { } e ? ToUtc(e) : null;
        campaign.IsActive = request.IsActive;
        campaign.Priority = request.Priority;
        campaign.Settings = request.Settings;
        campaign.FillType = fill;
        campaign.FilterDef = fill is "filter" or "mixed" ? request.FilterDef : null;
        campaign.UpdatedAt = DateTime.UtcNow;
        campaign.UpdatedBy = request.UpdatedBy;

        await CampaignProductMaterializer.SyncAsync(
            context, catDb, pricing, stock, campaign,
            request.ManualProductIds ?? [], request.ExcludedProductIds ?? [], ct);
        await context.SaveChangesAsync(ct);

        return Result.Success(true);
    }

    private static DateTime ToUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();
}
