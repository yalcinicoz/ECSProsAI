using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Infrastructure.Persistence;
using ECSPros.Iam.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ECSPros.Iam.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIamInfrastructure(this IServiceCollection services, NpgsqlDataSource dataSource, IConfiguration configuration)
    {
        services.AddDbContext<IamDbContext>(options =>
            options.UseNpgsql(dataSource,
                o => o.MigrationsHistoryTable("__ef_migrations_iam", "iam")));

        services.AddScoped<IIamDbContext>(sp => sp.GetRequiredService<IamDbContext>());
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        return services;
    }
}
