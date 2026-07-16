using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetMemberFavorites;

/// <summary>E5: üyenin favori ürün kodları (yeni → eski) — kalp işaretleme hafif liste;
/// Favorilerim sayfası kodlarla Catalog'dan kart verisi çeker (canlı katalogla birleşim —
/// silinen ürün listede görünmez).</summary>
public record GetMemberFavoritesQuery(Guid FirmPlatformId, Guid MemberId)
    : IRequest<Result<List<FavoriteKeyDto>>>;

/// <summary>2026-07-16: favori anahtarı ürün+renk (ColorValueId null = renksiz favori).</summary>
public record FavoriteKeyDto(string ProductCode, Guid? ColorValueId);

public class GetMemberFavoritesQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetMemberFavoritesQuery, Result<List<FavoriteKeyDto>>>
{
    public async Task<Result<List<FavoriteKeyDto>>> Handle(GetMemberFavoritesQuery request, CancellationToken ct)
    {
        var kayitlar = await db.Favorites
            .Where(f => f.FirmPlatformId == request.FirmPlatformId && f.MemberId == request.MemberId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new FavoriteKeyDto(f.ProductCode, f.ColorValueId))
            .ToListAsync(ct);
        return Result.Success(kayitlar);
    }
}
