using ECSPros.Shared.Kernel.Common;
using MediatR;

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
    List<StoreVariantAttributeDto> Attributes,
    int StockQty = 0);

public record StoreVariantImageDto(Guid Id, string ImageUrl, int SortOrder, bool IsMain);

public record StoreVariantAttributeDto(
    string AttributeTypeCode,
    Dictionary<string, string> AttributeTypeNameI18n,
    Guid AttributeValueId,
    Dictionary<string, string> AttributeValueNameI18n,
    bool IsColor = false,
    string? HexCode = null);
