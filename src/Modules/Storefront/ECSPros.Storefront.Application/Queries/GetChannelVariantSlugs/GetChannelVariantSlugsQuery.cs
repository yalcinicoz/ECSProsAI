using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetChannelVariantSlugs;

/// <summary>
/// URL aktarımı 2b (detay): verilen varyantların bu platformdaki gerçek URL slug'ları
/// (variantId → slug). Ürün detayında renk butonlarının o rengin kendi slug'ına link
/// vermesi için. Slug'ı olmayan varyant sözlükte yer almaz.
/// </summary>
public record GetChannelVariantSlugsQuery(Guid FirmPlatformId, List<Guid> VariantIds)
    : IRequest<Result<Dictionary<Guid, string>>>;

public class GetChannelVariantSlugsQueryHandler(IStorefrontDbContext sfDb)
    : IRequestHandler<GetChannelVariantSlugsQuery, Result<Dictionary<Guid, string>>>
{
    public async Task<Result<Dictionary<Guid, string>>> Handle(
        GetChannelVariantSlugsQuery request, CancellationToken ct)
    {
        if (request.VariantIds is not { Count: > 0 })
            return Result.Success(new Dictionary<Guid, string>());

        var map = await sfDb.ChannelVariants.AsNoTracking()
            .Where(cv => cv.FirmPlatformId == request.FirmPlatformId && cv.Slug != null
                      && request.VariantIds.Contains(cv.VariantId))
            .Select(cv => new { cv.VariantId, cv.Slug })
            .ToDictionaryAsync(x => x.VariantId, x => x.Slug!, ct);
        return Result.Success(map);
    }
}
