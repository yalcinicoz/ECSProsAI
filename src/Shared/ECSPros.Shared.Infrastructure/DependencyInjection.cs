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
        // MemoryLoginAttemptCounter / Redis geri dönüş yolu için (TryAdd — host zaten
        // çağırdıysa etkisiz).
        services.AddMemoryCache();

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

            // FAZ 10 / A3-A4: cache DIŞI Redis kullanımı (SET NX/Lua/pub-sub — device state,
            // login sayacı, cache bust) için paylaşılan multiplexer. Aynı güvenlik ağı
            // seçenekleriyle (AbortOnConnectFail=false + kısa timeout) — Redis kapalıyken
            // açılış patlamaz, işlemler hızlı hata verir; fail-open/closed kararı TÜKETİCİNİN
            // sorumluluğudur (device state fail-closed, login sayacı memory'ye düşer).
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));

            // FAZ 10 / A4: hesap bazlı hatalı giriş sayacı Redis'te (kilit tüm düğümlerde);
            // Redis hatasında sınıf kendi içinde düğüm-yerel sayaca düşer (fail-open).
            services.AddSingleton<ILoginAttemptCounter, RedisLoginAttemptCounter>();
        }
        else
        {
            services.AddSingleton<ICacheService, NoOpCacheService>();
            services.AddSingleton<ILoginAttemptCounter, MemoryLoginAttemptCounter>();
        }

        // ─── Email / SMS ────────────────────────────────────────────────
        // H8 (K12 kararı): e-posta gerçek kanal. SmtpEmailService ayarları sırayla çözer:
        // DB (ISmtpSettingsProvider — Api kaydeder) → Email:Smtp config → ikisi de yoksa
        // log'a yazar (site e-postasız da çalışır; Redis kayıt deseniyle aynı güvenlik ağı).
        // SMS gerçek kanal (2026-07-15, K3 kapandı): GES Telekom (TT Mesaj) — ayarlar DB'den
        // (ISmsSettingsProvider — Api kaydeder); kayıt yoksa log'a yazar (eski stub davranışı).
        services.AddHttpClient();
        services.AddTransient<IEmailService, SmtpEmailService>();
        services.AddTransient<ISmsService, GesTelekomSmsService>();

        return services;
    }
}
