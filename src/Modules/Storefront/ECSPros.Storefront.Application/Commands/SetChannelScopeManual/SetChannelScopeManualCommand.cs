using ECSPros.Shared.Contracts.Channels;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Application.Services.ChannelScoping;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Storefront.Application.Commands.SetChannelScopeManual;

/// <summary>
/// Kapsam katmanında manuel işlem: include (ScopeSource=manual, InScope=true, IsExcluded=false),
/// exclude (IsExcluded=true → kalıcı kapsam dışı, sync geri eklemez), clear (manuel karar kaldırılır:
/// IsExcluded=false; manual satır filter'a döner ve InScope bir sonraki sync'e kadar olduğu gibi kalır).
/// </summary>
public record SetChannelScopeManualCommand(Guid FirmPlatformId, List<Guid> ProductIds, string Action)
    : IRequest<Result<int>>;

public class SetChannelScopeManualCommandHandler(
    IStorefrontDbContext sfDb, IChannelCapabilityResolver capabilities, IMemoryCache cache)
    : IRequestHandler<SetChannelScopeManualCommand, Result<int>>
{
    public async Task<Result<int>> Handle(SetChannelScopeManualCommand request, CancellationToken ct)
    {
        var action = (request.Action ?? "").Trim().ToLowerInvariant();
        if (action is not ("include" or "exclude" or "clear")) return Result.Failure<int>("Geçersiz işlem (include|exclude|clear).");
        var ids = request.ProductIds.Distinct().ToList();
        if (ids.Count == 0) return Result.Success(0);

        var rows = await sfDb.ChannelProducts
            .Where(cp => cp.FirmPlatformId == request.FirmPlatformId && ids.Contains(cp.ProductId))
            .ToListAsync(ct);
        var byProduct = rows.ToDictionary(r => r.ProductId);
        var caps = await capabilities.GetAsync(request.FirmPlatformId, ct);
        var now = DateTime.UtcNow; var n = 0;

        foreach (var pid in ids)
        {
            byProduct.TryGetValue(pid, out var row);
            switch (action)
            {
                case "include":
                    if (row is null)
                        sfDb.ChannelProducts.Add(new ChannelProduct { FirmPlatformId = request.FirmPlatformId, ProductId = pid,
                            InScope = true, ScopeSource = "manual", IsActive = caps.AutoPublish, CreatedAt = now });
                    else { row.InScope = true; row.ScopeSource = "manual"; row.IsExcluded = false; row.UpdatedAt = now; }
                    n++; break;
                case "exclude":
                    if (row is null)
                        sfDb.ChannelProducts.Add(new ChannelProduct { FirmPlatformId = request.FirmPlatformId, ProductId = pid,
                            InScope = false, ScopeSource = "manual", IsExcluded = true, CreatedAt = now });
                    else { row.IsExcluded = true; row.InScope = false; row.ScopeSource = "manual"; row.UpdatedAt = now; }
                    n++; break;
                case "clear":
                    if (row is null) break;
                    row.IsExcluded = false; row.ScopeSource = "filter"; row.UpdatedAt = now; n++; break;
            }
        }
        await sfDb.SaveChangesAsync(ct);
        cache.Remove(ChannelProductCacheKeys.Excluded(request.FirmPlatformId));
        return Result.Success(n);
    }
}
