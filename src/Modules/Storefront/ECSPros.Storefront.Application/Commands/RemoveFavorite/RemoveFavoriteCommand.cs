using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.RemoveFavorite;

/// <summary>E5: favoriden çıkar (soft delete) — kayıt yoksa da başarı döner (toggle UX).</summary>
public record RemoveFavoriteCommand(
    Guid FirmPlatformId,
    Guid MemberId,
    string ProductCode) : IRequest<Result<bool>>;

public class RemoveFavoriteCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<RemoveFavoriteCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RemoveFavoriteCommand request, CancellationToken ct)
    {
        var kod = request.ProductCode.Trim();
        var favori = await db.Favorites.FirstOrDefaultAsync(
            f => f.FirmPlatformId == request.FirmPlatformId
                 && f.MemberId == request.MemberId
                 && f.ProductCode == kod, ct);
        if (favori is not null)
        {
            favori.IsDeleted = true;
            favori.DeletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return Result.Success(true);
    }
}
