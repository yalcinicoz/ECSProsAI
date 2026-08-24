using ECSPros.Inventory.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Queries.LookupBins;

/// <summary>Birim (raf) arama: barkod TAM → kod/ad içeren; kısım+depo adlarıyla (en çok 10).</summary>
public record LookupBinsQuery(string Term, Guid? WarehouseId = null) : IRequest<Result<List<BinLookupDto>>>;

public record BinLookupDto(Guid BinId, string Code, string? Barcode, string Section, string Warehouse, Guid WarehouseId, bool SellableOnline, bool Exact);

public class LookupBinsQueryHandler(IInventoryDbContext invDb) : IRequestHandler<LookupBinsQuery, Result<List<BinLookupDto>>>
{
    public async Task<Result<List<BinLookupDto>>> Handle(LookupBinsQuery request, CancellationToken ct)
    {
        var term = (request.Term ?? "").Trim();
        if (term.Length < 2) return Result.Success(new List<BinLookupDto>());
        var lower = term.ToLower();

        var baseQ = from b in invDb.WarehouseBins
                    join sec in invDb.WarehouseSections on b.SectionId equals sec.Id
                    join w in invDb.Warehouses on sec.WarehouseId equals w.Id
                    where b.IsActive && w.IsActive
                    select new { b, sec, w };
        if (request.WarehouseId.HasValue) baseQ = baseQ.Where(x => x.w.Id == request.WarehouseId.Value);

        var exact = await baseQ.Where(x => x.b.Barcode == term || x.b.Code == term)
            .Select(x => new BinLookupDto(x.b.Id, x.b.Code, x.b.Barcode,
                x.sec.Name != "" ? x.sec.Name : x.sec.Code,
                x.w.NameI18n["tr"] ?? x.w.Code,
                x.w.Id, x.sec.IsSellableOnline, true))
            .Take(10).ToListAsync(ct);
        if (exact.Count > 0) return Result.Success(exact);

        var partial = await baseQ.Where(x => x.b.Code.ToLower().Contains(lower) || (x.b.Barcode ?? "").Contains(term))
            .OrderBy(x => x.b.Code)
            .Select(x => new BinLookupDto(x.b.Id, x.b.Code, x.b.Barcode,
                x.sec.Name != "" ? x.sec.Name : x.sec.Code,
                x.w.NameI18n["tr"] ?? x.w.Code,
                x.w.Id, x.sec.IsSellableOnline, false))
            .Take(10).ToListAsync(ct);
        return Result.Success(partial);
    }
}
