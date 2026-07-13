using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetCargoCarriersByFirmIntegrations;

/// <summary>
/// H2: Gönderilerin FirmIntegrationId'lerinden kargo firması kimliğini (ad/logo/takip URL
/// şablonu) çözer — yalnız cargo tipli entegrasyonlar döner; eşleşmeyen id sözlükte olmaz
/// (çağıran firma bilinmiyor gibi davranır — Shipment.FirmIntegrationId FK'sizdir, eski/
/// yetim kayıtlarda kayıt bulunamayabilir).
/// </summary>
public record GetCargoCarriersByFirmIntegrationsQuery(List<Guid> FirmIntegrationIds)
    : IRequest<Result<Dictionary<Guid, CargoCarrierDto>>>;

public record CargoCarrierDto(
    string Name,                 // kargo firmasının adı (servis NameI18n tr) — sözleşme adı DEĞİL
    string? LogoUrl,
    string? TrackingUrlTemplate); // {trackingNumber} yer tutuculu

public class GetCargoCarriersByFirmIntegrationsQueryHandler
    : IRequestHandler<GetCargoCarriersByFirmIntegrationsQuery, Result<Dictionary<Guid, CargoCarrierDto>>>
{
    private readonly ICoreDbContext _db;

    public GetCargoCarriersByFirmIntegrationsQueryHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<Dictionary<Guid, CargoCarrierDto>>> Handle(
        GetCargoCarriersByFirmIntegrationsQuery request, CancellationToken ct)
    {
        var idler = request.FirmIntegrationIds.Distinct().ToList();
        if (idler.Count == 0)
            return Result.Success(new Dictionary<Guid, CargoCarrierDto>());

        var kayitlar = await _db.FirmPlatformIntegrations
            .Where(fi => idler.Contains(fi.Id) && fi.IntegrationService.ServiceType == "cargo")
            .Select(fi => new
            {
                fi.Id,
                SozlesmeAdi = fi.Name,
                ServisAd = fi.IntegrationService.NameI18n,
                fi.IntegrationService.LogoUrl,
                fi.IntegrationService.TrackingUrlTemplate
            })
            .ToListAsync(ct);

        // Müşteriye kargo FİRMASININ adı gösterilir (servis kataloğu) — FirmIntegration.Name
        // admin'in sözleşme etiketi olabilir ("Aras 2026 anlaşması"), yalnız servis adı
        // boşsa yedek olarak kullanılır (E2E'nin yakaladığı ayrım).
        return Result.Success(kayitlar.ToDictionary(
            k => k.Id,
            k => new CargoCarrierDto(
                k.ServisAd.GetValueOrDefault("tr")
                    ?? k.ServisAd.Values.FirstOrDefault()
                    ?? (string.IsNullOrWhiteSpace(k.SozlesmeAdi) ? "Kargo" : k.SozlesmeAdi),
                k.LogoUrl,
                k.TrackingUrlTemplate)));
    }
}
