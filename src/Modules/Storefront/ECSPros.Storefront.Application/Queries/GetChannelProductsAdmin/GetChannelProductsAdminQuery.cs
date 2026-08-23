using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetChannelProductsAdmin;

/// <summary>
/// Admin "Kanal Ürünleri" (satış görünürlüğü M2/M3) toplu yönetim listesi. Katalog ürünlerini
/// (görselli) + bu platformdaki channel_products durumunu (seçili mi / durdurma penceresi)
/// birleştirir. Status filtresi opt-out semantiğine göre: satır yok/seçili → "kanalda".
/// </summary>
public record GetChannelProductsAdminQuery(
    Guid FirmPlatformId,
    string? Search = null,
    string? Status = null,   // all | selected | excluded | stopped
    int Page = 1,
    int PageSize = 30) : IRequest<Result<PagedResult<ChannelProductAdminItemDto>>>;

public record ChannelProductAdminItemDto(
    Guid ProductId,
    string Code,
    Dictionary<string, string> NameI18n,
    string? MainImageUrl,
    bool IsSelected,                  // opt-out: satır yok VEYA IsActive=true
    DateTime? SaleStoppedFrom,
    DateTime? SaleStoppedUntil,
    bool IsStoppedNow);

public class GetChannelProductsAdminQueryHandler(IStorefrontDbContext sfDb, ICatalogDbContext catDb)
    : IRequestHandler<GetChannelProductsAdminQuery, Result<PagedResult<ChannelProductAdminItemDto>>>
{
    public async Task<Result<PagedResult<ChannelProductAdminItemDto>>> Handle(
        GetChannelProductsAdminQuery request, CancellationToken ct)
    {
        var simdi = DateTime.UtcNow;

        // Katalog ürün tabanı: görselli, silinmemiş (satışa kapalı ürünler de yönetilebilsin).
        var baseQuery = catDb.Products.AsNoTracking()
            .Where(p => catDb.ProductImages.Any(img => img.ProductId == p.Id));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim().ToLower();
            baseQuery = baseQuery.Where(p => p.Code.ToLower().Contains(s)
                || PgJsonFunctions.JsonText(p.NameI18n, "tr")!.ToLower().Contains(s));
        }

        // Bu platformdaki durum kayıtları (deny/durdurma) — bellek içi birleştirilecek küme
        // küçük (yalnız çıkarılan/durdurulan/durum atanmış ürünler).
        var stateRows = await sfDb.ChannelProducts.AsNoTracking()
            .Where(cp => cp.FirmPlatformId == request.FirmPlatformId)
            .Select(cp => new { cp.ProductId, cp.IsActive, cp.SaleStoppedFrom, cp.SaleStoppedUntil, cp.InScope, cp.IsExcluded })
            .ToListAsync(ct);
        var stateByProduct = stateRows.ToDictionary(r => r.ProductId);

        // F1 kapsam: filter|mixed kanalda liste tabanı = kapsamdaki ürünler (InScope && !IsExcluded); all → tüm katalog.
        var filterBased = await sfDb.ChannelScopes.AsNoTracking()
            .AnyAsync(s => s.FirmPlatformId == request.FirmPlatformId && (s.FillType == "filter" || s.FillType == "mixed"), ct);
        if (filterBased)
        {
            var inScope = stateRows.Where(r => r.InScope && !r.IsExcluded).Select(r => r.ProductId).ToHashSet();
            baseQuery = baseQuery.Where(p => inScope.Contains(p.Id));
        }
        else
        {
            var manuallyExcluded = stateRows.Where(r => r.IsExcluded).Select(r => r.ProductId).ToHashSet();
            if (manuallyExcluded.Count > 0) baseQuery = baseQuery.Where(p => !manuallyExcluded.Contains(p.Id));
        }

        bool Selected(Guid pid) => !stateByProduct.TryGetValue(pid, out var st) || st.IsActive;
        bool StoppedNow(Guid pid) => stateByProduct.TryGetValue(pid, out var st)
            && st.SaleStoppedFrom.HasValue && st.SaleStoppedFrom.Value <= simdi
            && (!st.SaleStoppedUntil.HasValue || st.SaleStoppedUntil.Value >= simdi);

        // Durum filtresi kayıt kümesine göre daraltılır. "selected"/"stopped"/"excluded" için
        // ilgili ürün Id kümesi kullanılır; "all" için tüm katalog.
        var status = request.Status?.ToLower();
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
        else if (status is "selected")
        {
            // Kanalda: çıkarılmamış (opt-out). Çıkarılan Id'ler hariç tümü.
            var excluded = stateRows.Where(r => !r.IsActive).Select(r => r.ProductId).ToHashSet();
            baseQuery = baseQuery.Where(p => !excluded.Contains(p.Id));
        }

        var total = await baseQuery.CountAsync(ct);

        var paged = await baseQuery
            .OrderBy(p => p.Code)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new { p.Id, p.Code, p.NameI18n })
            .ToListAsync(ct);

        var productIds = paged.Select(p => p.Id).ToList();
        var cdnBase = await CdnHelper.BuildListUrlAsync(catDb, ct);
        var images = await catDb.ProductImages.AsNoTracking()
            .Where(img => productIds.Contains(img.ProductId))
            .GroupBy(img => img.ProductId)
            .Select(g => new { ProductId = g.Key, FileName = g.OrderBy(i => i.SortOrder).First().FileName })
            .ToDictionaryAsync(x => x.ProductId, x => x.FileName, ct);

        var items = paged.Select(p =>
        {
            stateByProduct.TryGetValue(p.Id, out var st);
            return new ChannelProductAdminItemDto(
                p.Id, p.Code, p.NameI18n,
                images.TryGetValue(p.Id, out var fn) ? cdnBase + fn : null,
                Selected(p.Id),
                st?.SaleStoppedFrom, st?.SaleStoppedUntil,
                StoppedNow(p.Id));
        }).ToList();

        return Result.Success(new PagedResult<ChannelProductAdminItemDto>(
            items, total, request.Page, request.PageSize));
    }
}
