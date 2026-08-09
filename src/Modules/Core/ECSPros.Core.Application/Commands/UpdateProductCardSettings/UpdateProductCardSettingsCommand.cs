using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.UpdateProductCardSettings;

/// <summary>
/// Ürün Kartı F1 (2026-08-09): kanal bazlı kart elementi aç/kapat ayarları —
/// FirmPlatform.Settings jsonb'sine YALNIZ "productCard" anahtarını merge eder
/// (diğer ayarlar korunur; ChannelsPage'in tam-replace davranışından etkilenmez,
/// o sayfa şema-dışı anahtarları geri yazar). Panel Storefront → Ürün Kartı ekranından yazılır.
/// </summary>
public record UpdateProductCardSettingsCommand(
    Guid FirmPlatformId,
    bool VideoBadge,
    bool SponsorBadge,
    bool ColorBadge,
    bool GalleryDots,
    bool FavoriteButton,
    bool CollectionButton,
    bool Rating,
    bool DiscountRow,
    bool CampaignPriceRow,
    bool CampaignBand,
    int CampaignBandSlot) : IRequest<Result<bool>>;

public class UpdateProductCardSettingsCommandHandler(ICoreDbContext db)
    : IRequestHandler<UpdateProductCardSettingsCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateProductCardSettingsCommand request, CancellationToken ct)
    {
        if (request.CampaignBandSlot is < 1 or > 3)
            return Result.Failure<bool>("Kampanya bandı slotu 1-3 aralığında olmalı.");

        var platform = await db.FirmPlatforms.FirstOrDefaultAsync(p => p.Id == request.FirmPlatformId, ct);
        if (platform is null) return Result.Failure<bool>("Platform bulunamadı.");

        var settings = platform.Settings is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(platform.Settings);
        settings["productCard"] = new Dictionary<string, object>
        {
            ["videoBadge"] = request.VideoBadge,
            ["sponsorBadge"] = request.SponsorBadge,
            ["colorBadge"] = request.ColorBadge,
            ["galleryDots"] = request.GalleryDots,
            ["favoriteButton"] = request.FavoriteButton,
            ["collectionButton"] = request.CollectionButton,
            ["rating"] = request.Rating,
            ["discountRow"] = request.DiscountRow,
            ["campaignPriceRow"] = request.CampaignPriceRow,
            ["campaignBand"] = request.CampaignBand,
            ["campaignBandSlot"] = request.CampaignBandSlot
        };
        platform.Settings = settings;
        platform.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
