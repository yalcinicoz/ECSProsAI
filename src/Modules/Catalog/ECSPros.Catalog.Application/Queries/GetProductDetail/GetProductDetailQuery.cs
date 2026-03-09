using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Catalog.Application.Queries.GetProductDetail;

public record GetProductDetailQuery(string Code) : IRequest<Result<ProductDetailDto>>;

public record ProductDetailDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? ShortDescriptionI18n,
    Guid ProductGroupId,
    bool IsActive,
    List<VariantDto> Variants);

public record VariantDto(
    Guid Id,
    string Sku,
    decimal BasePrice,
    decimal? BaseCost,
    bool IsActive,
    List<VariantImageDto> Images);

public record VariantImageDto(Guid Id, string ImageUrl, int SortOrder, bool IsMain);
