using ECSPros.Accounts.Application.Services;
using ECSPros.Accounts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
namespace ECSPros.Accounts.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddAccountsInfrastructure(this IServiceCollection services, NpgsqlDataSource dataSource)
    {
        services.AddDbContext<AccountsDbContext>(options =>
            options.UseNpgsql(dataSource, o =>
                {
                    o.MigrationsAssembly(typeof(AccountsDbContext).Assembly.FullName);
                    o.MigrationsHistoryTable("__ef_migrations_accounts", "accounts");
                    o.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);   // Faz 1: geçici DB hatasında otomatik yeniden dene
                }));
        services.AddScoped<IAccountsDbContext>(sp => sp.GetRequiredService<AccountsDbContext>());
        return services;
    }
}
