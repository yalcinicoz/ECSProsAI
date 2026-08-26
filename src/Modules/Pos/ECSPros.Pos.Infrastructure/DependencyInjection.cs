using ECSPros.Pos.Application.Services;
using ECSPros.Pos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ECSPros.Pos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPosInfrastructure(this IServiceCollection services, NpgsqlDataSource dataSource)
    {
        services.AddDbContext<PosDbContext>(options =>
            options.UseNpgsql(dataSource,
                o =>
                {
                    o.MigrationsHistoryTable("__ef_migrations_pos", "pos");
                    o.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);   // Faz 1: geçici DB hatasında otomatik yeniden dene
                }));

        services.AddScoped<IPosDbContext>(sp => sp.GetRequiredService<PosDbContext>());

        return services;
    }
}
