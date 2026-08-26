using ECSPros.Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Services;

public interface IInventoryDbContext
{
    DbSet<Warehouse> Warehouses { get; }
    DbSet<WarehouseLocation> WarehouseLocations { get; }
    DbSet<WarehouseSection> WarehouseSections { get; }
    DbSet<WarehouseBin> WarehouseBins { get; }
    DbSet<Stock> Stocks { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<StockReservation> StockReservations { get; }
    DbSet<TransferRequest> TransferRequests { get; }
    DbSet<TransferRequestItem> TransferRequestItems { get; }

    /// <summary>Faz 0 (StockTx): açık transaction + advisory kilit için — DbContext otomatik karşılar.</summary>
    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }
    Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker ChangeTracker { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
