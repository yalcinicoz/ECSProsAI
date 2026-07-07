using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetChannelVariantPricing;

public record GetChannelVariantPricingQuery(Guid FirmPlatformId, Guid ProductId) : IRequest<Result<List<ChannelVariantPriceDto>>>;

public record ChannelVariantPriceDto(
    Guid Id,
    Guid FirmPlatformId,
    Guid VariantId,
    string VariantSku,
    string? PriceType,
    decimal? PriceMultiplier,
    decimal? Price,
    decimal? CompareAtPrice,
    bool IsActive
);

public class GetChannelVariantPricingQueryHandler : IRequestHandler<GetChannelVariantPricingQuery, Result<List<ChannelVariantPriceDto>>>
{
    private readonly IStorefrontDbContext _sfDb;
    private readonly ICatalogDbContext _catDb;

    public GetChannelVariantPricingQueryHandler(IStorefrontDbContext sfDb, ICatalogDbContext catDb)
    {
        _sfDb = sfDb;
        _catDb = catDb;
    }

    public async Task<Result<List<ChannelVariantPriceDto>>> Handle(GetChannelVariantPricingQuery request, CancellationToken ct)
    {
        var variantSkuById = await _catDb.ProductVariants
            .Where(v => v.ProductId == request.ProductId)
            .Select(v => new { v.Id, v.Sku })
            .ToDictionaryAsync(v => v.Id, v => v.Sku, ct);

        var pricing = await _sfDb.ChannelVariants
            .Where(cv => cv.FirmPlatformId == request.FirmPlatformId && variantSkuById.Keys.Contains(cv.VariantId))
            .ToListAsync(ct);

        var dtos = pricing
            .Select(cv => new ChannelVariantPriceDto(
                cv.Id, cv.FirmPlatformId, cv.VariantId, variantSkuById.GetValueOrDefault(cv.VariantId, ""),
                cv.PriceType, cv.PriceMultiplier, cv.Price, cv.CompareAtPrice, cv.IsActive))
            .ToList();

        return Result.Success(dtos);
    }
}
