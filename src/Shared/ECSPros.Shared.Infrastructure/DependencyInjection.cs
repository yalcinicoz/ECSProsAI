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

        if (RedisConnectionFactory.IsCacheConfigured(configuration))
        {
            // Bağlantı seçenekleri BURADA, kodda zorlanır — connection string'e kim ne
            // yazarsa yazsın bu güvenlik ağı geçerli kalır:
            //   AbortOnConnectFail=false → Redis kapalıyken uygulama açılışı/istekler patlamaz
            //   kısa timeout'lar        → kötü günde istek başına maliyet ~1 sn ile sınırlı
            //     (RedisCacheService'in devre kesicisi bu maliyeti de ilk isteklerle sınırlar)
            var cacheRedisOptions = RedisConnectionFactory.CreateCache(configuration);

            services.AddStackExchangeRedisCache(options =>
            {
                options.ConfigurationOptions = cacheRedisOptions;
                options.InstanceName = configuration["Redis:Cache:InstanceName"] ?? "ECSPros:";
            });
            services.AddSingleton<ICacheService, RedisCacheService>();
        }
        else
        {
            services.AddSingleton<ICacheService, NoOpCacheService>();
        }

        // ─── Redis kritik state / pub-sub ─────────────────────────────
        // Cache ve güvenlik/oturum/SignalR farklı eviction politikalarına sahip Redis
        // kümelerinde çalışabilir. State bağlantısı yoksa güvenli mevcut fallback'ler korunur.
        if (RedisConnectionFactory.IsStateConfigured(configuration))
        {
            var stateRedisOptions = RedisConnectionFactory.CreateState(configuration);
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(stateRedisOptions));

            // Hesap bazlı giriş sayacı Redis hatasında kendi memory sayacına düşer.
            services.AddSingleton<ILoginAttemptCounter, RedisLoginAttemptCounter>();

            // Cache bust pub/sub tüm API düğümlerinin local memory cache'ini temizler.
            services.AddSingleton<RedisCacheBustService>();
            services.AddSingleton<ICacheBustPublisher>(sp => sp.GetRequiredService<RedisCacheBustService>());
            services.AddHostedService(sp => sp.GetRequiredService<RedisCacheBustService>());
        }
        else
        {
            services.AddSingleton<ILoginAttemptCounter, MemoryLoginAttemptCounter>();
            services.AddSingleton<ICacheBustPublisher, LocalCacheBustService>();
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
