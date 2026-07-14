using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Infrastructure.Persistence;
using ECSPros.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Infrastructure.Services;

// Cutover (2026-07-14): "online satılabilir stok" artık yalnız satışa-AÇIK kısımların (aktif depo)
// serbest stoğu (StockOps). warehouseId verilirse o deponun tüm rafları toplanır (kısım filtresi yok).
public class InventoryStockService(InventoryDbContext db) : IStockService
{
    public async Task<int> GetAvailableStockAsync(Guid variantId, Guid? warehouseId = null, CancellationToken ct = default)
    {
        if (warehouseId is null)
            return await StockOps.AvailableOnlineAsync(db, variantId, ct);

        return await db.Stocks
            .Where(s => s.VariantId == variantId && s.WarehouseId == warehouseId.Value)
            .SumAsync(s => (int?)(s.Quantity - s.ReservedQuantity), ct) ?? 0;
    }

    public async Task<bool> HasSufficientStockAsync(Guid variantId, int quantity, Guid? warehouseId = null, CancellationToken ct = default)
    {
        var available = await GetAvailableStockAsync(variantId, warehouseId, ct);
        return available >= quantity;
    }

    public Task<Dictionary<Guid, int>> GetVariantAvailableStocksAsync(CancellationToken ct = default)
        => StockOps.AllAvailableOnlineAsync(db, ct);
}
