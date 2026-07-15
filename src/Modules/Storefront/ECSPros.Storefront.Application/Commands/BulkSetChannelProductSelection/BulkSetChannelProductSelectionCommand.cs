using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.BulkSetChannelProductSelection;

/// <summary>
/// Satış görünürlüğü M2 (kanal seçimi): verilen ürünleri kanala alır (Selected=true) veya
/// kanaldan çıkarır (Selected=false). Opt-out: satır yoksa ve Selected=true ise kayıt gereksiz
/// (satır yok = zaten kanalda); Selected=false için satır oluşturulur/güncellenir.
/// </summary>
public record BulkSetChannelProductSelectionCommand(
    Guid FirmPlatformId,
    List<Guid> ProductIds,
    bool Selected) : IRequest<Result<int>>;

public class BulkSetChannelProductSelectionCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<BulkSetChannelProductSelectionCommand, Result<int>>
{
    public async Task<Result<int>> Handle(BulkSetChannelProductSelectionCommand request, CancellationToken ct)
    {
        if (request.ProductIds is not { Count: > 0 })
            return Result.Failure<int>("Ürün seçilmedi.");

        var now = DateTime.UtcNow;
        var mevcut = await db.ChannelProducts
            .Where(cp => cp.FirmPlatformId == request.FirmPlatformId && request.ProductIds.Contains(cp.ProductId))
            .ToListAsync(ct);
        var mevcutByProduct = mevcut.ToDictionary(cp => cp.ProductId);

        var degisen = 0;
        foreach (var pid in request.ProductIds.Distinct())
        {
            if (mevcutByProduct.TryGetValue(pid, out var kayit))
            {
                if (kayit.IsActive == request.Selected) continue;
                kayit.IsActive = request.Selected;
                kayit.UpdatedAt = now;
                degisen++;
            }
            else
            {
                // Satır yok. Selected=true → opt-out gereği zaten kanalda; kayıt oluşturmaya
                // gerek yok. Selected=false → çıkarma kaydı oluştur.
                if (request.Selected) continue;
                db.ChannelProducts.Add(new ChannelProduct
                {
                    FirmPlatformId = request.FirmPlatformId,
                    ProductId = pid,
                    IsActive = false
                });
                degisen++;
            }
        }

        if (degisen > 0) await db.SaveChangesAsync(ct);
        return Result.Success(degisen);
    }
}
