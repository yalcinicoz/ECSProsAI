using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetCargoOptions;

/// <summary>
/// Teslimat adımı kargo seçenekleri (2026-07-22): adresin MAHALLESİNE atanmış kargo
/// şirketleri (mahalle önceliğiyle) — mahalleye atama yoksa firmadaki TÜM aktif kargo
/// entegrasyonları genel ("default" kural) önceliğiyle döner. Genel kuralı olmayan
/// aktif kargolar listenin sonuna ada göre eklenir (atama zorunlu değil — spec kararı).
/// </summary>
public record GetCargoOptionsQuery(Guid FirmPlatformId, Guid? NeighborhoodId)
    : IRequest<Result<List<CargoOptionDto>>>;

public record CargoOptionDto(Guid IntegrationId, string Name, string ServiceCode, bool MahalleyeOzel);

public class GetCargoOptionsQueryHandler(ICoreDbContext db)
    : IRequestHandler<GetCargoOptionsQuery, Result<List<CargoOptionDto>>>
{
    public async Task<Result<List<CargoOptionDto>>> Handle(GetCargoOptionsQuery request, CancellationToken ct)
    {
        var firmId = await db.FirmPlatforms
            .Where(fp => fp.Id == request.FirmPlatformId)
            .Select(fp => (Guid?)fp.FirmId)
            .FirstOrDefaultAsync(ct);
        if (firmId is null)
            return Result.Failure<List<CargoOptionDto>>("Platform bulunamadı.");

        var aktifKargolar = await db.FirmPlatformIntegrations
            .Where(fi => fi.FirmId == firmId && fi.IsActive
                         && fi.IntegrationService.ServiceType == "cargo"
                         && (fi.FirmPlatformId == null || fi.FirmPlatformId == request.FirmPlatformId))
            .Select(fi => new
            {
                fi.Id,
                fi.Name,
                ServisAd = fi.IntegrationService.NameI18n,
                fi.IntegrationService.Code,
            })
            .ToListAsync(ct);
        if (aktifKargolar.Count == 0)
            return Result.Success(new List<CargoOptionDto>());

        var ad = (string? ozel, Dictionary<string, string> i18n, string kod) =>
            !string.IsNullOrWhiteSpace(ozel) ? ozel!
            : i18n.TryGetValue("tr", out var tr) ? tr
            : i18n.Values.FirstOrDefault() ?? kod;

        // 1) Mahalleye özel atama varsa YALNIZ o liste geçerlidir (mahalle önceliğiyle)
        if (request.NeighborhoodId is { } mahalleId)
        {
            var mahalleKurallari = await db.CargoRules
                .Where(r => r.FirmId == firmId && r.RuleType == "neighborhood"
                            && r.NeighborhoodId == mahalleId && r.IsActive)
                .OrderByDescending(r => r.Priority)
                .Select(r => new { r.FirmPlatformIntegrationId, r.Priority })
                .ToListAsync(ct);

            var mahalleListesi = mahalleKurallari
                .Select(r => aktifKargolar.FirstOrDefault(k => k.Id == r.FirmPlatformIntegrationId))
                .Where(k => k is not null)
                .Select(k => new CargoOptionDto(k!.Id, ad(k.Name, k.ServisAd, k.Code), k.Code, true))
                .ToList();
            if (mahalleListesi.Count > 0)
                return Result.Success(mahalleListesi);
        }

        // 2) Atama yok → tüm aktif kargolar, genel öncelik (default kural) sırasıyla
        var genelOncelik = await db.CargoRules
            .Where(r => r.FirmId == firmId && r.RuleType == "default" && r.IsActive)
            .ToDictionaryAsync(r => r.FirmPlatformIntegrationId, r => r.Priority, ct);

        var liste = aktifKargolar
            .OrderByDescending(k => genelOncelik.TryGetValue(k.Id, out var p) ? p : int.MinValue)
            .ThenBy(k => ad(k.Name, k.ServisAd, k.Code))
            .Select(k => new CargoOptionDto(k.Id, ad(k.Name, k.ServisAd, k.Code), k.Code, false))
            .ToList();

        return Result.Success(liste);
    }
}
