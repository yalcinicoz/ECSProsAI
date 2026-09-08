using ECSPros.Api.Handlers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Grid;

/// <summary>
/// Admin stok listesi DataGrid şeması (plan F4). Cross-module (Inventory + Catalog) olduğundan Api katmanında
/// (GetStocksAdminHandler deseni). Arama/depo/ikincil filtreler adlandırılmış parametre kalır; grid beyaz listesi sayısal/durum alanları.
/// </summary>
public static class StockGrid
{
    public static readonly GridSchema<Stock> Schema = new GridSchema<Stock>()
        .Number("quantity", s => s.Quantity)
        .Number("reserved", s => s.ReservedQuantity)
        .Number("available", s => s.Quantity - s.ReservedQuantity)
        .Bool("inStock", s => s.Quantity > s.ReservedQuantity)
        .Enum("stockType", s => s.StockType, new[] { "physical", "virtual" })
        .Date("createdAt", s => s.CreatedAt)
        .Date("updatedAt", s => s.UpdatedAt)
        .Guid("warehouseId", s => s.WarehouseId)
        .Guid("sectionId", s => s.SectionId)
        .Guid("binId", s => s.BinId)
        .Guid("variantId", s => s.VariantId)
        .Sort("quantity", s => s.Quantity)
        .Sort("reserved", s => s.ReservedQuantity)
        .Sort("available", s => s.Quantity - s.ReservedQuantity)
        .Sort("updatedAt", s => s.UpdatedAt)
        .TieBreaker(s => s.Id);

    /// <summary>Grid sıralaması varsa o; yoksa mevcut varyant + kısım + raf sırası (aynı varyantın rafları alt alta).</summary>
    public static IQueryable<Stock> ApplySortCompat(IQueryable<Stock> q, GridRequest? grid)
        => grid?.Sort is { Length: > 0 } ? Schema.ApplySort(q, grid)
            : q.OrderBy(st => st.VariantId).ThenBy(st => st.SectionId).ThenBy(st => st.BinId);
}

internal record StockRaw(Guid VariantId, Guid WarehouseId, Guid? SectionId, Guid? BinId, int Quantity, int ReservedQuantity, string StockType, DateTime? UpdatedAt);

public record StockListFilters(string? Search = null, Guid? WarehouseId = null, bool AvailableOnly = false,
    Guid? VariantId = null, Guid? SectionId = null, Guid? BinId = null);

public record StockExportRow(string ProductCode, string ProductName, string? Options, string? Barcode, string WarehouseName, string? SectionName,
    string? BinCode, int Quantity, int ReservedQuantity, int AvailableQuantity, string StockType, DateTime? UpdatedAt);

public record ExportStocksQuery(StockListFilters Filters, GridRequest Grid, int MaxRows) : IRequest<Result<GridExportSource<StockExportRow>>>;

/// <summary>Export: sayfalamasız akış; ürün adı/kodu (Catalog) ve depo/kısım/raf adları 500'lük partilerle zenginleştirilir (bellek sabit).</summary>
public class ExportStocksQueryHandler(IInventoryDbContext invDb, ICatalogDbContext catDb, IProductService productService)
    : IRequestHandler<ExportStocksQuery, Result<GridExportSource<StockExportRow>>>
{
    public async Task<Result<GridExportSource<StockExportRow>>> Handle(ExportStocksQuery r, CancellationToken ct)
    {
        var f = r.Filters;
        var query = invDb.Stocks.AsNoTracking().AsQueryable();
        if (f.WarehouseId.HasValue) query = query.Where(s => s.WarehouseId == f.WarehouseId);
        if (f.AvailableOnly) query = query.Where(s => s.Quantity > s.ReservedQuantity);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var variantIds = await GetStocksAdminHandler.SearchVariantIdsAsync(catDb, f.Search, ct);
            if (variantIds.Count == 0) return Result.Success(new GridExportSource<StockExportRow>(0, Array.Empty<StockExportRow>().AsQueryable()));
            query = query.Where(st => variantIds.Contains(st.VariantId));
        }
        if (f.VariantId.HasValue) query = query.Where(st => st.VariantId == f.VariantId);
        if (f.SectionId.HasValue) query = query.Where(st => st.SectionId == f.SectionId);
        if (f.BinId.HasValue) query = query.Where(st => st.BinId == f.BinId);
        query = StockGrid.Schema.ApplyFilters(query, r.Grid);

        var count = await query.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<StockExportRow>>($"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");

        // depo/kısım/raf sözlükleri (küçük) baştan
        static string TrAd(Dictionary<string, string>? i18n) => i18n is null ? "" : i18n.TryGetValue("tr", out var ad) ? ad : i18n.Values.FirstOrDefault() ?? "";
        var warehouses = (await invDb.Warehouses.AsNoTracking().Select(w => new { w.Id, w.NameI18n }).ToListAsync(ct)).ToDictionary(w => w.Id, w => TrAd(w.NameI18n));
        var sections = await invDb.WarehouseSections.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var bins = await invDb.WarehouseBins.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code, ct);

        var ordered = StockGrid.ApplySortCompat(query, r.Grid)
            .Select(st => new StockRaw(st.VariantId, st.WarehouseId, st.SectionId, st.BinId, st.Quantity, st.ReservedQuantity, st.StockType, st.UpdatedAt));

        IEnumerable<StockExportRow> Rows()
        {
            var buffer = new List<StockRaw>(500);
            foreach (var st in ordered.AsEnumerable())
            {
                buffer.Add(st);
                if (buffer.Count == 500) { foreach (var row in Flush(buffer)) yield return row; buffer.Clear(); }
            }
            foreach (var row in Flush(buffer)) yield return row;
        }

        IEnumerable<StockExportRow> Flush(List<StockRaw> buffer)
        {
            if (buffer.Count == 0) yield break;
            var ids = buffer.Select(b => b.VariantId).Distinct().ToList();
            var displays = productService.GetVariantDisplayAsync(ids, ct).GetAwaiter().GetResult();
            var barcodes = catDb.ProductVariants.AsNoTracking().Where(v => ids.Contains(v.Id)).Select(v => new { v.Id, v.Barcode }).ToDictionary(v => v.Id, v => v.Barcode);
            foreach (var b in buffer)
            {
                displays.TryGetValue(b.VariantId, out var d);
                yield return new StockExportRow(
                    d?.ProductCode ?? "—", d is null ? "(katalogda yok)" : TrAd(d.ProductNameI18n), d?.OptionsText, barcodes.GetValueOrDefault(b.VariantId),
                    warehouses.GetValueOrDefault(b.WarehouseId, "—"), b.SectionId.HasValue ? sections.GetValueOrDefault(b.SectionId.Value) : null,
                    b.BinId.HasValue ? bins.GetValueOrDefault(b.BinId.Value) : null,
                    b.Quantity, b.ReservedQuantity, b.Quantity - b.ReservedQuantity, b.StockType, b.UpdatedAt);
            }
        }

        return Result.Success(new GridExportSource<StockExportRow>(count, Rows().AsQueryable()));
    }
}
