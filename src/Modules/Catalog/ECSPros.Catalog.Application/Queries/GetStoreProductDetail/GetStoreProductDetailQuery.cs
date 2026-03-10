using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetStoreProductDetail;

public record GetStoreProductDetailQuery(string ProductCode, Guid FirmPlatformId) : IRequest<Result<StoreProductDetailDto>>;

public record StoreProductDetailDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? ShortDescriptionI18n,
    bool IsActive,
    List<StoreVariantDto> Variants);

public record StoreVariantDto(
    Guid Id,
    string Sku,
    decimal BasePrice,
    decimal? PlatformPrice,
    decimal? CompareAtPrice,
    bool IsActive,
    List<StoreVariantImageDto> Images,
    List<StoreVariantAttributeDto> Attributes);

public record StoreVariantImageDto(Guid Id, string ImageUrl, int SortOrder, bool IsMain);

public record StoreVariantAttributeDto(
    string AttributeTypeCode,
    Dictionary<string, string> AttributeTypeNameI18n,
    string AttributeValueCode,
    Dictionary<string, string> AttributeValueNameI18n);

public class GetStoreProductDetailQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetStoreProductDetailQuery, Result<StoreProductDetailDto>>
{
    public async Task<Result<StoreProductDetailDto>> Handle(GetStoreProductDetailQuery request, CancellationToken ct)
    {
        var product = await db.Products
            .AsNoTracking()
            .Include(p => p.Variants)
                .ThenInclude(v => v.Images)
            .Include(p => p.Variants)
                .ThenInclude(v => v.FirmPlatformVariants)
            .Include(p => p.Variants)
                .ThenInclude(v => v.VariantAttributes)
                    .ThenInclude(va => va.AttributeType)
            .Include(p => p.Variants)
                .ThenInclude(v => v.VariantAttributes)
                    .ThenInclude(va => va.AttributeValue)
            .FirstOrDefaultAsync(p => p.Code == request.ProductCode && p.IsActive, ct);

        if (product is null)
            return Result.Failure<StoreProductDetailDto>("Ürün bulunamadı.");

        var variants = product.Variants
            .Where(v => v.IsActive)
            .Select(v =>
            {
                var fpv = v.FirmPlatformVariants.FirstOrDefault(x => x.FirmPlatformId == request.FirmPlatformId && x.IsActive);
                var attrs = v.VariantAttributes.Select(a => new StoreVariantAttributeDto(
                    a.AttributeType.Code, a.AttributeType.NameI18n,
                    a.AttributeValue.Code, a.AttributeValue.NameI18n)).ToList();

                return new StoreVariantDto(
                    v.Id, v.Sku, v.BasePrice,
                    fpv?.Price, fpv?.CompareAtPrice,
                    v.IsActive,
                    v.Images.OrderBy(i => i.SortOrder).Select(i => new StoreVariantImageDto(i.Id, i.ImageUrl, i.SortOrder, i.IsMain)).ToList(),
                    attrs);
            }).ToList();

        return Result.Success(new StoreProductDetailDto(
            product.Id, product.Code, product.NameI18n, product.ShortDescriptionI18n,
            product.IsActive, variants));
    }
}
