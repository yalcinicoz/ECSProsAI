using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Shared.Kernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Infrastructure.Persistence;

public class FulfillmentDbContext : DbContext
{
    public FulfillmentDbContext(DbContextOptions<FulfillmentDbContext> options) : base(options) { }

    public DbSet<PickingPlan> PickingPlans => Set<PickingPlan>();
    public DbSet<SortingBin> SortingBins => Set<SortingBin>();
    public DbSet<PackingStation> PackingStations => Set<PackingStation>();
    public DbSet<Package> Packages => Set<Package>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("fulfillment");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FulfillmentDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
