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
                o => o.MigrationsHistoryTable("__ef_migrations_fulfillment", "fulfillment")));

        services.AddScoped<IFulfillmentDbContext>(sp => sp.GetRequiredService<FulfillmentDbContext>());

        return services;
    }
}
