using ECSPros.Inventory.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Shelf;

/// <summary>Göz kalemi — ürün görünümü Catalog'dan (IProductService) zenginleştirilir; varyant silinmişse ad boş kalır.</summary>
public record BinItemDto(Guid VariantId, int Quantity, int ReservedQuantity, int AvailableQuantity,
    string? ProductCode, string? ProductName, string? OptionsText, string? ImageUrl, string? Sku);

public record BinContentsDto(
    Guid BinId, string BinCode, string BinBarcode, string? BinName, bool BinActive,
    Guid SectionId, string SectionCode, string SectionName, bool SectionSellableOnline,
    Guid WarehouseId, string WarehouseCode, string WarehouseName,
    int TotalQuantity, int TotalReserved, List<BinItemDto> Items);

/// <summary>R1 Raf İçeriği: göz barkodu ya da Id ile.</summary>
public record GetBinContentsQuery(string? Barcode, Guid? BinId) : IRequest<Result<BinContentsDto>>;

public class GetBinContentsQueryHandler(IInventoryDbContext db, IProductService products)
    : IRequestHandler<GetBinContentsQuery, Result<BinContentsDto>>
{
    public async Task<Result<BinContentsDto>> Handle(GetBinContentsQuery request, CancellationToken ct)
    {
        var bin = await ShelfOps.FindBinAsync(db, request.BinId, request.Barcode, ct);
        if (bin is null) return Result.Failure<BinContentsDto>("Göz bulunamadı (barkod tanınmadı).");

        var stoklar = await db.Stocks.AsNoTracking()
            .Where(s => s.BinId == bin.Bin.Id && (s.Quantity > 0 || s.ReservedQuantity > 0))
            .OrderByDescending(s => s.Quantity)
            .Select(s => new { s.VariantId, s.Quantity, s.ReservedQuantity })
            .ToListAsync(ct);
        var gorunum = await GorunumAsync(products, stoklar.Select(s => s.VariantId), ct);

        var items = stoklar.Select(s =>
        {
            gorunum.TryGetValue(s.VariantId, out var g);
            return new BinItemDto(s.VariantId, s.Quantity, s.ReservedQuantity, s.Quantity - s.ReservedQuantity,
                g?.ProductCode, g?.ProductNameI18n.GetValueOrDefault("tr") ?? g?.ProductNameI18n.Values.FirstOrDefault(),
                g?.OptionsText, g?.ImageUrl, g?.Sku);
        }).ToList();

        return Result.Success(new BinContentsDto(
            bin.Bin.Id, bin.Bin.Code, bin.Bin.Barcode, bin.Bin.Name, bin.Bin.IsActive,
            bin.Section.Id, bin.Section.Code, bin.Section.Name, bin.Section.IsSellableOnline,
            bin.Warehouse.Id, bin.Warehouse.Code, bin.Warehouse.NameI18n.GetValueOrDefault("tr") ?? bin.Warehouse.Code,
            items.Sum(i => i.Quantity), items.Sum(i => i.ReservedQuantity), items));
    }

    internal static async Task<Dictionary<Guid, VariantDisplayInfo>> GorunumAsync(IProductService products, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var liste = ids.Distinct().ToList();
        if (liste.Count == 0) return new();
        try { return await products.GetVariantDisplayAsync(liste, ct); }
        catch { return new(); }   // zenginleştirme isteğe bağlı — katalog erişilemezse ham liste döner
    }
}

public record VariantBinDto(Guid WarehouseId, string WarehouseCode, Guid SectionId, string SectionCode, bool SectionSellableOnline,
    Guid BinId, string BinCode, string BinBarcode, int Quantity, int ReservedQuantity);

/// <summary>R1: bir ürün hangi gözlerde (depo filtresi isteğe bağlı).</summary>
public record GetVariantBinsQuery(Guid VariantId, Guid? WarehouseId) : IRequest<Result<List<VariantBinDto>>>;

public class GetVariantBinsQueryHandler(IInventoryDbContext db) : IRequestHandler<GetVariantBinsQuery, Result<List<VariantBinDto>>>
{
    public async Task<Result<List<VariantBinDto>>> Handle(GetVariantBinsQuery request, CancellationToken ct)
    {
        var q = from s in db.Stocks.AsNoTracking()
                join b in db.WarehouseBins on s.BinId equals b.Id
                join sec in db.WarehouseSections on b.SectionId equals sec.Id
                join w in db.Warehouses on sec.WarehouseId equals w.Id
                where s.VariantId == request.VariantId && (s.Quantity > 0 || s.ReservedQuantity > 0)
                select new VariantBinDto(w.Id, w.Code, sec.Id, sec.Code, sec.IsSellableOnline, b.Id, b.Code, b.Barcode, s.Quantity, s.ReservedQuantity);
        if (request.WarehouseId.HasValue) q = q.Where(x => x.WarehouseId == request.WarehouseId.Value);
        return Result.Success(await q.OrderBy(x => x.WarehouseCode).ThenBy(x => x.SectionCode).ThenBy(x => x.BinCode).ToListAsync(ct));
    }
}
