using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Core.Application.Commands.CreateLookupValue;

public record CreateLookupValueCommand(
    string TypeCode,
    string Code,
    Dictionary<string, string> NameI18n,
    string? Color,
    string? Icon,
    bool IsDefault = false,
    int SortOrder = 0) : IRequest<Result<Guid>>;
