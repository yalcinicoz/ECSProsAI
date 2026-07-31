using ECSPros.Promotion.Application.Services;
using ECSPros.Promotion.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Commands.CopyCampaign;

// F1: kampanyayı kopyala — aynı tip/ayar/ürün kapsamı, yeni kod ile (opsiyonel başka platforma).
// Tek-platform modelinde aynı kampanyayı başka platforma çoğaltmanın yolu budur.
public record CopyCampaignCommand(
    Guid SourceId,
    string NewCode,
    Guid? TargetFirmPlatformId = null,
    Guid? CreatedBy = null) : IRequest<Result<Guid>>;

public class CopyCampaignCommandHandler(IPromotionDbContext db)
    : IRequestHandler<CopyCampaignCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CopyCampaignCommand request, CancellationToken ct)
    {
        var src = await db.Campaigns.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.SourceId, ct);
        if (src is null) return Result.Failure<Guid>("Kaynak kampanya bulunamadı.");

        if (await db.Campaigns.AnyAsync(c => c.Code == request.NewCode, ct))
            return Result.Failure<Guid>($"'{request.NewCode}' kampanya kodu zaten mevcut.");

        var copy = new Campaign
        {
            FirmPlatformId = request.TargetFirmPlatformId ?? src.FirmPlatformId,
            CampaignTypeId = src.CampaignTypeId,
            Code = request.NewCode,
            NameI18n = new Dictionary<string, string>(src.NameI18n),
            DescriptionI18n = src.DescriptionI18n is null ? null : new Dictionary<string, string>(src.DescriptionI18n),
            BadgeLabel = src.BadgeLabel,
            StartsAt = src.StartsAt,
            EndsAt = src.EndsAt,
            Priority = src.Priority,
            IsActive = false, // kopya pasif başlar — operatör kontrol edip açar
            Settings = new Dictionary<string, object>(src.Settings),
            FillType = src.FillType,
            FilterDef = src.FilterDef is null ? null : new Dictionary<string, object>(src.FilterDef),
            CreatedBy = request.CreatedBy,
        };
        db.Campaigns.Add(copy);

        // Manuel ürünleri kopyala (filtre ürünleri hedef platformda yeniden materyalize edilebilir;
        // burada manuel seçim taşınır — filtre sonuçları ilk kaydetmede/sync'te güncellenir).
        var manual = await db.CampaignProducts.AsNoTracking()
            .Where(p => p.CampaignId == src.Id && p.AddedType == "manual")
            .Select(p => new { p.ProductId, p.VariantId })
            .ToListAsync(ct);
        foreach (var m in manual)
            db.CampaignProducts.Add(new CampaignProduct
            {
                CampaignId = copy.Id, ProductId = m.ProductId, VariantId = m.VariantId, AddedType = "manual",
            });

        await db.SaveChangesAsync(ct);
        return Result.Success(copy.Id);
    }
}
