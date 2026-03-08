using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECSPros.Core.Infrastructure.Persistence;

public class CoreDbContextFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    public CoreDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CoreDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=ecommerce_db;Username=ecommerce;Password=EcsPros2025SecureDb!",
            o => o.MigrationsHistoryTable("__ef_migrations_core", "core"));

        return new CoreDbContext(optionsBuilder.Options);
    }
}
