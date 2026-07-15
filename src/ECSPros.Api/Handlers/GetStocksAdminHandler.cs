using ECSPros.Catalog.Application.Services;
using ECSPros.Inventory.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Handlers;

/// <summary>
/// Admin stok listesi (2026-07-15): eski GET /inventory/stocks 165K raf satırını sayfasız
/// döndürüp tarayıcıyı donduruyordu ve yalnız variantId gösteriyordu. Bu sorgu sayfalı çalışır
/// ve satırları açıklayıcı bilgiyle zenginleştirir (ürün kodu/adı/seçenek + depo/kısım/raf adı).
/// Cross-module olduğundan (Inventory + Catalog) Api katmanında (GetStoreProductDetailHandler deseni).
/// </summary>
public record GetStocksAdminQuery(
    string? Search = null,          // ürün kodu / adı / barkod / SKU
    Guid? WarehouseId = null,
    bool AvailableOnly = false,
    int Page = 1,
    int PageSize = 30) : IRequest<Result<PagedResult<StockAdminRowDto>>>;

public record StockAdminRowDto(
    Guid Id,
    Guid VariantId,
    string ProductCode,
    string ProductName,
    string? Options,                // "Beden: M, Renk: Beyaz"
    string? ImageUrl,
    string WarehouseName,
    string? SectionName,
    string? BinCode,
    int Quantity,
    int ReservedQuantity,
    int AvailableQuantity);

public class GetStocksAdminHandler(
    IInventoryDbContext invDb,
    ICatalogDbContext catDb,
    IProductService productService)
    : IRequestHandler<GetStocksAdminQuery, Result<PagedResult<StockAdminRowDto>>>
{
    public async Task<Result<PagedResult<StockAdminRowDto>>> Handle(
        GetStocksAdminQuery request, CancellationToken ct)
    {
        var query = invDb.Stocks.AsNoTracking().AsQueryable();

        if (request.WarehouseId.HasValue)
            query = query.Where(s => s.WarehouseId == request.WarehouseId);

        if (request.AvailableOnly)
            query = query.Where(s => s.Quantity > s.ReservedQuantity);

        // Arama: katalogda ürün kodu / Türkçe ad / barkod / SKU eşleşen varyant kümesi.
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim().ToLower();
            var variantIds = await catDb.ProductVariants.AsNoTracking()
                .Where(v => v.Product.Code.ToLower().Contains(s)
                         || (v.Barcode != null && v.Barcode.ToLower() == s)
                         || v.Sku.ToLower() == s
                         || Catalog.Application.Helpers.PgJsonFunctions.JsonText(v.Product.NameI18n, "tr")!.ToLower().Contains(s))
                .Select(v => v.Id)
                .Take(20000)
                .ToListAsync(ct);
            if (variantIds.Count == 0)
                return Result.Success(new PagedResult<StockAdminRowDto>([], 0, request.Page, request.PageSize));
            query = query.Where(st => variantIds.Contains(st.VariantId));
        }

        var total = await query.CountAsync(ct);

        // Aynı varyantın rafları alt alta gelsin diye varyant + kısım + raf sırası.
        var rows = await query
            .OrderBy(st => st.VariantId).ThenBy(st => st.SectionId).ThenBy(st => st.BinId)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(st => new { st.Id, st.VariantId, st.WarehouseId, st.SectionId, st.BinId, st.Quantity, st.ReservedQuantity })
            .ToListAsync(ct);

        // ── Zenginleştirme (yalnız sayfadaki satırlar) ──
        var displays = await productService.GetVariantDisplayAsync(rows.Select(r => r.VariantId).ToList(), ct);

        var whIds = rows.Select(r => r.WarehouseId).Distinct().ToList();
        var warehouses = await invDb.Warehouses.AsNoTracking()
            .Where(w => whIds.Contains(w.Id))
            .Select(w => new { w.Id, w.NameI18n })
            .ToDictionaryAsync(w => w.Id, w => w.NameI18n, ct);

        var secIds = rows.Where(r => r.SectionId.HasValue).Select(r => r.SectionId!.Value).Distinct().ToList();
        var sections = secIds.Count > 0
            ? await invDb.WarehouseSections.AsNoTracking()
                .Where(x => secIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
            : new Dictionary<Guid, string>();

        var binIds = rows.Where(r => r.BinId.HasValue).Select(r => r.BinId!.Value).Distinct().ToList();
        var bins = binIds.Count > 0
            ? await invDb.WarehouseBins.AsNoTracking()
                .Where(x => binIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Code, ct)
            : new Dictionary<Guid, string>();

        static string TrAd(Dictionary<string, string>? i18n) =>
            i18n is null ? "" : i18n.TryGetValue("tr", out var ad) ? ad : i18n.Values.FirstOrDefault() ?? "";

        var items = rows.Select(r =>
        {
            displays.TryGetValue(r.VariantId, out var d);
            return new StockAdminRowDto(
                r.Id, r.VariantId,
                d?.ProductCode ?? "—",
                d is null ? "(katalogda yok)" : TrAd(d.ProductNameI18n),
                d?.OptionsText,
                d?.ImageUrl,
                warehouses.TryGetValue(r.WarehouseId, out var wn) ? TrAd(wn) : "—",
                r.SectionId.HasValue ? sections.GetValueOrDefault(r.SectionId.Value) : null,
                r.BinId.HasValue ? bins.GetValueOrDefault(r.BinId.Value) : null,
                r.Quantity, r.ReservedQuantity, r.Quantity - r.ReservedQuantity);
        }).ToList();

        return Result.Success(new PagedResult<StockAdminRowDto>(items, total, request.Page, request.PageSize));
    }
}
