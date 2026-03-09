using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ECSPros.Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogInfrastructure(this IServiceCollection services, NpgsqlDataSource dataSource)
    {
        services.AddDbContext<CatalogDbContext>(options =>
            options.UseNpgsql(dataSource,
                o => o.MigrationsHistoryTable("__ef_migrations_catalog", "catalog")));

        services.AddScoped<ICatalogDbContext>(sp => sp.GetRequiredService<CatalogDbContext>());

        return services;
    }
}
