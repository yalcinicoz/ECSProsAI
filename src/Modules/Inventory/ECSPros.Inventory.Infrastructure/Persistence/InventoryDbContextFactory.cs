using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECSPros.Inventory.Infrastructure.Persistence;

public class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<InventoryDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=ecommerce_db;Username=ecommerce;Password=EcsPros2025SecureDb!",
            o => o.MigrationsHistoryTable("__ef_migrations_inventory", "inventory"));

        return new InventoryDbContext(optionsBuilder.Options);
    }
}
