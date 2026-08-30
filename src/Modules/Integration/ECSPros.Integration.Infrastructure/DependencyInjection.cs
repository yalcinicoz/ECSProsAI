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
    /// <param name="workerRoluAktif">FAZ 10 / A2: false ise arka plan worker'ları bu düğümde
    /// kaydedilmez (Node:Role=Api). Varsayılan true — tek sunucu davranışı değişmez.</param>
    public static IServiceCollection AddIntegrationInfrastructure(
        this IServiceCollection services, NpgsqlDataSource dataSource, bool workerRoluAktif = true)
    {
        services.AddDbContext<IntegrationDbContext>(options =>
            options.UseNpgsql(dataSource,
                o =>
                {
                    o.MigrationsHistoryTable("__ef_migrations_integration", "integration");
                    o.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);   // Faz 1: geçici DB hatasında otomatik yeniden dene
                }));

        services.AddScoped<IIntegrationDbContext>(sp => sp.GetRequiredService<IntegrationDbContext>());

        // Marketplace adapters
        services.AddSingleton<IMarketplaceAdapter, TrendyolMarketplaceAdapter>();
        services.AddSingleton<AmazonSpApiClient>();
        services.AddSingleton<IMarketplaceAdapter, AmazonMarketplaceAdapter>();

        // Cargo adapters
        services.AddSingleton<ICargoAdapter, YurticiCargoAdapter>();

        // e-Invoice adapters
        services.AddSingleton<IEInvoiceAdapter, ELogoEInvoiceAdapter>();

        // Adapter resolver
        services.AddSingleton<IAdapterResolver, AdapterResolver>();

        // HttpClient factory (required by adapters)
        services.AddHttpClient();

        // Background workers — yalnız Worker/Both rollü düğümde (FAZ 10 / A2)
        if (workerRoluAktif)
            services.AddHostedService<MarketplaceOrderFetchWorker>();

        return services;
    }
}
