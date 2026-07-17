using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetCartRemovedItems;

/// <summary>2026-07-17: üyenin sepetten çıkardığı ürünler — "Önceden Eklediklerim"
/// bölümü (son çıkarılan önce, en fazla 12).</summary>
public record GetCartRemovedItemsQuery(Guid FirmPlatformId, Guid MemberId)
    : IRequest<Result<List<CartRemovedItemDto>>>;

public record CartRemovedItemDto(
    Guid VariantId,
    string ProductCode,
    string Name,
    string? ImageUrl,
    decimal Price,
    string CurrencyCode);

public class GetCartRemovedItemsQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetCartRemovedItemsQuery, Result<List<CartRemovedItemDto>>>
{
    public async Task<Result<List<CartRemovedItemDto>>> Handle(GetCartRemovedItemsQuery request, CancellationToken ct)
    {
        var kayitlar = await db.CartRemovedItems
            .AsNoTracking()
            .Where(x => x.FirmPlatformId == request.FirmPlatformId && x.MemberId == request.MemberId)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(12)
            .Select(x => new CartRemovedItemDto(
                x.VariantId, x.ProductCode, x.Name, x.ImageUrl, x.Price, x.CurrencyCode))
            .ToListAsync(ct);

        return Result.Success(kayitlar);
    }
}
