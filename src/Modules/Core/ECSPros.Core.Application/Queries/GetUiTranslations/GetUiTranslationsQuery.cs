using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Core.Application.Queries.GetUiTranslations;

public record GetUiTranslationsQuery(string? Namespace = null, string? Lang = null)
    : IRequest<Result<List<UiTranslationDto>>>;

public record UiTranslationDto(
    Guid Id,
    string Namespace,
    string Key,
    string Lang,
    string Value);
