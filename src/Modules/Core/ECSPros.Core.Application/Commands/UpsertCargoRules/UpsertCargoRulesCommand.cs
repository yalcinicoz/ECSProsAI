using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.UpsertCargoRules;

/// <summary>
/// Mahalle-kargo atama ekranı (2026-07-22): bir kapsamın (firma geneli "default" ya da
/// tek mahalle "neighborhood") kargo kural listesini KOMPLE değiştirir — listede olmayan
/// mevcut kurallar soft-delete edilir, olanların önceliği güncellenir, yeniler eklenir.
/// Priority BÜYÜK olan önce gelir (mevcut GetCargoRules DESC sıralamasıyla uyumlu);
/// ekran, üstteki satıra en yüksek sayıyı verir.
/// </summary>
public record UpsertCargoRuleItem(Guid FirmPlatformIntegrationId, int Priority);

public record UpsertCargoRulesCommand(
    Guid FirmId,
    string RuleType,               // default | neighborhood
    Guid? NeighborhoodId,          // neighborhood için zorunlu
    List<UpsertCargoRuleItem> Items) : IRequest<Result<int>>;

public class UpsertCargoRulesCommandHandler(ICoreDbContext db)
    : IRequestHandler<UpsertCargoRulesCommand, Result<int>>
{
    public async Task<Result<int>> Handle(UpsertCargoRulesCommand request, CancellationToken ct)
    {
        if (request.RuleType is not ("default" or "neighborhood"))
            return Result.Failure<int>("Kural tipi 'default' ya da 'neighborhood' olmalıdır.");
        if (request.RuleType == "neighborhood" && request.NeighborhoodId is null)
            return Result.Failure<int>("Mahalle ataması için neighborhoodId zorunludur.");
        if (request.Items.Select(i => i.FirmPlatformIntegrationId).Distinct().Count() != request.Items.Count)
            return Result.Failure<int>("Aynı kargo şirketi listede iki kez yer alamaz.");

        // Gönderilen entegrasyonlar firmaya ait ve kargo tipinde mi?
        var istenen = request.Items.Select(i => i.FirmPlatformIntegrationId).ToList();
        if (istenen.Count > 0)
        {
            var gecerli = await db.FirmPlatformIntegrations
                .Where(fi => fi.FirmId == request.FirmId
                             && istenen.Contains(fi.Id)
                             && fi.IntegrationService.ServiceType == "cargo")
                .Select(fi => fi.Id)
                .ToListAsync(ct);
            var gecersiz = istenen.Except(gecerli).ToList();
            if (gecersiz.Count > 0)
                return Result.Failure<int>("Listede firmaya ait olmayan ya da kargo tipinde olmayan entegrasyon var.");
        }

        var mevcutlar = await db.CargoRules
            .Where(r => r.FirmId == request.FirmId
                        && r.RuleType == request.RuleType
                        && r.NeighborhoodId == request.NeighborhoodId)
            .ToListAsync(ct);

        var simdi = DateTime.UtcNow;
        foreach (var eski in mevcutlar.Where(m => istenen.All(id => id != m.FirmPlatformIntegrationId)))
        {
            eski.IsDeleted = true;
            eski.DeletedAt = simdi;
        }

        foreach (var item in request.Items)
        {
            var mevcut = mevcutlar.FirstOrDefault(m => m.FirmPlatformIntegrationId == item.FirmPlatformIntegrationId);
            if (mevcut is not null)
            {
                mevcut.Priority = item.Priority;
                mevcut.IsActive = true;
                mevcut.UpdatedAt = simdi;
            }
            else
            {
                db.CargoRules.Add(new CargoRule
                {
                    FirmId = request.FirmId,
                    FirmPlatformIntegrationId = item.FirmPlatformIntegrationId,
                    RuleType = request.RuleType,
                    NeighborhoodId = request.NeighborhoodId,
                    Priority = item.Priority,
                    IsActive = true,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(request.Items.Count);
    }
}
