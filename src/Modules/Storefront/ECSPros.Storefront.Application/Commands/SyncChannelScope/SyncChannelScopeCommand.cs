using ECSPros.Catalog.Application.Helpers;
using ECSPros.Shared.Contracts.Channels;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Application.Services.ChannelScoping;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Storefront.Application.Commands.SyncChannelScope;

/// <summary>
/// F1 kapsam materyalizasyonu: filter|mixed kanalda FilterDef çalıştırılır; eşleşenler channel_products'a
/// InScope=true/ScopeSource=filter yazılır (yeni satır IsActive = kanal yeteneği autoPublish), eşleşmeyen
/// filter-kaynaklı satırlar InScope=false olur (kanal kararı/durdurma geçmişi silinmez). manual satırlar ve
/// IsExcluded korunur. all kanalda sync no-op (kapsam örtük). Sonuç: filtreden geçen ürün sayısı.
/// </summary>
public record SyncChannelScopeCommand(Guid FirmPlatformId) : IRequest<Result<int>>;

public class SyncChannelScopeCommandHandler(
    IStorefrontDbContext sfDb,
    ChannelScopeResolver resolver,
    IChannelCapabilityResolver capabilities,
    IMemoryCache cache)
    : IRequestHandler<SyncChannelScopeCommand, Result<int>>
{
    public async Task<Result<int>> Handle(SyncChannelScopeCommand request, CancellationToken ct)
    {
        var scope = await sfDb.ChannelScopes.FirstOrDefaultAsync(s => s.FirmPlatformId == request.FirmPlatformId, ct);
        if (scope is null || !scope.IsFilterBased)
        {
            cache.Remove(ChannelProductCacheKeys.Excluded(request.FirmPlatformId));
            return Result.Success(0);
        }

        try
        {
            var rules = CategoryFilterRules.From(scope.FilterDef) ?? new CategoryFilterRules();
            var matched = (await resolver.ResolveAsync(request.FirmPlatformId, rules, ct)).ToHashSet();
            var caps = await capabilities.GetAsync(request.FirmPlatformId, ct);

            var rows = await sfDb.ChannelProducts
                .Where(cp => cp.FirmPlatformId == request.FirmPlatformId)
                .ToListAsync(ct);
            var byProduct = rows.ToDictionary(r => r.ProductId);
            var now = DateTime.UtcNow;

            foreach (var pid in matched)
            {
                if (byProduct.TryGetValue(pid, out var row))
                {
                    // manual/legacy satır: kapsam bayrağını aç, kaynağı koru (manual kalır; legacy → filter)
                    if (!row.InScope) { row.InScope = true; row.UpdatedAt = now; }
                    if (row.ScopeSource == "legacy") row.ScopeSource = "filter";
                }
                else
                {
                    sfDb.ChannelProducts.Add(new ChannelProduct
                    {
                        FirmPlatformId = request.FirmPlatformId,
                        ProductId = pid,
                        InScope = true,
                        ScopeSource = "filter",
                        IsActive = caps.AutoPublish,
                        CreatedAt = now,
                    });
                }
            }

            // filtreden düşenler: yalnız filter (ve legacy) kaynaklı satırlar kapsam dışına; manual satırlar kalır
            foreach (var row in rows)
            {
                if (matched.Contains(row.ProductId)) continue;
                if (row.ScopeSource == "manual") continue;
                if (row.InScope) { row.InScope = false; row.UpdatedAt = now; }
                if (row.ScopeSource == "legacy") row.ScopeSource = "filter";
            }

            scope.SyncedAt = now;
            scope.MatchedCount = matched.Count;
            scope.LastSyncError = null;
            await sfDb.SaveChangesAsync(ct);
            cache.Remove(ChannelProductCacheKeys.Excluded(request.FirmPlatformId));
            return Result.Success(matched.Count);
        }
        catch (Exception ex)
        {
            scope.LastSyncError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            scope.SyncedAt = DateTime.UtcNow;
            await sfDb.SaveChangesAsync(ct);
            return Result.Failure<int>("Kapsam güncellenemedi: " + ex.Message);
        }
    }
}
