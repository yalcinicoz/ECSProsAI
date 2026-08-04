using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.UpdateOrderConfirmSettings;

/// <summary>
/// O1 (2026-08-04): sipariş onay politikası — FirmPlatform.Settings jsonb'sine YALNIZ
/// ilgili anahtarları merge eder (diğer ayarlar korunur): orderConfirmPolicy {cod, card}
/// + orderConfirmLinkHours. Panel Bildirim Şablonları ekranından yazılır.
/// </summary>
public record UpdateOrderConfirmSettingsCommand(
    Guid FirmPlatformId,
    string Cod,        // always | never
    string Card,       // first_order | always | never
    int LinkHours) : IRequest<Result<bool>>;

public class UpdateOrderConfirmSettingsCommandHandler(ICoreDbContext db)
    : IRequestHandler<UpdateOrderConfirmSettingsCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateOrderConfirmSettingsCommand request, CancellationToken ct)
    {
        if (request.Cod is not ("always" or "never"))
            return Result.Failure<bool>("Kapıda onay politikası always/never olmalı.");
        if (request.Card is not ("first_order" or "always" or "never"))
            return Result.Failure<bool>("Kart onay politikası first_order/always/never olmalı.");
        if (request.LinkHours is < 1 or > 168)
            return Result.Failure<bool>("Link ömrü 1-168 saat aralığında olmalı.");

        var platform = await db.FirmPlatforms.FirstOrDefaultAsync(p => p.Id == request.FirmPlatformId, ct);
        if (platform is null) return Result.Failure<bool>("Platform bulunamadı.");

        var settings = platform.Settings is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(platform.Settings);
        settings["orderConfirmPolicy"] = new Dictionary<string, object>
        {
            ["cod"] = request.Cod,
            ["card"] = request.Card
        };
        settings["orderConfirmLinkHours"] = request.LinkHours;
        platform.Settings = settings;
        platform.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
