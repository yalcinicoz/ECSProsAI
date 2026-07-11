using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetPageBlocks;

/// <summary>G6: admin taslak blok listesi (yerleşim filtresiyle; sıralı).</summary>
public record GetPageBlocksQuery(
    Guid FirmPlatformId,
    string? Placement = null) : IRequest<Result<List<PageBlockListItemDto>>>;

public record PageBlockListItemDto(
    Guid Id,
    string Placement,
    string BlockType,
    string? Template,
    Dictionary<string, string> TitleI18n,
    int SortOrder,
    bool IsActive,
    int Priority,
    DateTime? StartAt,
    DateTime? EndAt,
    int ItemCount,
    bool HasProductSource,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public class GetPageBlocksQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetPageBlocksQuery, Result<List<PageBlockListItemDto>>>
{
    public async Task<Result<List<PageBlockListItemDto>>> Handle(GetPageBlocksQuery request, CancellationToken ct)
    {
        var q = db.PageBlocks.AsNoTracking()
            .Where(b => b.FirmPlatformId == request.FirmPlatformId);
        if (!string.IsNullOrEmpty(request.Placement))
            q = q.Where(b => b.Placement == request.Placement);

        // HasProductSource bellek tarafında: jsonb kolonda LIKE/Contains PostgreSQL'e
        // "jsonb ~~ unknown" olarak gider ve 42883 fırlatır (E2E buldu).
        var bloklar = await q
            .OrderBy(b => b.Placement).ThenBy(b => b.SortOrder).ThenBy(b => b.Priority)
            .Select(b => new
            {
                b.Id, b.Placement, b.BlockType, b.Template, b.TitleI18n,
                b.SortOrder, b.IsActive, b.Priority, b.StartAt, b.EndAt,
                ItemCount = b.Items.Count(i => !i.IsDeleted),
                b.ConfigJson, b.CreatedAt, b.UpdatedAt,
            })
            .ToListAsync(ct);

        return Result.Success(bloklar.Select(b => new PageBlockListItemDto(
            b.Id, b.Placement, b.BlockType, b.Template, b.TitleI18n,
            b.SortOrder, b.IsActive, b.Priority, b.StartAt, b.EndAt,
            b.ItemCount,
            b.ConfigJson?.Contains("productSource") == true,
            b.CreatedAt, b.UpdatedAt)).ToList());
    }
}
