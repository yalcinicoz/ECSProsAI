using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Catalog.Application.Commands.UpdateProduct;

public record UpdateProductCommand(
    Guid Id,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? ShortDescriptionI18n,
    Dictionary<string, string>? DescriptionI18n,
    decimal BasePrice,
    decimal? BaseCost,
    int TaxRate,
    bool IsActive,
    Guid? SupplierId,
    string? SupplierProductCode,
    Guid UpdatedBy,
    string? UpdatedByName = null) : IRequest<Result<bool>>;
