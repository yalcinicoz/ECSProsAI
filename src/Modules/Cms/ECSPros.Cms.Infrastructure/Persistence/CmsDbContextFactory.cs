using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECSPros.Cms.Infrastructure.Persistence;

public class CmsDbContextFactory : IDesignTimeDbContextFactory<CmsDbContext>
{
    public CmsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CmsDbContext>();
        optionsBuilder.UseNpgsql(
            ECSPros.Shared.Kernel.DesignTime.DesignTimeConnection.Resolve(),
            o => o.MigrationsHistoryTable("__ef_migrations_cms", "cms"));

        return new CmsDbContext(optionsBuilder.Options);
    }
}
