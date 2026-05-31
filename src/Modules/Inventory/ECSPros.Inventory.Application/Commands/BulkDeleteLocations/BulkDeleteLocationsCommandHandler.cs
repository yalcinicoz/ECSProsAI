using ECSPros.Inventory.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Commands.BulkDeleteLocations;

public class BulkDeleteLocationsCommandHandler
    : IRequestHandler<BulkDeleteLocationsCommand, Result<BulkDeleteLocationsResult>>
{
    private readonly IInventoryDbContext _db;

    public BulkDeleteLocationsCommandHandler(IInventoryDbContext db) => _db = db;

    public async Task<Result<BulkDeleteLocationsResult>> Handle(
        BulkDeleteLocationsCommand request, CancellationToken ct)
    {
        // Kod aralığındaki lokasyonları bul (lexicographic karşılaştırma)
        var locations = await _db.WarehouseLocations
            .Where(l => l.WarehouseId == request.WarehouseId
                     && string.Compare(l.Code, request.StartCode) >= 0
                     && string.Compare(l.Code, request.EndCode) <= 0)
            .ToListAsync(ct);

        if (locations.Count == 0)
            return Result.Failure<BulkDeleteLocationsResult>("Belirtilen kod aralığında lokasyon bulunamadı.");

        var locationIds = locations.Select(l => l.Id).ToList();

        // Dolu lokasyonları kontrol et (Stock.Quantity > 0)
        var occupiedStocks = await _db.Stocks
            .Where(s => s.LocationId.HasValue
                     && locationIds.Contains(s.LocationId.Value)
                     && s.Quantity > 0)
            .Select(s => new { s.LocationId, s.Quantity })
            .ToListAsync(ct);

        if (occupiedStocks.Count > 0)
        {
            // Lokasyon kodlarıyla eşleştir
            var locationCodeMap = locations.ToDictionary(l => l.Id, l => l.Code);
            var blocked = occupiedStocks
                .GroupBy(s => s.LocationId!.Value)
                .Select(g => new BlockedLocationInfo(
                    locationCodeMap.GetValueOrDefault(g.Key, g.Key.ToString()),
                    g.Sum(s => s.Quantity)))
                .OrderBy(b => b.Code)
                .ToList();

            return Result.Success(new BulkDeleteLocationsResult(0, Blocked: true, blocked));
        }

        // Soft delete
        var now = DateTime.UtcNow;
        foreach (var loc in locations)
        {
            loc.IsDeleted  = true;
            loc.DeletedAt  = now;
        }
        await _db.SaveChangesAsync(ct);

        return Result.Success(new BulkDeleteLocationsResult(locations.Count, Blocked: false, []));
    }
}
