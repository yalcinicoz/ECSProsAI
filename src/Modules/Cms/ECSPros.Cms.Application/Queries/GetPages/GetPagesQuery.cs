using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Cms.Application.Queries.GetPages;

public record GetPagesQuery(Guid? FirmPlatformId = null, bool ActiveOnly = true) : IRequest<Result<List<PageDto>>>;

public record PageDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string> SlugI18n,
    string PageType,
    bool IsActive,
    DateTime? PublishAt,
    DateTime? UnpublishAt);
