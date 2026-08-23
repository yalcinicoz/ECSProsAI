using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Contracts.Channels;
using ECSPros.Storefront.Application.Services.ChannelScoping;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetChannelProductsAdmin;

/// <summary>
/// "Tüm eşleşenleri seç" (toplu K2/K3) için: verilen filtreye (arama + durum) uyan tüm ürün
/// Id'lerini döner. Frontend bu id'leri toplu komuta geçirir (28K id'yi sayfa sayfa toplamaya
/// gerek kalmaz). GetChannelProductsAdminQuery ile aynı filtre semantiği.
/// </summary>
public record GetChannelProductIdsAdminQuery(
    Guid FirmPlatformId,
    string? Search = null,
    string? Status = null,
    IReadOnlyCollection<Guid>? RestrictToProductIds = null) : IRequest<Result<List<Guid>>>;

public class GetChannelProductIdsAdminQueryHandler(IStorefrontDbContext sfDb, ICatalogDbContext catDb, IChannelCapabilityResolver capabilityResolver)
    : IRequestHandler<GetChannelProductIdsAdminQuery, Result<List<Guid>>>
{
    public async Task<Result<List<Guid>>> Handle(GetChannelProductIdsAdminQuery request, CancellationToken ct)
    {
        var simdi = DateTime.UtcNow;

        var baseQuery = catDb.Products.AsNoTracking()
            .Where(p => catDb.ProductImages.Any(img => img.ProductId == p.Id));

        // F5 K6: kanalın kapalı olduğu kaynaklar (seller/supply) listede görünmez.
        var allowedSources = ChannelScopeResolver.AllowedSourceTypes(await capabilityResolver.GetAsync(request.FirmPlatformId, ct));
        if (allowedSources.Count < 3)
            baseQuery = baseQuery.Where(p => allowedSources.Contains(p.SourceType));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim().ToLower();
            baseQuery = baseQuery.Where(p => p.Code.ToLower().Contains(s)
                || PgJsonFunctions.JsonText(p.NameI18n, "tr")!.ToLower().Contains(s));
        }

        // F1 kapsam: filter|mixed kanalda taban = kapsamdaki ürünler; all'da manuel hariç tutulanlar düşer.
        var filterBased = await sfDb.ChannelScopes.AsNoTracking()
            .AnyAsync(sc => sc.FirmPlatformId == request.FirmPlatformId && (sc.FillType == "filter" || sc.FillType == "mixed"), ct);
        var scopeRows = await sfDb.ChannelProducts.AsNoTracking()
            .Where(cp => cp.FirmPlatformId == request.FirmPlatformId && (cp.IsExcluded || (filterBased && cp.InScope)))
            .Select(cp => new { cp.ProductId, cp.InScope, cp.IsExcluded })
            .ToListAsync(ct);
        if (filterBased)
        {
            var inScope = scopeRows.Where(r => r.InScope && !r.IsExcluded).Select(r => r.ProductId).ToHashSet();
            baseQuery = baseQuery.Where(p => inScope.Contains(p.Id));
        }
        else
        {
            var manuallyExcluded = scopeRows.Where(r => r.IsExcluded).Select(r => r.ProductId).ToHashSet();
            if (manuallyExcluded.Count > 0) baseQuery = baseQuery.Where(p => !manuallyExcluded.Contains(p.Id));
        }

        if (request.RestrictToProductIds is not null)
        {
            var allow = request.RestrictToProductIds as HashSet<Guid> ?? request.RestrictToProductIds.ToHashSet();
            baseQuery = baseQuery.Where(p => allow.Contains(p.Id));
        }

        var status = request.Status?.ToLower();
        if (status is "excluded" or "stopped" or "selected")
        {
            var stateRows = await sfDb.ChannelProducts.AsNoTracking()
                .Where(cp => cp.FirmPlatformId == request.FirmPlatformId)
                .Select(cp => new { cp.ProductId, cp.IsActive, cp.SaleStoppedFrom, cp.SaleStoppedUntil })
                .ToListAsync(ct);

            if (status is "excluded")
            {
                var ids = stateRows.Where(r => !r.IsActive).Select(r => r.ProductId).ToHashSet();
                baseQuery = baseQuery.Where(p => ids.Contains(p.Id));
            }
            else if (status is "stopped")
            {
                var ids = stateRows.Where(r => r.SaleStoppedFrom.HasValue && r.SaleStoppedFrom.Value <= simdi
                            && (!r.SaleStoppedUntil.HasValue || r.SaleStoppedUntil.Value >= simdi))
                            .Select(r => r.ProductId).ToHashSet();
                baseQuery = baseQuery.Where(p => ids.Contains(p.Id));
            }
            else // selected — çıkarılmamış (opt-out)
            {
                var excluded = stateRows.Where(r => !r.IsActive).Select(r => r.ProductId).ToHashSet();
                baseQuery = baseQuery.Where(p => !excluded.Contains(p.Id));
            }
        }

        var ids2 = await baseQuery.Select(p => p.Id).ToListAsync(ct);
        return Result.Success(ids2);
    }
}
