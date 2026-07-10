using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetMemberFavorites;

/// <summary>E5: üyenin favori ürün kodları (yeni → eski) — kalp işaretleme hafif liste;
/// Favorilerim sayfası kodlarla Catalog'dan kart verisi çeker (canlı katalogla birleşim —
/// silinen ürün listede görünmez).</summary>
public record GetMemberFavoritesQuery(Guid FirmPlatformId, Guid MemberId)
    : IRequest<Result<List<string>>>;

public class GetMemberFavoritesQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetMemberFavoritesQuery, Result<List<string>>>
{
    public async Task<Result<List<string>>> Handle(GetMemberFavoritesQuery request, CancellationToken ct)
    {
        var kodlar = await db.Favorites
            .Where(f => f.FirmPlatformId == request.FirmPlatformId && f.MemberId == request.MemberId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.ProductCode)
            .ToListAsync(ct);
        return Result.Success(kodlar);
    }
}
