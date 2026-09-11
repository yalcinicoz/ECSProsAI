using ECSPros.Order.Application.Services;
using ECSPros.Promotion.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Handlers;

/// <summary>
/// Kampanya silme (2026-09-11 kullanıcı kuralı): YALNIZ hiçbir siparişte kullanılmamış kampanya silinebilir;
/// kullanılmışsa hata (pasife alınması önerilir). Soft delete — ürün eşlemeleri (CampaignProduct) yerinde kalır,
/// Campaigns global filtresi kampanyayı vitrin/çözümleyici sorgularından düşürür.
/// Kullanım izi iki yerde: sipariş hediyeleri (OrderGift.CampaignId) ve checkout'un yazdığı
/// CustomerNotes.campaigns metni ("Ad [KOD] tür -tutar; …") — sipariş tarafında ayrı kampanya kolonu yok.
/// Handler Api katmanında: Promotion modülü Order modülünü referans almaz (GetStocksAdminHandler kalıbı).
/// "Yayında olan kampanya" uyarısı panelde verilir (durum + tarih aralığı); sunucu engellemez.
/// </summary>
public record DeleteCampaignCommand(Guid Id, Guid DeletedBy) : IRequest<Result<bool>>;

public class DeleteCampaignHandler(IPromotionDbContext promoDb, IOrderDbContext orderDb)
    : IRequestHandler<DeleteCampaignCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteCampaignCommand r, CancellationToken ct)
    {
        var campaign = await promoDb.Campaigns.FirstOrDefaultAsync(c => c.Id == r.Id, ct);
        if (campaign is null) return Result.Failure<bool>("Kampanya bulunamadı.");

        var hediyeKullanimi = await orderDb.Orders.AsNoTracking()
            .AnyAsync(o => o.Gifts.Any(g => g.CampaignId == r.Id), ct);
        if (hediyeKullanimi)
            return Result.Failure<bool>("Bu kampanya siparişlerde (hediye) kullanılmış; silinemez. Kampanyayı pasife alabilirsiniz.");

        var desen = $"%[{campaign.Code}]%";
        var notKullanimi = await orderDb.Orders
            .FromSqlInterpolated($@"SELECT * FROM ""order"".ord_orders WHERE ""CustomerNotes""->>'campaigns' LIKE {desen}")
            .AsNoTracking()
            .AnyAsync(ct);
        if (notKullanimi)
            return Result.Failure<bool>("Bu kampanya siparişlerde kullanılmış; silinemez. Kampanyayı pasife alabilirsiniz.");

        campaign.IsDeleted = true;
        campaign.DeletedAt = DateTime.UtcNow;
        campaign.DeletedBy = r.DeletedBy;
        campaign.IsActive = false;
        await promoDb.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
