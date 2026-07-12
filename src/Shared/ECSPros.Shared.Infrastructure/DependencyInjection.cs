using ECSPros.Shared.Contracts;
using ECSPros.Shared.Infrastructure.Caching;
using ECSPros.Shared.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace ECSPros.Shared.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── Redis Cache ───────────────────────────────────────────────
        // ICacheService HER ZAMAN kayıtlıdır: Redis yapılandırılmamışsa NoOp'a düşer.
        // Böylece bağlantı dizesinin silinmesi/bozulması ICacheService enjekte eden
        // handler'ları DI hatasıyla patlatamaz — site en kötü ihtimalle cache'siz çalışır.
        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            // Bağlantı seçenekleri BURADA, kodda zorlanır — connection string'e kim ne
            // yazarsa yazsın bu güvenlik ağı geçerli kalır:
            //   AbortOnConnectFail=false → Redis kapalıyken uygulama açılışı/istekler patlamaz
            //   kısa timeout'lar        → kötü günde istek başına maliyet ~1 sn ile sınırlı
            //     (RedisCacheService'in devre kesicisi bu maliyeti de ilk isteklerle sınırlar)
            var redisOptions = ConfigurationOptions.Parse(redisConnection);
            redisOptions.AbortOnConnectFail = false;
            redisOptions.ConnectTimeout = 1500;
            redisOptions.ConnectRetry = 1;
            redisOptions.AsyncTimeout = 1000;
            redisOptions.SyncTimeout = 1000;

            services.AddStackExchangeRedisCache(options =>
            {
                options.ConfigurationOptions = redisOptions;
                options.InstanceName = "ECSPros:";
            });
            services.AddSingleton<ICacheService, RedisCacheService>();
        }
        else
        {
            services.AddSingleton<ICacheService, NoOpCacheService>();
        }

        // ─── Email / SMS ────────────────────────────────────────────────
        // H8 (K12 kararı): e-posta gerçek kanal — Email:Smtp:Host yapılandırılmışsa SMTP,
        // yoksa Log stub'ı (site e-postasız da çalışır; Redis kayıt deseniyle aynı güvenlik
        // ağı). SMS için sağlayıcı kararı (K3) hâlâ açık — Log stub'ı kalır.
        if (!string.IsNullOrWhiteSpace(configuration["Email:Smtp:Host"]))
            services.AddTransient<IEmailService, SmtpEmailService>();
        else
            services.AddTransient<IEmailService, LogEmailService>();
        services.AddTransient<ISmsService, LogSmsService>();

        return services;
    }
}
