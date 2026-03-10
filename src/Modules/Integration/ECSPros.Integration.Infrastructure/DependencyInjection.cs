using ECSPros.Integration.Application.Adapters;
using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Infrastructure.Adapters;
using ECSPros.Integration.Infrastructure.Adapters.Cargo;
using ECSPros.Integration.Infrastructure.Adapters.EInvoice;
using ECSPros.Integration.Infrastructure.Adapters.Marketplace;
using ECSPros.Integration.Infrastructure.Persistence;
using ECSPros.Integration.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ECSPros.Integration.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIntegrationInfrastructure(
        this IServiceCollection services, NpgsqlDataSource dataSource)
    {
        services.AddDbContext<IntegrationDbContext>(options =>
            options.UseNpgsql(dataSource,
                o => o.MigrationsHistoryTable("__ef_migrations_integration", "integration")));

        services.AddScoped<IIntegrationDbContext>(sp => sp.GetRequiredService<IntegrationDbContext>());

        // Marketplace adapters
        services.AddSingleton<IMarketplaceAdapter, TrendyolMarketplaceAdapter>();

        // Cargo adapters
        services.AddSingleton<ICargoAdapter, YurticiCargoAdapter>();

        // e-Invoice adapters
        services.AddSingleton<IEInvoiceAdapter, ELogoEInvoiceAdapter>();

        // Adapter resolver
        services.AddSingleton<IAdapterResolver, AdapterResolver>();

        // HttpClient factory (required by adapters)
        services.AddHttpClient();

        // Background workers
        services.AddHostedService<MarketplaceOrderFetchWorker>();

        return services;
    }
}
