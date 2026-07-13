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
    Guid UpdatedBy,
    // P1c: null = ExtraData'ya dokunma; dolu = olduğu gibi yaz (iade alt nedenleri vb.)
    Dictionary<string, object>? ExtraData = null) : IRequest<Result<bool>>;
