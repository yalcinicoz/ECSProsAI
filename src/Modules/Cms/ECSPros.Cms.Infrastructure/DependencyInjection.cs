using ECSPros.Cms.Application.Services;
using ECSPros.Cms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ECSPros.Cms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCmsInfrastructure(this IServiceCollection services, NpgsqlDataSource dataSource)
    {
        services.AddDbContext<CmsDbContext>(options =>
            options.UseNpgsql(dataSource,
                o => o.MigrationsHistoryTable("__ef_migrations_cms", "cms")));

        services.AddScoped<ICmsDbContext>(sp => sp.GetRequiredService<CmsDbContext>());

        return services;
    }
}
