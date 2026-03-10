using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Catalog.Application.Commands.UpdateProduct;

public record UpdateProductCommand(
    Guid Id,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? ShortDescriptionI18n,
    bool IsActive,
    Guid UpdatedBy) : IRequest<Result<bool>>;
