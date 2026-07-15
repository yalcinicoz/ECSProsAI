using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.BulkSetChannelProductStop;

/// <summary>
/// Satış görünürlüğü M3 (kanalda durdurma): verilen ürünlerin satışını durdurur (From/Until
/// penceresi) veya durdurmayı temizler. From null gönderilirse durdurma kaldırılır (satış
/// başlatılır). Pencere bitince sorgu-zamanı otomatik açılır (job yok). Satır yoksa oluşturulur.
/// </summary>
public record BulkSetChannelProductStopCommand(
    Guid FirmPlatformId,
    List<Guid> ProductIds,
    DateTime? From,
    DateTime? Until) : IRequest<Result<int>>;

public class BulkSetChannelProductStopCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<BulkSetChannelProductStopCommand, Result<int>>
{
    public async Task<Result<int>> Handle(BulkSetChannelProductStopCommand request, CancellationToken ct)
    {
        if (request.ProductIds is not { Count: > 0 })
            return Result.Failure<int>("Ürün seçilmedi.");
        if (request.From.HasValue && request.Until.HasValue && request.Until.Value < request.From.Value)
            return Result.Failure<int>("Bitiş tarihi başlangıçtan önce olamaz.");

        var now = DateTime.UtcNow;
        var from = request.From?.ToUniversalTime();
        var until = request.Until?.ToUniversalTime();
        var durdur = from.HasValue;   // From yoksa → durdurmayı temizle (satış başlat)

        var mevcut = await db.ChannelProducts
            .Where(cp => cp.FirmPlatformId == request.FirmPlatformId && request.ProductIds.Contains(cp.ProductId))
            .ToListAsync(ct);
        var mevcutByProduct = mevcut.ToDictionary(cp => cp.ProductId);

        var degisen = 0;
        foreach (var pid in request.ProductIds.Distinct())
        {
            if (mevcutByProduct.TryGetValue(pid, out var kayit))
            {
                kayit.SaleStoppedFrom = from;
                kayit.SaleStoppedUntil = until;
                kayit.UpdatedAt = now;
                degisen++;
            }
            else
            {
                // Satır yok. Durdurma temizleme isteği ise yapacak bir şey yok (zaten durdurulmamış).
                if (!durdur) continue;
                db.ChannelProducts.Add(new ChannelProduct
                {
                    FirmPlatformId = request.FirmPlatformId,
                    ProductId = pid,
                    SaleStoppedFrom = from,
                    SaleStoppedUntil = until
                });
                degisen++;
            }
        }

        if (degisen > 0) await db.SaveChangesAsync(ct);
        return Result.Success(degisen);
    }
}
