using ECSPros.Inventory.Infrastructure.Persistence;
using ECSPros.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Infrastructure.Services;

public class InventoryStockService(InventoryDbContext db) : IStockService
{
    public async Task<int> GetAvailableStockAsync(Guid variantId, Guid? warehouseId = null, CancellationToken ct = default)
    {
        var query = db.Stocks.Where(s => s.VariantId == variantId);
        if (warehouseId.HasValue) query = query.Where(s => s.WarehouseId == warehouseId.Value);
        return await query.SumAsync(s => s.Quantity - s.ReservedQuantity, ct);
    }

    public async Task<bool> HasSufficientStockAsync(Guid variantId, int quantity, Guid? warehouseId = null, CancellationToken ct = default)
    {
        var available = await GetAvailableStockAsync(variantId, warehouseId, ct);
        return available >= quantity;
    }

    public async Task<HashSet<Guid>> GetInStockVariantIdsAsync(CancellationToken ct = default)
    {
        var ids = await db.Stocks
            .Where(s => s.Quantity > s.ReservedQuantity)
            .Select(s => s.VariantId)
            .Distinct()
            .ToListAsync(ct);
        return ids.ToHashSet();
    }
}
