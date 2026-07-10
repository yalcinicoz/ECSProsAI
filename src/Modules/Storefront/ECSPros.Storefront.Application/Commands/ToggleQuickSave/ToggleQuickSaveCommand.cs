using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.ToggleQuickSave;

/// <summary>
/// E6: kart/detay bookmark'ı — tasarımda koleksiyon seçici olmadığından buton, üyenin
/// otomatik "Kaydedilenler" koleksiyonuyla birebir çalışır (yoksa gizli+paylaşımsız
/// oluşturulur; elle kurulan koleksiyonlar oluşturma modalından yönetilir). Ekleme
/// idempotent, çıkarma yalnız Kaydedilenler'den (özenle kurulmuş koleksiyonlara dokunmaz).
/// </summary>
public record ToggleQuickSaveCommand(
    Guid FirmPlatformId,
    Guid MemberId,
    string ProductCode,
    bool Ekle) : IRequest<Result<bool>>;

public class ToggleQuickSaveCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<ToggleQuickSaveCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ToggleQuickSaveCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ProductCode))
            return Result.Failure<bool>("Ürün kodu gereklidir.");
        var kod = request.ProductCode.Trim();

        var koleksiyon = await db.Collections
            .FirstOrDefaultAsync(c => c.FirmPlatformId == request.FirmPlatformId
                                      && c.MemberId == request.MemberId && c.IsQuickSave, ct);

        if (koleksiyon is null)
        {
            if (!request.Ekle) return Result.Success(true); // çıkarılacak bir şey yok
            koleksiyon = new Collection
            {
                FirmPlatformId = request.FirmPlatformId,
                MemberId = request.MemberId,
                Name = "Kaydedilenler",
                IsPublic = false,
                IsShareable = false,
                ShareCode = Guid.NewGuid().ToString("N")[..10],
                Status = "pending",
                IsQuickSave = true
            };
            db.Collections.Add(koleksiyon);
        }

        // soft-delete edilmişler global filtreye takılır — unique index için filtresiz bakılır
        var satir = await db.CollectionItems.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.CollectionId == koleksiyon.Id && i.ProductCode == kod, ct);
        if (request.Ekle)
        {
            if (satir is null)
                db.CollectionItems.Add(new CollectionItem { Collection = koleksiyon, ProductCode = kod });
            else if (satir.IsDeleted) { satir.IsDeleted = false; satir.DeletedAt = null; }
            koleksiyon.UpdatedAt = DateTime.UtcNow;
        }
        else if (satir is { IsDeleted: false })
        {
            satir.IsDeleted = true;
            satir.DeletedAt = DateTime.UtcNow;
            koleksiyon.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
