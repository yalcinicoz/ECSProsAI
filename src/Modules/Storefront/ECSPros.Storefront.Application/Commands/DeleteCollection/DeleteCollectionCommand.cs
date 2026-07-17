using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.DeleteCollection;

/// <summary>2026-07-17: koleksiyon silme (soft delete) — yalnız üyenin kendi koleksiyonu;
/// "Kaydedilenler" hızlı koleksiyonu silinemez (bookmark hedefi).</summary>
public record DeleteCollectionCommand(
    Guid FirmPlatformId,
    Guid MemberId,
    Guid CollectionId) : IRequest<Result<bool>>;

public class DeleteCollectionCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<DeleteCollectionCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteCollectionCommand request, CancellationToken ct)
    {
        var koleksiyon = await db.Collections.FirstOrDefaultAsync(
            k => k.Id == request.CollectionId
                 && k.FirmPlatformId == request.FirmPlatformId
                 && k.MemberId == request.MemberId, ct);
        if (koleksiyon is null)
            return Result.Failure<bool>("Koleksiyon bulunamadı.");
        if (koleksiyon.IsQuickSave)
            return Result.Failure<bool>("Kaydedilenler koleksiyonu silinemez.");

        koleksiyon.IsDeleted = true;
        koleksiyon.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
