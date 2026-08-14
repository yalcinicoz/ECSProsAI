using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.UpdateNavigationSettings;

/// <summary>
/// Site navigasyon ayarları (2026-08-14): FirmPlatform.Settings jsonb'sine YALNIZ
/// "navigation" anahtarını merge eder (diğer ayarlar korunur — productCard kalıbıyla aynı).
/// megaMenuHover: üst menü linklerinin üzerine gelince mega menü açılsın mı
/// (varsayılan KAPALI; kapalıyken mega menü yalnız "Kategoriler" tıklamasıyla açılır,
/// menü linkleri tıklamada doğrudan ürün listesine gider).
/// </summary>
public record UpdateNavigationSettingsCommand(
    Guid FirmPlatformId,
    bool MegaMenuHover) : IRequest<Result<bool>>;

public class UpdateNavigationSettingsCommandHandler(ICoreDbContext db)
    : IRequestHandler<UpdateNavigationSettingsCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateNavigationSettingsCommand request, CancellationToken ct)
    {
        var platform = await db.FirmPlatforms.FirstOrDefaultAsync(p => p.Id == request.FirmPlatformId, ct);
        if (platform is null) return Result.Failure<bool>("Platform bulunamadı.");

        var settings = platform.Settings is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(platform.Settings);
        settings["navigation"] = new Dictionary<string, object>
        {
            ["megaMenuHover"] = request.MegaMenuHover
        };
        platform.Settings = settings;
        platform.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
