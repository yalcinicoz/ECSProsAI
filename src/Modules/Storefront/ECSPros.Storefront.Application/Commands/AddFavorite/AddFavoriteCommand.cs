using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.AddFavorite;

/// <summary>E5: favoriye ekle — idempotent (kayıt varsa aynı Id döner; soft-delete edilmiş
/// eski kayıt varsa geri açılır — unique index IsDeleted'ı ayırt etmez).</summary>
public record AddFavoriteCommand(
    Guid FirmPlatformId,
    Guid MemberId,
    string ProductCode,
    Guid? VariantId = null) : IRequest<Result<Guid>>;

public class AddFavoriteCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<AddFavoriteCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AddFavoriteCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ProductCode))
            return Result.Failure<Guid>("Ürün kodu gereklidir.");
        var kod = request.ProductCode.Trim();

        var mevcut = await db.Favorites
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.FirmPlatformId == request.FirmPlatformId
                                      && f.MemberId == request.MemberId
                                      && f.ProductCode == kod, ct);
        if (mevcut is not null)
        {
            if (mevcut.IsDeleted)
            {
                mevcut.IsDeleted = false;
                mevcut.DeletedAt = null;
                mevcut.VariantId = request.VariantId;
                mevcut.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            return Result.Success(mevcut.Id);
        }

        var favori = new Favorite
        {
            FirmPlatformId = request.FirmPlatformId,
            MemberId = request.MemberId,
            ProductCode = kod,
            VariantId = request.VariantId
        };
        db.Favorites.Add(favori);
        await db.SaveChangesAsync(ct);
        return Result.Success(favori.Id);
    }
}
