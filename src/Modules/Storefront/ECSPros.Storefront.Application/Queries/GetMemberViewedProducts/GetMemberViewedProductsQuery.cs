using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetMemberViewedProducts;

/// <summary>E12: üyenin son gezdiği ürünler (yeni → eski, en çok 50) —
/// Önceden Gezdiklerim sayfası + Faz G "son gezilenler" bloğu.</summary>
public record GetMemberViewedProductsQuery(Guid FirmPlatformId, Guid MemberId, int Limit = 50)
    : IRequest<Result<List<ViewedProductDto>>>;

public record ViewedProductDto(string ProductCode, DateTime ViewedAt);

public class GetMemberViewedProductsQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetMemberViewedProductsQuery, Result<List<ViewedProductDto>>>
{
    public async Task<Result<List<ViewedProductDto>>> Handle(GetMemberViewedProductsQuery request, CancellationToken ct)
    {
        var liste = await db.ViewedProducts
            .Where(v => v.FirmPlatformId == request.FirmPlatformId && v.MemberId == request.MemberId)
            .OrderByDescending(v => v.ViewedAt)
            .Take(Math.Clamp(request.Limit, 1, 50))
            .Select(v => new ViewedProductDto(v.ProductCode, v.ViewedAt))
            .ToListAsync(ct);

        return Result.Success(liste);
    }
}
