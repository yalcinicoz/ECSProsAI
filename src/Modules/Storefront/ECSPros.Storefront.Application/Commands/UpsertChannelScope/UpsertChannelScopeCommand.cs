using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Commands.SyncChannelScope;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Application.Services.ChannelScoping;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Storefront.Application.Commands.UpsertChannelScope;

/// <summary>Kanal kapsam tanımını kaydeder (all|filter|mixed + FilterDef) ve hemen sync çalıştırır.</summary>
public record UpsertChannelScopeCommand(Guid FirmPlatformId, string FillType, Dictionary<string, object>? FilterDef)
    : IRequest<Result<int>>;

public class UpsertChannelScopeCommandHandler(IStorefrontDbContext sfDb, IMediator mediator, IMemoryCache cache)
    : IRequestHandler<UpsertChannelScopeCommand, Result<int>>
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase) { "all", "filter", "mixed" };

    public async Task<Result<int>> Handle(UpsertChannelScopeCommand request, CancellationToken ct)
    {
        var fill = (request.FillType ?? "all").Trim().ToLowerInvariant();
        if (!Allowed.Contains(fill)) return Result.Failure<int>("Geçersiz doldurma tipi (all|filter|mixed).");
        if (fill != "all" && (request.FilterDef is null || request.FilterDef.Count == 0))
            return Result.Failure<int>("Filtre tabanlı kapsam için en az bir kural gerekir.");

        var scope = await sfDb.ChannelScopes.FirstOrDefaultAsync(s => s.FirmPlatformId == request.FirmPlatformId, ct);
        if (scope is null)
        {
            scope = new ChannelScope { FirmPlatformId = request.FirmPlatformId, CreatedAt = DateTime.UtcNow };
            sfDb.ChannelScopes.Add(scope);
        }
        scope.FillType = fill;
        scope.FilterDef = fill == "all" ? null : request.FilterDef;
        scope.UpdatedAt = DateTime.UtcNow;
        await sfDb.SaveChangesAsync(ct);

        if (fill == "all")
        {
            // all'a dönüş: filter kaynaklı "kapsam dışı" bayrakları temizlenir (örtük kapsam; satırlar karar için kalır)
            var rows = await sfDb.ChannelProducts
                .Where(cp => cp.FirmPlatformId == request.FirmPlatformId && !cp.InScope)
                .ToListAsync(ct);
            foreach (var r in rows) r.InScope = true;
            scope.MatchedCount = null; scope.SyncedAt = DateTime.UtcNow; scope.LastSyncError = null;
            await sfDb.SaveChangesAsync(ct);
            cache.Remove(ChannelProductCacheKeys.Excluded(request.FirmPlatformId));
            return Result.Success(0);
        }

        return await mediator.Send(new SyncChannelScopeCommand(request.FirmPlatformId), ct);
    }
}
