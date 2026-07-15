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
    int PageSize = 30,
    // İkincil filtre (arama sonucundan türetilen facet seçimleri)
    Guid? VariantId = null,
    Guid? SectionId = null,
    Guid? BinId = null) : IRequest<Result<PagedResult<StockAdminRowDto>>>;

/// <summary>Arama sonucundan türetilen ikincil filtre seçenekleri: bulunan ürünün varyantları
/// ve stoğun bulunduğu depo/kısım/raflar (sayaçlı). ParentId: kısım→depo, raf→kısım
/// (frontend kademeli daraltma için).</summary>
public record GetStocksAdminFacetsQuery(
    string? Search, Guid? WarehouseId, bool AvailableOnly,
    // Mevcut ikincil seçimler: her facet boyutu DİĞER seçimlerle daraltılır (klasik facet
    // kuralı) — yoksa sayaçlar listeyle tutmaz (örn. varyant seçiliyken raf sayıları tüm
    // varyantların raflarını gösterir, seçilen raf boş liste döndürebilirdi).
    Guid? VariantId = null, Guid? SectionId = null, Guid? BinId = null) : IRequest<Result<StockAdminFacetsDto>>;

public record StockFacetOption(Guid Id, string Label, int Count, Guid? ParentId = null);

public record StockAdminFacetsDto(
    List<StockFacetOption> Variants,
    List<StockFacetOption> Warehouses,
    List<StockFacetOption> Sections,
    List<StockFacetOption> Bins);

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
            var variantIds = await SearchVariantIdsAsync(catDb, request.Search, ct);
            if (variantIds.Count == 0)
                return Result.Success(new PagedResult<StockAdminRowDto>([], 0, request.Page, request.PageSize));
            query = query.Where(st => variantIds.Contains(st.VariantId));
        }

        // İkincil filtre (arama facet'lerinden seçilenler)
        if (request.VariantId.HasValue) query = query.Where(st => st.VariantId == request.VariantId);
        if (request.SectionId.HasValue) query = query.Where(st => st.SectionId == request.SectionId);
        if (request.BinId.HasValue) query = query.Where(st => st.BinId == request.BinId);

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

    /// <summary>Katalogda ürün kodu / Türkçe ad / barkod / SKU eşleşen varyant id kümesi
    /// (liste + facet sorguları aynı eşleşme kuralını kullanır).</summary>
    internal static async Task<List<Guid>> SearchVariantIdsAsync(ICatalogDbContext catDb, string search, CancellationToken ct)
    {
        var s = search.Trim().ToLower();
        return await catDb.ProductVariants.AsNoTracking()
            .Where(v => v.Product.Code.ToLower().Contains(s)
                     || (v.Barcode != null && v.Barcode.ToLower() == s)
                     || v.Sku.ToLower() == s
                     || Catalog.Application.Helpers.PgJsonFunctions.JsonText(v.Product.NameI18n, "tr")!.ToLower().Contains(s))
            .Select(v => v.Id)
            .Take(20000)
            .ToListAsync(ct);
    }
}

/// <summary>Arama sonucunun ikincil filtre seçenekleri: varyantlar + stoğun bulunduğu
/// depo/kısım/raflar (satır sayaçlı). Yalnız arama varken anlamlı — aramasız boş döner
/// (tüm kataloğun raf facet'i gereksiz/pahalı).</summary>
public class GetStocksAdminFacetsHandler(
    IInventoryDbContext invDb,
    ICatalogDbContext catDb,
    IProductService productService)
    : IRequestHandler<GetStocksAdminFacetsQuery, Result<StockAdminFacetsDto>>
{
    public async Task<Result<StockAdminFacetsDto>> Handle(GetStocksAdminFacetsQuery request, CancellationToken ct)
    {
        var bos = new StockAdminFacetsDto([], [], [], []);
        if (string.IsNullOrWhiteSpace(request.Search))
            return Result.Success(bos);

        var variantIds = await GetStocksAdminHandler.SearchVariantIdsAsync(catDb, request.Search, ct);
        if (variantIds.Count == 0)
            return Result.Success(bos);

        var query = invDb.Stocks.AsNoTracking().Where(st => variantIds.Contains(st.VariantId));
        if (request.WarehouseId.HasValue) query = query.Where(st => st.WarehouseId == request.WarehouseId);
        if (request.AvailableOnly) query = query.Where(st => st.Quantity > st.ReservedQuantity);

        var rows = await query
            .Select(st => new { st.VariantId, st.WarehouseId, st.SectionId, st.BinId })
            .ToListAsync(ct);
        if (rows.Count == 0)
            return Result.Success(bos);

        static string TrAd(Dictionary<string, string>? i18n) =>
            i18n is null ? "" : i18n.TryGetValue("tr", out var ad) ? ad : i18n.Values.FirstOrDefault() ?? "";

        // Her boyut, KENDİSİ HARİÇ diğer seçimlerle daraltılmış satır kümesinden hesaplanır —
        // sayaçlar hep görünen listeyle tutarlı kalır.
        var forVariants = rows.Where(r =>
            (!request.SectionId.HasValue || r.SectionId == request.SectionId)
            && (!request.BinId.HasValue || r.BinId == request.BinId)).ToList();
        var forSections = rows.Where(r =>
            (!request.VariantId.HasValue || r.VariantId == request.VariantId)
            && (!request.BinId.HasValue || r.BinId == request.BinId)).ToList();
        var forBins = rows.Where(r =>
            (!request.VariantId.HasValue || r.VariantId == request.VariantId)
            && (!request.SectionId.HasValue || r.SectionId == request.SectionId)).ToList();

        // Varyantlar (en çok satırlı 200) — etiket: ürün kodu · seçenekler
        var variantGroups = forVariants.GroupBy(r => r.VariantId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count).Take(200).ToList();
        var displays = await productService.GetVariantDisplayAsync(variantGroups.Select(g => g.Id).ToList(), ct);
        var variants = variantGroups.Select(g =>
        {
            displays.TryGetValue(g.Id, out var d);
            var label = d is null ? g.Id.ToString()[..8]
                : string.IsNullOrEmpty(d.OptionsText) ? d.ProductCode : $"{d.ProductCode} · {d.OptionsText}";
            return new StockFacetOption(g.Id, label, g.Count);
        }).OrderBy(v => v.Label).ToList();

        // Depolar (tüm ikincil seçimlerle daraltılmış)
        var forWarehouses = rows.Where(r =>
            (!request.VariantId.HasValue || r.VariantId == request.VariantId)
            && (!request.SectionId.HasValue || r.SectionId == request.SectionId)
            && (!request.BinId.HasValue || r.BinId == request.BinId)).ToList();
        var whIds = forWarehouses.Select(r => r.WarehouseId).Distinct().ToList();
        var whNames = await invDb.Warehouses.AsNoTracking()
            .Where(w => whIds.Contains(w.Id))
            .Select(w => new { w.Id, w.NameI18n })
            .ToDictionaryAsync(w => w.Id, w => w.NameI18n, ct);
        var warehouses = forWarehouses.GroupBy(r => r.WarehouseId)
            .Select(g => new StockFacetOption(g.Key,
                whNames.TryGetValue(g.Key, out var n) ? TrAd(n) : "—", g.Count()))
            .OrderBy(w => w.Label).ToList();

        // Kısımlar (parent = depo)
        var secIds = forSections.Where(r => r.SectionId.HasValue).Select(r => r.SectionId!.Value).Distinct().ToList();
        var secInfo = secIds.Count > 0
            ? await invDb.WarehouseSections.AsNoTracking()
                .Where(x => secIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Name, x.WarehouseId })
                .ToDictionaryAsync(x => x.Id, ct)
            : null;
        var sections = forSections.Where(r => r.SectionId.HasValue)
            .GroupBy(r => r.SectionId!.Value)
            .Select(g => new StockFacetOption(g.Key,
                secInfo != null && secInfo.TryGetValue(g.Key, out var si) ? si.Name : "—",
                g.Count(),
                secInfo != null && secInfo.TryGetValue(g.Key, out var si2) ? si2.WarehouseId : null))
            .OrderBy(x => x.Label).ToList();

        // Raflar (parent = kısım; en çok satırlı 300)
        var binGroups = forBins.Where(r => r.BinId.HasValue)
            .GroupBy(r => r.BinId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count).Take(300).ToList();
        var binIds = binGroups.Select(g => g.Id).ToList();
        var binInfo = binIds.Count > 0
            ? await invDb.WarehouseBins.AsNoTracking()
                .Where(x => binIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Code, x.SectionId })
                .ToDictionaryAsync(x => x.Id, ct)
            : null;
        var bins = binGroups
            .Select(g => new StockFacetOption(g.Id,
                binInfo != null && binInfo.TryGetValue(g.Id, out var bi) ? bi.Code : "—",
                g.Count,
                binInfo != null && binInfo.TryGetValue(g.Id, out var bi2) ? bi2.SectionId : null))
            .OrderBy(x => x.Label).ToList();

        return Result.Success(new StockAdminFacetsDto(variants, warehouses, sections, bins));
    }
}
