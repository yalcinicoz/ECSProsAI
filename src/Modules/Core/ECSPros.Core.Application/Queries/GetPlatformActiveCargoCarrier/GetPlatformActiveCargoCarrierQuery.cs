using ECSPros.Core.Application.Queries.GetCargoCarriersByFirmIntegrations;
using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetPlatformActiveCargoCarrier;

/// <summary>
/// H2: Platformun aktif kargo anlaşmasını döner (sipariş onay sayfası "Kargo Firması"
/// satırı — gönderi henüz yokken firma aktif anlaşmadan bilinir). Çözümleme kuralı:
/// platforma özel kayıt (FirmPlatformId eşleşen) firma-geneline (FirmPlatformId null)
/// tercih edilir; aynı seviyede birden çok kayıt varsa ilki (Name sırasıyla) döner;
/// hiç yoksa null — satır gizli.
/// </summary>
public record GetPlatformActiveCargoCarrierQuery(Guid FirmPlatformId)
    : IRequest<Result<CargoCarrierDto?>>;

public class GetPlatformActiveCargoCarrierQueryHandler
    : IRequestHandler<GetPlatformActiveCargoCarrierQuery, Result<CargoCarrierDto?>>
{
    private readonly ICoreDbContext _db;

    public GetPlatformActiveCargoCarrierQueryHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<CargoCarrierDto?>> Handle(
        GetPlatformActiveCargoCarrierQuery request, CancellationToken ct)
    {
        var kayit = await _db.FirmPlatformIntegrations
            .Where(fi => fi.IsActive
                         && fi.IntegrationService.ServiceType == "cargo"
                         && (fi.FirmPlatformId == request.FirmPlatformId
                             || (fi.FirmPlatformId == null
                                 && fi.Firm.FirmPlatforms.Any(p => p.Id == request.FirmPlatformId))))
            .OrderBy(fi => fi.FirmPlatformId == null ? 1 : 0)
            .ThenBy(fi => fi.Name)
            .Select(fi => new
            {
                SozlesmeAdi = fi.Name,
                ServisAd = fi.IntegrationService.NameI18n,
                fi.IntegrationService.LogoUrl,
                fi.IntegrationService.TrackingUrlTemplate
            })
            .FirstOrDefaultAsync(ct);

        if (kayit is null)
            return Result.Success<CargoCarrierDto?>(null);

        // Ad önceliği GetCargoCarriersByFirmIntegrations ile aynı: servis (firma) adı;
        // sözleşme etiketi yalnız yedek.
        return Result.Success<CargoCarrierDto?>(new CargoCarrierDto(
            kayit.ServisAd.GetValueOrDefault("tr")
                ?? kayit.ServisAd.Values.FirstOrDefault()
                ?? (string.IsNullOrWhiteSpace(kayit.SozlesmeAdi) ? "Kargo" : kayit.SozlesmeAdi),
            kayit.LogoUrl,
            kayit.TrackingUrlTemplate));
    }
}
