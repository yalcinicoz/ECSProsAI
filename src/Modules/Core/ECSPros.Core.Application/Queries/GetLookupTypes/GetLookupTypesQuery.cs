using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Core.Application.Queries.GetLookupTypes;

public record GetLookupTypesQuery : IRequest<Result<List<LookupTypeDto>>>;

public record LookupTypeDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    string? Description,
    bool IsSystem);
