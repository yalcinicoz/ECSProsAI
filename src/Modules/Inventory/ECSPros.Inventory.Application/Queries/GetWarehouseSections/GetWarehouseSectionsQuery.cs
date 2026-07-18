using ECSPros.Inventory.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Queries.GetWarehouseSections;

/// <summary>Depo detayı için üçlü yapının orta+alt katmanı: Kısımlar ve birimleri, stok özetleriyle.</summary>
public record GetWarehouseSectionsQuery(Guid WarehouseId) : IRequest<Result<List<WarehouseSectionDto>>>;

public record WarehouseBinDto(
    Guid Id, string Code, string Barcode, string? Name,
    int PickingOrder, bool IsActive, int StockRowCount, int TotalQuantity);

public record WarehouseSectionDto(
    Guid Id, string Code, string Name, bool IsSellableOnline,
    int PickingOrder, bool IsActive, int SortOrder,
    int StockRowCount, int TotalQuantity,
    List<WarehouseBinDto> Bins);

public class GetWarehouseSectionsQueryHandler
    : IRequestHandler<GetWarehouseSectionsQuery, Result<List<WarehouseSectionDto>>>
{
    private readonly IInventoryDbContext _db;
    public GetWarehouseSectionsQueryHandler(IInventoryDbContext db) => _db = db;

    public async Task<Result<List<WarehouseSectionDto>>> Handle(GetWarehouseSectionsQuery r, CancellationToken ct)
    {
        var sections = await _db.WarehouseSections
            .Include(s => s.Bins.Where(b => !b.IsDeleted))
            .Where(s => s.WarehouseId == r.WarehouseId)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.PickingOrder).ThenBy(s => s.Code)
            .ToListAsync(ct);

        // Kısım/birim başına stok özeti (satır sayısı + toplam adet)
        var sectionIds = sections.Select(s => s.Id).ToList();
        var stockSummary = await _db.Stocks
            .Where(st => st.SectionId != null && sectionIds.Contains(st.SectionId.Value))
            .GroupBy(st => new { st.SectionId, st.BinId })
            .Select(g => new { g.Key.SectionId, g.Key.BinId, Rows = g.Count(), Qty = g.Sum(x => x.Quantity) })
            .ToListAsync(ct);

        var bySection = stockSummary.GroupBy(x => x.SectionId!.Value)
            .ToDictionary(g => g.Key, g => new { Rows = g.Sum(x => x.Rows), Qty = g.Sum(x => x.Qty) });
        var byBin = stockSummary.Where(x => x.BinId.HasValue)
            .ToDictionary(x => x.BinId!.Value, x => new { x.Rows, x.Qty });

        var dto = sections.Select(s => new WarehouseSectionDto(
            s.Id, s.Code, s.Name, s.IsSellableOnline, s.PickingOrder, s.IsActive, s.SortOrder,
            bySection.TryGetValue(s.Id, out var ss) ? ss.Rows : 0,
            bySection.TryGetValue(s.Id, out var ss2) ? ss2.Qty : 0,
            s.Bins.OrderBy(b => b.PickingOrder).ThenBy(b => b.Code).Select(b => new WarehouseBinDto(
                b.Id, b.Code, b.Barcode, b.Name, b.PickingOrder, b.IsActive,
                byBin.TryGetValue(b.Id, out var bs) ? bs.Rows : 0,
                byBin.TryGetValue(b.Id, out var bs2) ? bs2.Qty : 0)).ToList()
        )).ToList();

        return Result.Success(dto);
    }
}
