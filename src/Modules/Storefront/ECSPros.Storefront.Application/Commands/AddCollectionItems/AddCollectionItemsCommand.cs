using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.AddCollectionItems;

/// <summary>2026-07-16: var olan koleksiyona ürün ekleme — ortak "Koleksiyona Ekle" modal
/// akışının (Varolan Koleksiyon) API karşılığı. Yalnız üyenin kendi koleksiyonuna eklenir;
/// mevcut kayıt (unique CollectionId+ProductCode) atlanır — idempotent.</summary>
public record AddCollectionItemsCommand(
    Guid FirmPlatformId,
    Guid MemberId,
    Guid CollectionId,
    List<string> ProductCodes) : IRequest<Result<int>>;

public class AddCollectionItemsCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<AddCollectionItemsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(AddCollectionItemsCommand request, CancellationToken ct)
    {
        var kodlar = (request.ProductCodes ?? [])
            .Select(k => k?.Trim())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k!)
            .Distinct()
            .ToList();
        if (kodlar.Count == 0)
            return Result.Failure<int>("Eklenecek ürün bulunamadı.");

        var koleksiyon = await db.Collections.FirstOrDefaultAsync(
            k => k.Id == request.CollectionId
                 && k.FirmPlatformId == request.FirmPlatformId
                 && k.MemberId == request.MemberId, ct);
        if (koleksiyon is null)
            return Result.Failure<int>("Koleksiyon bulunamadı.");

        var mevcutlar = await db.CollectionItems
            .Where(i => i.CollectionId == koleksiyon.Id && kodlar.Contains(i.ProductCode))
            .Select(i => i.ProductCode)
            .ToListAsync(ct);

        var eklenen = 0;
        foreach (var kod in kodlar.Except(mevcutlar))
        {
            db.CollectionItems.Add(new CollectionItem { CollectionId = koleksiyon.Id, ProductCode = kod });
            eklenen++;
        }

        if (eklenen > 0)
            await db.SaveChangesAsync(ct);
        return Result.Success(eklenen);
    }
}
