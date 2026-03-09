using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Core.Application.Queries.GetLookupValues;

public record GetLookupValuesQuery(string TypeCode, bool ActiveOnly = true) : IRequest<Result<List<LookupValueDto>>>;

public record LookupValueDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    string? Color,
    string? Icon,
    bool IsDefault,
    bool IsActive,
    int SortOrder);
