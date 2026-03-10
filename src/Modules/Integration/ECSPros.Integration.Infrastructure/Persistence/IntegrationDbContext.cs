using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Integration.Infrastructure.Persistence;

public class IntegrationDbContext(DbContextOptions<IntegrationDbContext> options)
    : DbContext(options), IIntegrationDbContext
{
    public DbSet<IntegrationLog> IntegrationLogs => Set<IntegrationLog>();
    public DbSet<MarketplaceProduct> MarketplaceProducts => Set<MarketplaceProduct>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("integration");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IntegrationDbContext).Assembly);
    }
}
