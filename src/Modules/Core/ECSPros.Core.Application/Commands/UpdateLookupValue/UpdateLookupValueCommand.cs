using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Core.Application.Commands.UpdateLookupValue;

public record UpdateLookupValueCommand(
    Guid Id,
    Dictionary<string, string> NameI18n,
    string? Color,
    string? Icon,
    bool IsDefault,
    bool IsActive,
    int SortOrder,
    Guid UpdatedBy) : IRequest<Result<bool>>;
