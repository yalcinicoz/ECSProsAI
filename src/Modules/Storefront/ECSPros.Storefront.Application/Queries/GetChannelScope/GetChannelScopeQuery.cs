using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetChannelScope;

public record GetChannelScopeQuery(Guid FirmPlatformId) : IRequest<Result<ChannelScopeDto>>;

public record ChannelScopeProductDto(Guid ProductId, string Code, Dictionary<string, string> NameI18n);

public record ChannelScopeDto(
    Guid FirmPlatformId,
    string FillType,
    Dictionary<string, object>? FilterDef,
    DateTime? SyncedAt,
    int? MatchedCount,
    string? LastSyncError,
    int InScopeCount,            // filter/mixed: InScope && !IsExcluded satır sayısı; all: null anlamlı değil → -1
    List<ChannelScopeProductDto> ManualIncluded,
    List<ChannelScopeProductDto> ManualExcluded);

public class GetChannelScopeQueryHandler(IStorefrontDbContext sfDb, ICatalogDbContext catDb)
    : IRequestHandler<GetChannelScopeQuery, Result<ChannelScopeDto>>
{
    public async Task<Result<ChannelScopeDto>> Handle(GetChannelScopeQuery request, CancellationToken ct)
    {
        var scope = await sfDb.ChannelScopes.AsNoTracking().FirstOrDefaultAsync(s => s.FirmPlatformId == request.FirmPlatformId, ct);
        var fill = scope?.FillType ?? "all";

        var manualRows = await sfDb.ChannelProducts.AsNoTracking()
            .Where(cp => cp.FirmPlatformId == request.FirmPlatformId && (cp.ScopeSource == "manual" || cp.IsExcluded))
            .Select(cp => new { cp.ProductId, cp.IsExcluded, cp.InScope })
            .ToListAsync(ct);
        var inScopeCount = fill == "all" ? -1 :
            await sfDb.ChannelProducts.AsNoTracking().CountAsync(cp => cp.FirmPlatformId == request.FirmPlatformId && cp.InScope && !cp.IsExcluded, ct);

        var ids = manualRows.Select(r => r.ProductId).ToList();
        var products = ids.Count == 0 ? new() : await catDb.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id)).Select(p => new ChannelScopeProductDto(p.Id, p.Code, p.NameI18n)).ToListAsync(ct);
        var byId = products.ToDictionary(p => p.ProductId);

        List<ChannelScopeProductDto> Pick(bool excluded) => manualRows
            .Where(r => r.IsExcluded == excluded && (excluded || r.InScope))
            .Select(r => byId.TryGetValue(r.ProductId, out var p) ? p : null).Where(p => p is not null)!
            .OrderBy(p => p!.Code).ToList()!;

        return Result.Success(new ChannelScopeDto(request.FirmPlatformId, fill, scope?.FilterDef, scope?.SyncedAt,
            scope?.MatchedCount, scope?.LastSyncError, inScopeCount, Pick(false), Pick(true)));
    }
}
