using ECSPros.Shared.Contracts;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Infrastructure.Persistence;
using ECSPros.Storefront.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ECSPros.Storefront.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddStorefrontInfrastructure(
        this IServiceCollection services, NpgsqlDataSource dataSource)
    {
        services.AddDbContext<StorefrontDbContext>(options =>
            options.UseNpgsql(dataSource,
                o => o.MigrationsHistoryTable("__ef_migrations_storefront", "storefront")));

        services.AddScoped<IStorefrontDbContext>(sp => sp.GetRequiredService<StorefrontDbContext>());
        services.AddScoped<IChannelPricingService, StorefrontChannelPricingService>();

        return services;
    }
}
