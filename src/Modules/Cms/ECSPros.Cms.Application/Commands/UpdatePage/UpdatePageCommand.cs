using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Cms.Application.Commands.UpdatePage;

public record UpdatePageCommand(
    Guid Id,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string> SlugI18n,
    Dictionary<string, string>? MetaTitleI18n,
    Dictionary<string, string>? MetaDescriptionI18n,
    bool IsActive,
    DateTime? PublishAt,
    DateTime? UnpublishAt,
    string? TargetGender,
    Guid UpdatedBy) : IRequest<Result<bool>>;
