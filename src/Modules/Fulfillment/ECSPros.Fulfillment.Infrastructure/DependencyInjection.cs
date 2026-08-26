using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ECSPros.Fulfillment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFulfillmentInfrastructure(this IServiceCollection services, NpgsqlDataSource dataSource)
    {
        services.AddDbContext<FulfillmentDbContext>(options =>
            options.UseNpgsql(dataSource,
                o =>
                {
                    o.MigrationsHistoryTable("__ef_migrations_fulfillment", "fulfillment");
                    o.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);   // Faz 1: geçici DB hatasında otomatik yeniden dene
                }));

        services.AddScoped<IFulfillmentDbContext>(sp => sp.GetRequiredService<FulfillmentDbContext>());
        services.AddScoped<IPackageNumberService, Services.PackageNumberService>();
        services.AddScoped<IOrderPackagingReader, Services.OrderPackagingReader>();
        services.AddScoped<IOrderPickingReader, Services.OrderPickingReader>(); // OP1: görev adayları

        return services;
    }
}
