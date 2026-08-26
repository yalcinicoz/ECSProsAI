using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECSPros.Integration.Infrastructure.Persistence;

public class IntegrationDbContextFactory : IDesignTimeDbContextFactory<IntegrationDbContext>
{
    public IntegrationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IntegrationDbContext>();
        optionsBuilder.UseNpgsql(
            ECSPros.Shared.Kernel.DesignTime.DesignTimeConnection.Resolve(),
            o => o.MigrationsHistoryTable("__ef_migrations_integration", "integration"));

        return new IntegrationDbContext(optionsBuilder.Options);
    }
}
