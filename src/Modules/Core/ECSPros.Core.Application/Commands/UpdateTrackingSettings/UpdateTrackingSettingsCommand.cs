using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.UpdateTrackingSettings;

/// <summary>
/// Takip/çerez kanal ayarları (İE-1 Faz A, 2026-08-22 — plan
/// docs/reklam-analytics-entegrasyon-is-akisi.md §Faz A-4): FirmPlatform.Settings jsonb'sine
/// YALNIZ "tracking" anahtarını merge eder (navigation/productCard kalıbı; diğer ayarlar korunur).
///
/// Alanlar:
///  - purchaseAt: "confirmed" (varsayılan — sunucu taraflı satın alma event'i sipariş onaylanınca)
///    | "created" (sipariş oluşturulunca).
///  - consentBanner: HER ZAMAN true, consentDefault: HER ZAMAN "deny" — kullanıcı kararı
///    2026-08-22: EU hedefi VAR (GDPR + KVKK); panelden kapatılamaz. Değerler yine de jsonb'ye
///    yazılır ki vitrin/dispatcher tek yerden okusun (Faz C/F bu anahtarları tüketir).
///  - categories: sabit ["analytics","ads","personalization"] (Faz F kategori metinlerini ayrıca tutar).
/// </summary>
public record UpdateTrackingSettingsCommand(
    Guid FirmPlatformId,
    string PurchaseAt) : IRequest<Result<bool>>;

public class UpdateTrackingSettingsCommandHandler(ICoreDbContext db)
    : IRequestHandler<UpdateTrackingSettingsCommand, Result<bool>>
{
    public static readonly string[] PurchaseAtDegerleri = { "confirmed", "created" };

    public async Task<Result<bool>> Handle(UpdateTrackingSettingsCommand request, CancellationToken ct)
    {
        var purchaseAt = (request.PurchaseAt ?? "confirmed").Trim().ToLowerInvariant();
        if (!PurchaseAtDegerleri.Contains(purchaseAt))
            return Result.Failure<bool>("purchaseAt yalnız 'confirmed' veya 'created' olabilir.");

        var platform = await db.FirmPlatforms.FirstOrDefaultAsync(p => p.Id == request.FirmPlatformId, ct);
        if (platform is null) return Result.Failure<bool>("Platform bulunamadı.");

        var settings = platform.Settings is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(platform.Settings);
        settings["tracking"] = Varsayilan(purchaseAt);
        platform.Settings = settings;
        platform.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }

    /// <summary>"tracking" anahtarının tam içeriği — EU kararı gereği consent alanları sabit.</summary>
    public static Dictionary<string, object> Varsayilan(string purchaseAt = "confirmed") => new()
    {
        ["consentBanner"] = true,
        ["consentDefault"] = "deny",
        ["categories"] = new[] { "analytics", "ads", "personalization" },
        ["purchaseAt"] = purchaseAt
    };
}
