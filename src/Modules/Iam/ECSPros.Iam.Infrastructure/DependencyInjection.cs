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
                o =>
                {
                    o.MigrationsHistoryTable("__ef_migrations_iam", "iam");
                    o.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);   // Faz 1: geçici DB hatasında otomatik yeniden dene
                }));

        services.AddScoped<IIamDbContext>(sp => sp.GetRequiredService<IamDbContext>());
        // Y1 (2026-09-09, K3): efektif yetkinin TEK kaynağı — yetki kontrolleri her istekte buradan
        // okur (token'a gömülmez), yetki değişince önbellek düşürülür.
        services.AddScoped<IEtkinYetkiServisi, EtkinYetkiServisi>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ISupplierUserTokenService, SupplierUserTokenService>();

        return services;
    }
}
