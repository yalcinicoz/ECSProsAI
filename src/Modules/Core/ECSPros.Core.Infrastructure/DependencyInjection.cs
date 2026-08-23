using ECSPros.Core.Application.Services;
using ECSPros.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ECSPros.Core.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreInfrastructure(this IServiceCollection services, NpgsqlDataSource dataSource)
    {
        services.AddDbContext<CoreDbContext>(options =>
            options.UseNpgsql(dataSource,
                o => o.MigrationsHistoryTable("__ef_migrations_core", "core")));

        services.AddScoped<ICoreDbContext>(sp => sp.GetRequiredService<CoreDbContext>());
        services.AddScoped<ECSPros.Shared.Contracts.ICargoCodeService, Services.CargoCodeService>();
        services.AddMemoryCache();
        services.AddScoped<ECSPros.Shared.Contracts.Channels.IChannelCapabilityResolver, Services.ChannelCapabilityResolver>();

        return services;
    }
}
