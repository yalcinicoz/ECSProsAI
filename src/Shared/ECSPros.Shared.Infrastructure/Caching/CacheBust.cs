using ECSPros.Shared.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ECSPros.Shared.Infrastructure.Caching;

/// <summary>
/// FAZ 10 / A9 — Redis pub/sub tabanlı cache bust: yayıncı (Bust) yerel IMemoryCache'ten
/// siler + kanala anahtar yayınlar; abone (StartAsync, HER düğümde — worker rol kapısına
/// GİRMEZ) gelen anahtarı kendi belleğinden siler. Kaynak düğüm kendi mesajını da alır —
/// ikinci Remove zararsız no-op. Redis kapalıyken Bust yalnız yerel çalışır (uyarı loglu);
/// abonelik AbortOnConnectFail=false sayesinde bağlantı gelince kendiliğinden kurulur.
/// </summary>
public sealed class RedisCacheBustService(
    IConnectionMultiplexer redis,
    IMemoryCache memoryCache,
    ILogger<RedisCacheBustService> logger) : ICacheBustPublisher, IHostedService
{
    public const string Kanal = "ECSPros:cache:bust";

    public void Bust(string anahtar)
    {
        memoryCache.Remove(anahtar);
        try
        {
            redis.GetSubscriber().Publish(
                RedisChannel.Literal(Kanal), anahtar, CommandFlags.FireAndForget);
        }
        catch (Exception ex)
        {
            // Yerel silme yapıldı; diğer düğümler kısa TTL ile tazelenir.
            logger.LogWarning(ex, "Cache bust yayını başarısız (anahtar: {Anahtar}) — yalnız yerel silindi.", anahtar);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await redis.GetSubscriber().SubscribeAsync(
                RedisChannel.Literal(Kanal), (_, mesaj) =>
                {
                    var anahtar = (string?)mesaj;
                    if (string.IsNullOrEmpty(anahtar)) return;
                    memoryCache.Remove(anahtar);
                    logger.LogDebug("Cache bust alındı: {Anahtar}", anahtar);
                });
            logger.LogInformation("Cache bust aboneliği kuruldu ({Kanal}).", Kanal);
        }
        catch (Exception ex)
        {
            // Açılışı bozma — multiplexer bağlantı gelince bekleyen abonelikleri kurar.
            logger.LogWarning(ex, "Cache bust aboneliği şu an kurulamadı; Redis bağlanınca denenecek.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Redis yapılandırılmamış ortam: bust yalnız yerel IMemoryCache silmesidir
/// (tek düğüm — bugünkü davranışla birebir aynı).</summary>
public sealed class LocalCacheBustService(IMemoryCache memoryCache) : ICacheBustPublisher
{
    public void Bust(string anahtar) => memoryCache.Remove(anahtar);
}
