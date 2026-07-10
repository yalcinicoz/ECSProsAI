using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.ModerateCollection;

/// <summary>E6: koleksiyon moderasyonu (admin) — approved koleksiyonlar Faz G
/// "Koleksiyonlar bloğu"nda (yalnız IsPublic olanlar) kullanılabilir.</summary>
public record ModerateCollectionCommand(Guid CollectionId, bool Approve) : IRequest<Result<bool>>;

public class ModerateCollectionCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<ModerateCollectionCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ModerateCollectionCommand request, CancellationToken ct)
    {
        var koleksiyon = await db.Collections.FirstOrDefaultAsync(c => c.Id == request.CollectionId, ct);
        if (koleksiyon is null) return Result.Failure<bool>("Koleksiyon bulunamadı.");

        koleksiyon.Status = request.Approve ? "approved" : "rejected";
        koleksiyon.ModeratedAt = DateTime.UtcNow;
        koleksiyon.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
