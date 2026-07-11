using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Infrastructure.Persistence;
using ECSPros.Crm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ECSPros.Crm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCrmInfrastructure(this IServiceCollection services, NpgsqlDataSource dataSource)
    {
        services.AddDbContext<CrmDbContext>(options =>
            options.UseNpgsql(dataSource,
                o => o.MigrationsHistoryTable("__ef_migrations_crm", "crm")));

        services.AddScoped<ICrmDbContext>(sp => sp.GetRequiredService<CrmDbContext>());
        services.AddScoped<Shared.Contracts.IMemberService, CrmMemberService>(); // G5: modüller arası üye bilgisi
        services.AddScoped<IMemberTokenService, MemberTokenService>();
        services.AddSingleton<IMemberPasswordHasher, MemberPasswordHasher>();

        return services;
    }
}
