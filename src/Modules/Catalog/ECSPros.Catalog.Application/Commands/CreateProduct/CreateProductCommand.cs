using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Catalog.Application.Commands.CreateProduct;

public record CreateProductCommand(
    Guid ProductGroupId,
    string? Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? ShortDescriptionI18n,
    Dictionary<string, string>? DescriptionI18n,
    decimal BasePrice,
    decimal? BaseCost,
    int TaxRate,
    List<CreateVariantDto>? Variants) : IRequest<Result<CreateProductResult>>;

public record CreateProductResult(Guid Id, string Code);

public record CreateVariantDto(
    string Sku,
    decimal BasePrice,
    decimal? BaseCost);
