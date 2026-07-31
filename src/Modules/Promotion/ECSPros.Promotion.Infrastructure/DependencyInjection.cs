using ECSPros.Promotion.Application.Services;
using ECSPros.Promotion.Infrastructure.Persistence;
using ECSPros.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ECSPros.Promotion.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPromotionInfrastructure(this IServiceCollection services, NpgsqlDataSource dataSource)
    {
        services.AddDbContext<PromotionDbContext>(options =>
            options.UseNpgsql(dataSource,
                o => o.MigrationsHistoryTable("__ef_migrations_promotion", "promotion")));

        services.AddScoped<IPromotionDbContext>(sp => sp.GetRequiredService<PromotionDbContext>());

        // F2: kampanya çözümleme servisi (F3 kart/detay + F4 checkout ortak çekirdek).
        services.AddScoped<IProductCampaignResolver, ProductCampaignResolver>();

        return services;
    }
}
