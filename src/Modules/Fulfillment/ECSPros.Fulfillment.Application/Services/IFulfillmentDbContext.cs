using ECSPros.Fulfillment.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Services;

public interface IFulfillmentDbContext
{
    DbSet<PickingPlan> PickingPlans { get; }
    DbSet<SortingBin> SortingBins { get; }
    DbSet<PackingStation> PackingStations { get; }
    DbSet<Package> Packages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
