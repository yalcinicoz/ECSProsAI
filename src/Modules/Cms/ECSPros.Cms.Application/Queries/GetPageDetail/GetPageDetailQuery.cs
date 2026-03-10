using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Cms.Application.Queries.GetPageDetail;

public record GetPageDetailQuery(Guid PageId) : IRequest<Result<PageDetailDto>>;

public record PageDetailDto(
    Guid Id,
    Guid FirmPlatformId,
    Guid TemplateId,
    string Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string> SlugI18n,
    string PageType,
    string? TargetGender,
    Guid? TargetCategoryId,
    Dictionary<string, string>? MetaTitleI18n,
    Dictionary<string, string>? MetaDescriptionI18n,
    bool IsActive,
    DateTime? PublishAt,
    DateTime? UnpublishAt,
    DateTime CreatedAt);
