using ECSPros.Promotion.Infrastructure.Persistence;
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

        return services;
    }
}
