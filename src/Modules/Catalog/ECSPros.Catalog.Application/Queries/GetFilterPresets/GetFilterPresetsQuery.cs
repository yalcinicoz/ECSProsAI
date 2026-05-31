using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetFilterPresets;

public record GetFilterPresetsQuery(bool ActiveOnly = false) : IRequest<Result<List<FilterPresetSummaryDto>>>;

public record FilterPresetSummaryDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    string? Description,
    bool IsActive,
    int SortOrder,
    int UsedInCategories);

public class GetFilterPresetsQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetFilterPresetsQuery, Result<List<FilterPresetSummaryDto>>>
{
    public async Task<Result<List<FilterPresetSummaryDto>>> Handle(GetFilterPresetsQuery request, CancellationToken ct)
    {
        var query = db.FilterPresets.AsQueryable();
        if (request.ActiveOnly) query = query.Where(fp => fp.IsActive);

        var presets = await query.OrderBy(fp => fp.SortOrder).ThenBy(fp => fp.Code).ToListAsync(ct);

        // Kaç kategoride kullanıldığını say
        var presetIds = presets.Select(p => p.Id).ToList();
        var usageCounts = await db.Categories
            .Where(c => c.FilterPresetId.HasValue && presetIds.Contains(c.FilterPresetId!.Value))
            .GroupBy(c => c.FilterPresetId!.Value)
            .Select(g => new { PresetId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.PresetId, g => g.Count, ct);

        var result = presets.Select(fp => new FilterPresetSummaryDto(
            fp.Id, fp.Code, fp.NameI18n, fp.Description, fp.IsActive, fp.SortOrder,
            usageCounts.GetValueOrDefault(fp.Id, 0))).ToList();

        return Result.Success(result);
    }
}
