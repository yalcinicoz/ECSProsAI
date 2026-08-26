using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECSPros.Order.Infrastructure.Persistence;

public class OrderDbContextFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    public OrderDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrderDbContext>();
        optionsBuilder.UseNpgsql(
            ECSPros.Shared.Kernel.DesignTime.DesignTimeConnection.Resolve(),
            o => o.MigrationsHistoryTable("__ef_migrations_order", "order"));

        return new OrderDbContext(optionsBuilder.Options);
    }
}
