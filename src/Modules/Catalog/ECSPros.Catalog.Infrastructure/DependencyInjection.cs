using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Infrastructure.Persistence;
using ECSPros.Catalog.Infrastructure.Services;
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
        services.AddScoped<IImageUploadService, LocalDiskImageUploadService>();
        services.AddScoped<IVideoUploadService, LocalDiskVideoUploadService>();

        return services;
    }
}
