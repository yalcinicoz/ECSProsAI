using ECSPros.Promotion.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Queries.GetActiveCampaignProductRefs;

/// <summary>
/// G3: "kampanyalı ürünler" kaynağı — şu an yürürlükte olan kampanyaların
/// CampaignProduct referanslarını (ürün ve/veya varyant id) döner. Platform kapsamı:
/// kampanyanın CampaignPlatform satırı yoksa tüm platformlarda geçerli sayılır;
/// satırları varsa istenen platform için IsIncluded olmalı. Varyant referanslarının
/// ürüne çözümü çağıranın işidir (API katmanı IProductService ile).
/// </summary>
public record GetActiveCampaignProductRefsQuery(Guid FirmPlatformId)
    : IRequest<Result<CampaignProductRefsDto>>;

public record CampaignProductRefsDto(List<Guid> ProductIds, List<Guid> VariantIds);

public class GetActiveCampaignProductRefsQueryHandler(IPromotionDbContext db)
    : IRequestHandler<GetActiveCampaignProductRefsQuery, Result<CampaignProductRefsDto>>
{
    public async Task<Result<CampaignProductRefsDto>> Handle(GetActiveCampaignProductRefsQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var refs = await db.CampaignProducts
            .AsNoTracking()
            .Where(cp => db.Campaigns.Any(c => c.Id == cp.CampaignId
                && c.IsActive
                && c.StartsAt <= now
                && (c.EndsAt == null || c.EndsAt >= now)
                && (!db.CampaignPlatforms.Any(p => p.CampaignId == c.Id)
                    || db.CampaignPlatforms.Any(p => p.CampaignId == c.Id
                        && p.FirmPlatformId == request.FirmPlatformId && p.IsIncluded))))
            .Select(cp => new { cp.ProductId, cp.VariantId })
            .ToListAsync(ct);

        return Result.Success(new CampaignProductRefsDto(
            refs.Where(r => r.ProductId.HasValue).Select(r => r.ProductId!.Value).Distinct().ToList(),
            refs.Where(r => r.VariantId.HasValue).Select(r => r.VariantId!.Value).Distinct().ToList()));
    }
}
