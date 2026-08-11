using ECSPros.Promotion.Application.Services;
using ECSPros.Promotion.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Commands.SupplierCampaignParticipation;

/// <summary>P3a (2026-08-11): satıcının kampanya keşfi — opt-in'e açık, penceresi geçmemiş
/// kampanyalar + satıcının katılım durumu. Komisyon oranı ve indirim paylaşımı da döner
/// (satıcı neye katıldığını bilerek katılır — K1 alt-kararı).</summary>
public record GetSupplierCampaignsQuery(Guid SupplierAccountId) : IRequest<Result<List<SupplierCampaignDto>>>;

public record SupplierCampaignDto(
    Guid CampaignId,
    string Code,
    Dictionary<string, string> NameI18n,
    DateTime StartsAt,
    DateTime? EndsAt,
    decimal? SupplierCommissionRate,
    decimal SupplierDiscountSharePercent,
    bool Joined,
    List<Guid> JoinedProductIds);

/// <summary>Kampanyaya katıl (opt-in) — ProductIds boş: kapsama giren TÜM ürünlerle.
/// Tekrar çağrı katılımı ürün listesiyle GÜNCELLER (idempotent).</summary>
public record JoinCampaignCommand(Guid SupplierAccountId, Guid CampaignId, List<Guid>? ProductIds)
    : IRequest<Result<bool>>;

/// <summary>Katılımı geri çek — sonrasındaki teslimlerde kampanya katmanı/paylaşımı uygulanmaz.</summary>
public record LeaveCampaignCommand(Guid SupplierAccountId, Guid CampaignId) : IRequest<Result<bool>>;

public class SupplierCampaignParticipationHandlers(IPromotionDbContext db) :
    IRequestHandler<GetSupplierCampaignsQuery, Result<List<SupplierCampaignDto>>>,
    IRequestHandler<JoinCampaignCommand, Result<bool>>,
    IRequestHandler<LeaveCampaignCommand, Result<bool>>
{
    public async Task<Result<List<SupplierCampaignDto>>> Handle(GetSupplierCampaignsQuery request, CancellationToken ct)
    {
        var simdi = DateTime.UtcNow;
        var kampanyalar = await db.Campaigns.AsNoTracking()
            .Where(c => c.IsActive && c.RequiresSupplierOptIn && (c.EndsAt == null || c.EndsAt >= simdi))
            .OrderBy(c => c.StartsAt)
            .ToListAsync(ct);
        var idler = kampanyalar.Select(c => c.Id).ToList();
        var katilimlar = await db.CampaignSupplierParticipations.AsNoTracking()
            .Where(p => idler.Contains(p.CampaignId) && p.SupplierAccountId == request.SupplierAccountId && p.IsActive)
            .ToListAsync(ct);

        return Result.Success(kampanyalar.Select(c =>
        {
            var katilim = katilimlar.FirstOrDefault(p => p.CampaignId == c.Id);
            return new SupplierCampaignDto(
                c.Id, c.Code, c.NameI18n, c.StartsAt, c.EndsAt,
                c.SupplierCommissionRate, c.SupplierDiscountSharePercent,
                katilim is not null, katilim?.ProductIds ?? []);
        }).ToList());
    }

    public async Task<Result<bool>> Handle(JoinCampaignCommand request, CancellationToken ct)
    {
        var simdi = DateTime.UtcNow;
        var kampanya = await db.Campaigns.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CampaignId && c.IsActive, ct);
        if (kampanya is null) return Result.Failure<bool>("Kampanya bulunamadı.");
        if (!kampanya.RequiresSupplierOptIn)
            return Result.Failure<bool>("Bu kampanya satıcı katılımına açık değil.");
        if (kampanya.EndsAt is { } bitis && bitis < simdi)
            return Result.Failure<bool>("Kampanya sona erdi.");

        var katilim = await db.CampaignSupplierParticipations
            .FirstOrDefaultAsync(p => p.CampaignId == request.CampaignId
                && p.SupplierAccountId == request.SupplierAccountId, ct);
        if (katilim is null)
        {
            db.CampaignSupplierParticipations.Add(new CampaignSupplierParticipation
            {
                CampaignId = request.CampaignId,
                SupplierAccountId = request.SupplierAccountId,
                ProductIds = request.ProductIds ?? [],
                JoinedAt = simdi,
                IsActive = true
            });
        }
        else
        {
            katilim.ProductIds = request.ProductIds ?? [];
            katilim.IsActive = true;
            katilim.JoinedAt = simdi;
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }

    public async Task<Result<bool>> Handle(LeaveCampaignCommand request, CancellationToken ct)
    {
        var katilim = await db.CampaignSupplierParticipations
            .FirstOrDefaultAsync(p => p.CampaignId == request.CampaignId
                && p.SupplierAccountId == request.SupplierAccountId && p.IsActive, ct);
        if (katilim is null) return Result.Failure<bool>("Bu kampanyada aktif katılımınız yok.");
        katilim.IsActive = false;
        katilim.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
