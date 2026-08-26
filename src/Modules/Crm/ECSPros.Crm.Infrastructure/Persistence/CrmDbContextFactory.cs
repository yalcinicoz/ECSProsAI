using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECSPros.Crm.Infrastructure.Persistence;

public class CrmDbContextFactory : IDesignTimeDbContextFactory<CrmDbContext>
{
    public CrmDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CrmDbContext>();
        optionsBuilder.UseNpgsql(
            ECSPros.Shared.Kernel.DesignTime.DesignTimeConnection.Resolve(),
            o => o.MigrationsHistoryTable("__ef_migrations_crm", "crm"));

        return new CrmDbContext(optionsBuilder.Options);
    }
}
