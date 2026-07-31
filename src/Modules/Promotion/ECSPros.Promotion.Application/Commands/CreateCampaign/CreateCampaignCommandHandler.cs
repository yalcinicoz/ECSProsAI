using ECSPros.Catalog.Application.Services;
using ECSPros.Promotion.Application.Services;
using ECSPros.Promotion.Domain.Entities;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Commands.CreateCampaign;

public class CreateCampaignCommandHandler(
    IPromotionDbContext context,
    ICatalogDbContext catDb,
    IChannelPricingService pricing,
    IStockService stock)
    : IRequestHandler<CreateCampaignCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateCampaignCommand request, CancellationToken ct)
    {
        if (await context.Campaigns.AnyAsync(c => c.Code == request.Code, ct))
            return Result.Failure<Guid>($"'{request.Code}' kampanya kodu zaten mevcut.");

        var type = await context.CampaignTypes.FirstOrDefaultAsync(t => t.Id == request.CampaignTypeId, ct);
        if (type is null) return Result.Failure<Guid>("Kampanya tipi bulunamadı.");

        var hata = CampaignSettingsValidator.Validate(type.SettingsSchema, request.Settings);
        if (hata is not null) return Result.Failure<Guid>(hata);

        if (request.FirmPlatformId == Guid.Empty)
            return Result.Failure<Guid>("Platform seçilmedi.");

        var fill = NormalizeFill(request.FillType);

        var campaign = new Campaign
        {
            FirmPlatformId = request.FirmPlatformId,
            CampaignTypeId = request.CampaignTypeId,
            Code = request.Code,
            NameI18n = request.NameI18n,
            DescriptionI18n = request.DescriptionI18n,
            BadgeLabel = request.BadgeLabel,
            StartsAt = ToUtc(request.StartsAt),
            EndsAt = request.EndsAt is { } e ? ToUtc(e) : null,
            Priority = request.Priority,
            IsActive = request.IsActive,
            Settings = request.Settings,
            FillType = fill,
            FilterDef = fill is "filter" or "mixed" ? request.FilterDef : null,
            CreatedBy = request.CreatedBy,
        };

        context.Campaigns.Add(campaign);
        await context.SaveChangesAsync(ct);

        await CampaignProductMaterializer.SyncAsync(
            context, catDb, pricing, stock, campaign,
            request.ManualProductIds ?? [], request.ExcludedProductIds ?? [], ct);
        await context.SaveChangesAsync(ct);

        return Result.Success(campaign.Id);
    }

    private static string NormalizeFill(string? fill) =>
        fill is "manual" or "filter" or "mixed" ? fill : "all";

    private static DateTime ToUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();
}
