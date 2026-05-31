using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Catalog.Application.Queries.GetProductDetail;

public record GetProductDetailQuery(string Code) : IRequest<Result<ProductDetailDto>>;

public record ProductDetailDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? ShortDescriptionI18n,
    Dictionary<string, string>? DescriptionI18n,
    Guid ProductGroupId,
    bool IsActive,
    decimal BasePrice,
    decimal? BaseCost,
    int TaxRate,
    Guid? SupplierId,
    string? SupplierProductCode,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<ProductAttributeDto> Attributes,
    List<VariantDto> Variants,
    List<string> Tags,
    string? Slug,
    Dictionary<string, string>? MetaTitleI18n,
    Dictionary<string, string>? MetaDescriptionI18n,
    Dictionary<string, string>? MetaKeywordsI18n,
    List<AxisSubAttributeSchemaDto> AxisSubAttributeSchema,
    List<ProductAxisSubAttributeValueDto> AxisSubAttributeValues);

public record AxisSubAttributeSchemaDto(
    Guid AxisAttributeTypeId,
    string AxisAttributeTypeCode,
    Dictionary<string, string> AxisAttributeTypeNameI18n,
    List<SubAttributeSchemaItemDto> SubAttributes);

public record SubAttributeSchemaItemDto(
    Guid SubAttributeTypeId,
    string SubAttributeTypeCode,
    Dictionary<string, string> SubAttributeTypeNameI18n,
    bool IsRequired);

public record ProductAxisSubAttributeValueDto(
    Guid AttributeValueId,
    Guid SubAttributeTypeId,
    string Value);

public record ProductAttributeDto(
    Guid Id,
    Guid AttributeTypeId,
    string AttributeTypeCode,
    Dictionary<string, string> AttributeTypeNameI18n,
    Guid? AttributeValueId,
    Dictionary<string, string>? AttributeValueNameI18n,
    string? CustomValue);

public record VariantDto(
    Guid Id,
    string Sku,
    string? Barcode,
    decimal BasePrice,
    decimal? BaseCost,
    bool IsActive,
    List<VariantAttributeDto> VariantAttributes,
    List<VariantImageDto> Images);

public record VariantAttributeDto(
    Guid AttributeTypeId,
    string AttributeTypeCode,
    Dictionary<string, string> AttributeTypeNameI18n,
    Guid AttributeValueId,
    Dictionary<string, string> AttributeValueNameI18n);

public record VariantImageDto(Guid Id, string ImageUrl, int SortOrder, bool IsMain);
