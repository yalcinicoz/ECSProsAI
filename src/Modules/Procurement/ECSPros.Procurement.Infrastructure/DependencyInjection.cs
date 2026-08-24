using ECSPros.Procurement.Application.Services;
using ECSPros.Procurement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ECSPros.Procurement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProcurementInfrastructure(this IServiceCollection services, NpgsqlDataSource dataSource)
    {
        services.AddDbContext<ProcurementDbContext>(options =>
            options.UseNpgsql(dataSource,
                o => o.MigrationsHistoryTable("__ef_migrations_procurement", "procurement")));

        services.AddScoped<IProcurementDbContext>(sp => sp.GetRequiredService<ProcurementDbContext>());

        return services;
    }
}
