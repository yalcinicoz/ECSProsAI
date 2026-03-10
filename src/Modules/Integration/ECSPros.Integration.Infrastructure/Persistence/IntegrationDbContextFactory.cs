using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECSPros.Integration.Infrastructure.Persistence;

public class IntegrationDbContextFactory : IDesignTimeDbContextFactory<IntegrationDbContext>
{
    public IntegrationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IntegrationDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=ecommerce_db;Username=ecommerce;Password=EcsPros2025SecureDb!",
            o => o.MigrationsHistoryTable("__ef_migrations_integration", "integration"));

        return new IntegrationDbContext(optionsBuilder.Options);
    }
}
