using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.RecordCartRemovedItem;

/// <summary>2026-07-17: sepetten çıkarılan ürünü üyenin "Önceden Eklediklerim" geçmişine
/// yazar. Üye+platform+varyant başına tek kayıt (unique index soft-deleted satırı da
/// kapsar — mevcut kayıt undelete edilip tazelenir); üye+platform başına son 12 kayıt
/// tutulur, fazlası soft-delete ile budanır. Alanlar snapshot'tır, uzunluklar kolon
/// sınırlarına kırpılır (geçmiş kaydı — hata yerine kırpma).</summary>
public record RecordCartRemovedItemCommand(
    Guid FirmPlatformId,
    Guid MemberId,
    Guid VariantId,
    string ProductCode,
    string Name,
    string? ImageUrl,
    decimal Price,
    string? CurrencyCode) : IRequest<Result<bool>>;

public class RecordCartRemovedItemCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<RecordCartRemovedItemCommand, Result<bool>>
{
    private const int SaklananKayitSayisi = 12;

    public async Task<Result<bool>> Handle(RecordCartRemovedItemCommand request, CancellationToken ct)
    {
        if (request.VariantId == Guid.Empty)
            return Result.Failure<bool>("Varyant bilgisi gereklidir.");

        static string Kirp(string? deger, int uzunluk, string varsayilan = "") =>
            (string.IsNullOrWhiteSpace(deger) ? varsayilan : deger.Trim()) is var d && d.Length > uzunluk ? d[..uzunluk] : d;

        var kayit = await db.CartRemovedItems
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.FirmPlatformId == request.FirmPlatformId
                                      && x.MemberId == request.MemberId
                                      && x.VariantId == request.VariantId, ct);

        if (kayit is null)
        {
            kayit = new CartRemovedItem
            {
                FirmPlatformId = request.FirmPlatformId,
                MemberId = request.MemberId,
                VariantId = request.VariantId
            };
            db.CartRemovedItems.Add(kayit);
        }
        else
        {
            kayit.IsDeleted = false;
            kayit.DeletedAt = null;
            kayit.UpdatedAt = DateTime.UtcNow; // sıralama "son çıkarma" zamanına göre
        }

        kayit.ProductCode = Kirp(request.ProductCode, 50);
        kayit.Name = Kirp(request.Name, 300, kayit.ProductCode.Length > 0 ? kayit.ProductCode : "Ürün");
        kayit.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : Kirp(request.ImageUrl, 500);
        kayit.Price = request.Price;
        kayit.CurrencyCode = Kirp(request.CurrencyCode, 8, "TRY");

        await db.SaveChangesAsync(ct);

        // Budama: üye+platform başına en yeni 12 kayıt kalır
        var fazlalar = await db.CartRemovedItems
            .Where(x => x.FirmPlatformId == request.FirmPlatformId && x.MemberId == request.MemberId)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Skip(SaklananKayitSayisi)
            .ToListAsync(ct);
        if (fazlalar.Count > 0)
        {
            foreach (var fazla in fazlalar)
            {
                fazla.IsDeleted = true;
                fazla.DeletedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
        }

        return Result.Success(true);
    }
}
