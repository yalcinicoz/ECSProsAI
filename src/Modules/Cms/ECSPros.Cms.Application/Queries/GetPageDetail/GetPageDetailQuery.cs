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
    DateTime CreatedAt,
    // P2a additive: bölümler (P2b editörü bunların içeriğini düzenler)
    List<PageSectionDto>? Sections = null);

public record PageSectionDto(
    Guid Id,
    string SectionTypeCode,
    string? Name,
    Dictionary<string, string>? TitleI18n,
    Dictionary<string, object> Settings,
    bool IsActive,
    int SortOrder,
    DateTime? UpdatedAt,
    List<PageSectionItemDto> Items);

public record PageSectionItemDto(
    Guid Id,
    string ItemType,
    Dictionary<string, string>? TitleI18n,
    Dictionary<string, string>? DescriptionI18n,
    bool IsActive,
    int SortOrder);
