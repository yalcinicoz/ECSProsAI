using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetDraftBlocksWithItems;

/// <summary>
/// Vitrin canlı-önizlemeli editör (2026-07-22): bir yerleşimin TASLAK blokları,
/// öğeleriyle (görsel/başlık/link) birlikte tek sorguda — admin editörü blokları
/// gerçek içerikle dikey önizler. Ürün/koleksiyon kaynak çözümü API katmanında
/// (resolver) yapılır; bu sorgu yalnız blok+öğe verisini taşır.
/// </summary>
public record GetDraftBlocksWithItemsQuery(Guid FirmPlatformId, string Placement)
    : IRequest<Result<List<DraftBlockDto>>>;

public record DraftBlockDto(
    Guid Id,
    string BlockType,
    string? Template,
    Dictionary<string, string> TitleI18n,
    Dictionary<string, string>? SubtitleI18n,
    int SortOrder,
    bool IsActive,
    DateTime? StartAt,
    DateTime? EndAt,
    string? ConfigJson,
    List<DraftBlockItemDto> Items);

public record DraftBlockItemDto(
    Guid Id,
    Dictionary<string, string> TitleI18n,
    Dictionary<string, string>? SubtitleI18n,
    string? ImageUrl,
    string? MobileImageUrl,
    string? VideoUrl,
    string? LinkUrl,
    string? BadgeLabel,
    bool IsActive,
    string? ConfigJson);

public class GetDraftBlocksWithItemsQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetDraftBlocksWithItemsQuery, Result<List<DraftBlockDto>>>
{
    public async Task<Result<List<DraftBlockDto>>> Handle(GetDraftBlocksWithItemsQuery request, CancellationToken ct)
    {
        var bloklar = await db.PageBlocks.AsNoTracking()
            .Where(b => b.FirmPlatformId == request.FirmPlatformId && b.Placement == request.Placement)
            .OrderBy(b => b.SortOrder).ThenBy(b => b.Priority)
            .Select(b => new
            {
                b.Id, b.BlockType, b.Template, b.TitleI18n, b.SubtitleI18n,
                b.SortOrder, b.IsActive, b.StartAt, b.EndAt, b.ConfigJson,
                Items = b.Items.Where(i => !i.IsDeleted)
                    .OrderBy(i => i.SortOrder).ThenBy(i => i.Priority)
                    .Select(i => new DraftBlockItemDto(
                        i.Id, i.TitleI18n, i.SubtitleI18n, i.ImageUrl, i.MobileImageUrl,
                        i.VideoUrl, i.LinkUrl, i.BadgeLabel, i.IsActive, i.ConfigJson))
                    .ToList(),
            })
            .ToListAsync(ct);

        return Result.Success(bloklar.Select(b => new DraftBlockDto(
            b.Id, b.BlockType, b.Template, b.TitleI18n, b.SubtitleI18n,
            b.SortOrder, b.IsActive, b.StartAt, b.EndAt, b.ConfigJson, b.Items)).ToList());
    }
}
