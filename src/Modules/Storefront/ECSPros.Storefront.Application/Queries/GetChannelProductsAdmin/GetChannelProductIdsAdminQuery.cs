using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
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
    string? Status = null) : IRequest<Result<List<Guid>>>;

public class GetChannelProductIdsAdminQueryHandler(IStorefrontDbContext sfDb, ICatalogDbContext catDb)
    : IRequestHandler<GetChannelProductIdsAdminQuery, Result<List<Guid>>>
{
    public async Task<Result<List<Guid>>> Handle(GetChannelProductIdsAdminQuery request, CancellationToken ct)
    {
        var simdi = DateTime.UtcNow;

        var baseQuery = catDb.Products.AsNoTracking()
            .Where(p => catDb.ProductImages.Any(img => img.ProductId == p.Id));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim().ToLower();
            baseQuery = baseQuery.Where(p => p.Code.ToLower().Contains(s)
                || PgJsonFunctions.JsonText(p.NameI18n, "tr")!.ToLower().Contains(s));
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
