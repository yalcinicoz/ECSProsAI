using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Catalog.Application.Commands.CreateProduct;

public record CreateProductCommand(
    Guid ProductGroupId,
    string Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? ShortDescriptionI18n,
    List<CreateVariantDto> Variants) : IRequest<Result<Guid>>;

public record CreateVariantDto(
    string Sku,
    decimal BasePrice,
    decimal? BaseCost);
