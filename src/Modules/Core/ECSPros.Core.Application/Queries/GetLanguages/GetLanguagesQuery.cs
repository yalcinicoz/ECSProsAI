using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Core.Application.Queries.GetLanguages;

public record GetLanguagesQuery(bool ActiveOnly = true) : IRequest<Result<List<LanguageDto>>>;

public record LanguageDto(
    Guid Id,
    string Code,
    string NativeName,
    string Direction,
    bool IsDefault,
    bool IsActive,
    int SortOrder);
