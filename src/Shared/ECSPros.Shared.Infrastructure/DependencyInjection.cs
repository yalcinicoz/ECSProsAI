using ECSPros.Shared.Infrastructure.Caching;
using ECSPros.Shared.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECSPros.Shared.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── Redis Cache ───────────────────────────────────────────────
        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "ECSPros:";
            });
            services.AddSingleton<ICacheService, RedisCacheService>();
        }

        // ─── Email / SMS (Stub — replace with real providers in production) ─
        services.AddTransient<IEmailService, LogEmailService>();
        services.AddTransient<ISmsService, LogSmsService>();

        return services;
    }
}
