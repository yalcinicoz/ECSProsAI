using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetFirmPlatformPricing;

public record GetFirmPlatformPricingQuery(Guid FirmPlatformId, Guid ProductId) : IRequest<Result<List<FirmPlatformVariantPriceDto>>>;

public record FirmPlatformVariantPriceDto(
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

public class GetFirmPlatformPricingQueryHandler : IRequestHandler<GetFirmPlatformPricingQuery, Result<List<FirmPlatformVariantPriceDto>>>
{
    private readonly ICatalogDbContext _db;

    public GetFirmPlatformPricingQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<List<FirmPlatformVariantPriceDto>>> Handle(GetFirmPlatformPricingQuery request, CancellationToken ct)
    {
        var variantIds = await _db.ProductVariants
            .Where(v => v.ProductId == request.ProductId)
            .Select(v => v.Id)
            .ToListAsync(ct);

        var pricing = await _db.FirmPlatformVariants
            .Include(fpv => fpv.Variant)
            .Where(fpv => fpv.FirmPlatformId == request.FirmPlatformId && variantIds.Contains(fpv.VariantId))
            .Select(fpv => new FirmPlatformVariantPriceDto(
                fpv.Id, fpv.FirmPlatformId, fpv.VariantId, fpv.Variant.Sku,
                fpv.PriceType, fpv.PriceMultiplier, fpv.Price, fpv.CompareAtPrice, fpv.IsActive))
            .ToListAsync(ct);

        return Result.Success(pricing);
    }
}
