using ECSPros.Fulfillment.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Services;

public interface IFulfillmentDbContext
{
    DbSet<PickingPlan> PickingPlans { get; }
    DbSet<SortingBin> SortingBins { get; }
    DbSet<PackingStation> PackingStations { get; }
    DbSet<Package> Packages { get; }
    DbSet<PackageItem> PackageItems { get; }
    DbSet<PackageNumberSeries> PackageNumberSeries { get; }
    DbSet<PackageCodeHistory> PackageCodeHistories { get; }
    DbSet<PickingPlanLine> PickingPlanLines { get; }
    DbSet<OperationProfile> OperationProfiles { get; }
    DbSet<OperationLog> OperationLogs { get; }
    DbSet<SortingBox> SortingBoxes { get; }
    DbSet<PackingDesk> PackingDesks { get; }
    DbSet<CargoNotifyOutbox> CargoNotifyOutbox { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
