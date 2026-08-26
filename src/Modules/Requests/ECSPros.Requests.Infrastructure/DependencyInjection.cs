using ECSPros.Requests.Application.Services;
using ECSPros.Requests.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ECSPros.Requests.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRequestsInfrastructure(this IServiceCollection services, NpgsqlDataSource dataSource)
    {
        services.AddDbContext<RequestsDbContext>(options =>
            options.UseNpgsql(dataSource,
                o =>
                {
                    o.MigrationsHistoryTable("__ef_migrations_requests", "requests");
                    o.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);   // Faz 1: geçici DB hatasında otomatik yeniden dene
                }));

        services.AddScoped<IRequestsDbContext>(sp => sp.GetRequiredService<RequestsDbContext>());

        return services;
    }
}
