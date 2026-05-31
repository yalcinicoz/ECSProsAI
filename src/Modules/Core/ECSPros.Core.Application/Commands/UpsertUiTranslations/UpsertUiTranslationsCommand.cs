using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Core.Application.Commands.UpsertUiTranslations;

public record UpsertUiTranslationsCommand(List<UiTranslationItem> Items)
    : IRequest<Result<int>>;

public record UiTranslationItem(
    string Namespace,
    string Key,
    string Lang,
    string Value);
