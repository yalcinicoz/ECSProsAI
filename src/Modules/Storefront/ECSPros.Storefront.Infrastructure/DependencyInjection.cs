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
                o =>
                {
                    o.MigrationsHistoryTable("__ef_migrations_storefront", "storefront");
                    o.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);   // Faz 1: geçici DB hatasında otomatik yeniden dene
                }));

        services.AddScoped<IStorefrontDbContext>(sp => sp.GetRequiredService<StorefrontDbContext>());
        services.AddScoped<IChannelPricingService, StorefrontChannelPricingService>();
        services.AddScoped<IChannelProductFlagService, StorefrontChannelProductFlagService>();
        services.AddMemoryCache();
        services.AddScoped<Application.Services.ChannelScoping.ChannelScopeResolver>();   // F1 kanal kapsamı
        services.AddScoped<IProductReviewStatsService, StorefrontProductReviewStatsService>(); // E7: kart/detay puanları
        services.AddScoped<ICardMessageResolver, Application.Services.CardMessageResolver>(); // Ürün Kartı F2: kart mesajları

        return services;
    }
}
