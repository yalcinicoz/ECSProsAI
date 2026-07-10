using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.RecordProductView;

/// <summary>E12: ürün gezme kaydı — detay sayfası render'ında üye için çağrılır.
/// Ürün başına tek kayıt (tekrar gezmede zaman güncellenir); üye başına son 50
/// kayıt tutulur, fazlası budanır (soft). Hata sayfa render'ını etkilememeli —
/// çağıran taraf sonucu yok sayabilir.</summary>
public record RecordProductViewCommand(
    Guid FirmPlatformId,
    Guid MemberId,
    string ProductCode) : IRequest<Result>;

public class RecordProductViewCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<RecordProductViewCommand, Result>
{
    private const int KayitSiniri = 50;

    public async Task<Result> Handle(RecordProductViewCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ProductCode))
            return Result.Failure("Ürün kodu gereklidir.");
        var kod = request.ProductCode.Trim();
        var simdi = DateTime.UtcNow;

        var mevcut = await db.ViewedProducts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.FirmPlatformId == request.FirmPlatformId
                                      && v.MemberId == request.MemberId
                                      && v.ProductCode == kod, ct);
        if (mevcut is not null)
        {
            mevcut.IsDeleted = false;
            mevcut.DeletedAt = null;
            mevcut.ViewedAt = simdi;
            mevcut.UpdatedAt = simdi;
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }

        db.ViewedProducts.Add(new ViewedProduct
        {
            FirmPlatformId = request.FirmPlatformId,
            MemberId = request.MemberId,
            ProductCode = kod,
            ViewedAt = simdi
        });

        // Budama: üyenin platformdaki kayıtları sınırı aşarsa en eskiler düşer
        var fazlalik = await db.ViewedProducts
            .Where(v => v.FirmPlatformId == request.FirmPlatformId && v.MemberId == request.MemberId)
            .OrderByDescending(v => v.ViewedAt)
            .Skip(KayitSiniri - 1)
            .ToListAsync(ct);
        foreach (var eski in fazlalik)
        {
            eski.IsDeleted = true;
            eski.DeletedAt = simdi;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
