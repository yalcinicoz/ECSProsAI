using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetPageBlockDetail;

/// <summary>G6: admin blok detayı — tüm alanlar + sıralı öğe listesi.</summary>
public record GetPageBlockDetailQuery(Guid Id, Guid FirmPlatformId) : IRequest<Result<PageBlockDetailDto>>;

public record PageBlockDetailDto(
    Guid Id,
    string Placement,
    string BlockType,
    string? Template,
    Dictionary<string, string> TitleI18n,
    Dictionary<string, string>? SubtitleI18n,
    int SortOrder,
    bool IsActive,
    DateTime? StartAt,
    DateTime? EndAt,
    int Priority,
    string? RuleJson,
    string? ConfigJson,
    List<PageBlockItemDto> Items);

public record PageBlockItemDto(
    Guid Id,
    Dictionary<string, string> TitleI18n,
    Dictionary<string, string>? SubtitleI18n,
    string? ImageUrl,
    string? MobileImageUrl,
    string? VideoUrl,
    string? LinkUrl,
    bool OpenInNewTab,
    Dictionary<string, string>? ButtonTextI18n,
    string? BadgeLabel,
    int SortOrder,
    bool IsActive,
    DateTime? StartAt,
    DateTime? EndAt,
    int Priority,
    string? RuleJson,
    string? ConfigJson);

public class GetPageBlockDetailQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetPageBlockDetailQuery, Result<PageBlockDetailDto>>
{
    public async Task<Result<PageBlockDetailDto>> Handle(GetPageBlockDetailQuery request, CancellationToken ct)
    {
        var blok = await db.PageBlocks.AsNoTracking()
            .Where(b => b.Id == request.Id && b.FirmPlatformId == request.FirmPlatformId)
            .Select(b => new PageBlockDetailDto(
                b.Id, b.Placement, b.BlockType, b.Template, b.TitleI18n, b.SubtitleI18n,
                b.SortOrder, b.IsActive, b.StartAt, b.EndAt, b.Priority, b.RuleJson, b.ConfigJson,
                b.Items.Where(i => !i.IsDeleted)
                    .OrderBy(i => i.SortOrder).ThenBy(i => i.Priority)
                    .Select(i => new PageBlockItemDto(
                        i.Id, i.TitleI18n, i.SubtitleI18n, i.ImageUrl, i.MobileImageUrl,
                        i.VideoUrl, i.LinkUrl, i.OpenInNewTab, i.ButtonTextI18n, i.BadgeLabel,
                        i.SortOrder, i.IsActive, i.StartAt, i.EndAt, i.Priority,
                        i.RuleJson, i.ConfigJson))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

        return blok is null
            ? Result.Failure<PageBlockDetailDto>("Blok bulunamadı.")
            : Result.Success(blok);
    }
}
