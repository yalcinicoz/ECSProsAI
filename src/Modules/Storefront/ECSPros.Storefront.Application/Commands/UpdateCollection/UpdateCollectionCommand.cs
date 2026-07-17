using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.UpdateCollection;

/// <summary>2026-07-17: koleksiyon düzenleme — ortak modalın "Koleksiyon Güncelle" akışı.
/// Ad/açıklama/görünürlük güncellenir, ürün listesi verilen kodlarla eşitlenir (eksik
/// eklenir, fazlası soft-delete; unique CollectionId+ProductCode indexi soft-deleted
/// satırı da kapsadığından geri eklenen kod undelete edilir). Düzenleme sonrası koleksiyon
/// yeniden moderasyona düşer (pending) — oluşturma ile aynı spec şartı.</summary>
public record UpdateCollectionCommand(
    Guid FirmPlatformId,
    Guid MemberId,
    Guid CollectionId,
    string Name,
    string? Description,
    bool IsPublic,
    bool IsShareable,
    List<string>? ProductCodes = null) : IRequest<Result<Guid>>;

public class UpdateCollectionCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<UpdateCollectionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(UpdateCollectionCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Failure<Guid>("Koleksiyon adı gereklidir.");

        var koleksiyon = await db.Collections.FirstOrDefaultAsync(
            k => k.Id == request.CollectionId
                 && k.FirmPlatformId == request.FirmPlatformId
                 && k.MemberId == request.MemberId, ct);
        if (koleksiyon is null)
            return Result.Failure<Guid>("Koleksiyon bulunamadı.");
        if (koleksiyon.IsQuickSave)
            return Result.Failure<Guid>("Kaydedilenler koleksiyonu düzenlenemez.");

        koleksiyon.Name = request.Name.Trim();
        koleksiyon.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        koleksiyon.IsPublic = request.IsPublic;
        koleksiyon.IsShareable = request.IsShareable;
        koleksiyon.Status = "pending";
        koleksiyon.ModeratedAt = null;

        var istenenler = (request.ProductCodes ?? [])
            .Select(k => k?.Trim())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k!)
            .Distinct()
            .ToHashSet();

        var mevcutlar = await db.CollectionItems
            .IgnoreQueryFilters()
            .Where(i => i.CollectionId == koleksiyon.Id)
            .ToListAsync(ct);

        foreach (var mevcut in mevcutlar)
        {
            if (istenenler.Contains(mevcut.ProductCode))
            {
                mevcut.IsDeleted = false;
                mevcut.DeletedAt = null;
            }
            else if (!mevcut.IsDeleted)
            {
                mevcut.IsDeleted = true;
                mevcut.DeletedAt = DateTime.UtcNow;
            }
        }

        var mevcutKodlar = mevcutlar.Select(i => i.ProductCode).ToHashSet();
        foreach (var kod in istenenler.Where(k => !mevcutKodlar.Contains(k)))
            db.CollectionItems.Add(new CollectionItem { CollectionId = koleksiyon.Id, ProductCode = kod });

        await db.SaveChangesAsync(ct);
        return Result.Success(koleksiyon.Id);
    }
}
