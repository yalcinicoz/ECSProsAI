using ECSPros.Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Services;

public interface IInventoryDbContext
{
    DbSet<Warehouse> Warehouses { get; }
    DbSet<WarehouseLocation> WarehouseLocations { get; }
    DbSet<Stock> Stocks { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<StockReservation> StockReservations { get; }
    DbSet<TransferRequest> TransferRequests { get; }
    DbSet<TransferRequestItem> TransferRequestItems { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
