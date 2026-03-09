using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Cms.Application.Commands.CreatePage;

public record CreatePageCommand(
    Guid FirmPlatformId,
    Guid TemplateId,
    string Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string> SlugI18n,
    string PageType,
    string? TargetGender,
    Guid? TargetCategoryId,
    DateTime? PublishAt,
    DateTime? UnpublishAt) : IRequest<Result<Guid>>;
