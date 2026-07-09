using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetChannelProductFeatured;

/// <summary>B11: kanal ürününün öne çıkarma durumunu döner (satır yoksa null alanlar).</summary>
public record GetChannelProductFeaturedQuery(Guid FirmPlatformId, Guid ProductId)
    : IRequest<Result<ChannelProductFeaturedDto>>;

public record ChannelProductFeaturedDto(
    DateTime? FeaturedFrom,
    DateTime? FeaturedUntil,
    bool IsFeaturedNow);

public class GetChannelProductFeaturedQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetChannelProductFeaturedQuery, Result<ChannelProductFeaturedDto>>
{
    public async Task<Result<ChannelProductFeaturedDto>> Handle(
        GetChannelProductFeaturedQuery request, CancellationToken ct)
    {
        var kayit = await db.ChannelProducts.AsNoTracking()
            .Where(p => p.FirmPlatformId == request.FirmPlatformId && p.ProductId == request.ProductId)
            .Select(p => new { p.FeaturedFrom, p.FeaturedUntil })
            .FirstOrDefaultAsync(ct);

        var simdi = DateTime.UtcNow;
        var aktif = kayit?.FeaturedFrom != null && kayit.FeaturedFrom <= simdi
                    && (kayit.FeaturedUntil == null || kayit.FeaturedUntil >= simdi);

        return Result.Success(new ChannelProductFeaturedDto(
            kayit?.FeaturedFrom, kayit?.FeaturedUntil, aktif));
    }
}
