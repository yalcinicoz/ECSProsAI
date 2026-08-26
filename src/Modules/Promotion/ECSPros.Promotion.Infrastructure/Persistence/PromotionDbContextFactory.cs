using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECSPros.Promotion.Infrastructure.Persistence;

public class PromotionDbContextFactory : IDesignTimeDbContextFactory<PromotionDbContext>
{
    public PromotionDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PromotionDbContext>();
        optionsBuilder.UseNpgsql(
            ECSPros.Shared.Kernel.DesignTime.DesignTimeConnection.Resolve(),
            o => o.MigrationsHistoryTable("__ef_migrations_promotion", "promotion"));

        return new PromotionDbContext(optionsBuilder.Options);
    }
}
