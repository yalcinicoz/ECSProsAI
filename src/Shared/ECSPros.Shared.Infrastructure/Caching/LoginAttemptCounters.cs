using ECSPros.Shared.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ECSPros.Shared.Infrastructure.Caching;

/// <summary>
/// FAZ 10 / A4 — düğüm-yerel deneme sayacı (Redis yapılandırılmamış ortamlar ve
/// Redis kesintisi sırasındaki geri dönüş yolu). Bugünkü LoginMemberCommand
/// davranışının birebir taşınmışıdır.
/// </summary>
public sealed class MemoryLoginAttemptCounter(IMemoryCache cache) : ILoginAttemptCounter
{
    public Task<int> ArtirAsync(string anahtar, TimeSpan pencere, CancellationToken ct = default)
    {
        var sayi = (cache.TryGetValue<int>(anahtar, out var s) ? s : 0) + 1;
        cache.Set(anahtar, sayi, pencere);
        return Task.FromResult(sayi);
    }

    public Task<int> GetirAsync(string anahtar, CancellationToken ct = default)
        => Task.FromResult(cache.TryGetValue<int>(anahtar, out var s) ? s : 0);

    public Task SifirlaAsync(string anahtar, CancellationToken ct = default)
    {
        cache.Remove(anahtar);
        return Task.CompletedTask;
    }
}

/// <summary>
/// FAZ 10 / A4 — Redis tabanlı deneme sayacı: kilit tüm düğümlerde geçerli.
/// INCR+PEXPIRE tek Lua turunda (atomik — EXPIRE kaybolup kalıcı kilit oluşamaz).
/// Redis hatasında düğüm-yerel sayaca düşer (fail-open: giriş akışı Redis'e
/// bağımlı hâle GELMEZ) ve uyarı loglar.
/// </summary>
public sealed class RedisLoginAttemptCounter(
    IConnectionMultiplexer redis,
    IMemoryCache memoryCache,
    ILogger<RedisLoginAttemptCounter> logger) : ILoginAttemptCounter
{
    private const string OnEk = "ECSPros:login:";
    private readonly MemoryLoginAttemptCounter _yedek = new(memoryCache);

    private static readonly LuaScript ArtirScript = LuaScript.Prepare(
        // KEYS yerine parametreli @ sözdizimi: StackExchange değerleri kendisi bağlar
        "local v = redis.call('INCR', @anahtar) redis.call('PEXPIRE', @anahtar, @pencereMs) return v");

    public async Task<int> ArtirAsync(string anahtar, TimeSpan pencere, CancellationToken ct = default)
    {
        try
        {
            var sonuc = await redis.GetDatabase().ScriptEvaluateAsync(ArtirScript, new
            {
                anahtar = (RedisKey)(OnEk + anahtar),
                pencereMs = (long)pencere.TotalMilliseconds,
            });
            return (int)(long)sonuc;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Login sayacı Redis'e yazılamadı — düğüm-yerel sayaca düşüldü.");
            return await _yedek.ArtirAsync(anahtar, pencere, ct);
        }
    }

    public async Task<int> GetirAsync(string anahtar, CancellationToken ct = default)
    {
        try
        {
            var deger = await redis.GetDatabase().StringGetAsync(OnEk + anahtar);
            return deger.TryParse(out long sayi) ? (int)sayi : 0;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Login sayacı Redis'ten okunamadı — düğüm-yerel sayaca düşüldü.");
            return await _yedek.GetirAsync(anahtar, ct);
        }
    }

    public async Task SifirlaAsync(string anahtar, CancellationToken ct = default)
    {
        // Her iki depo da temizlenir — Redis kesintisi sırasında yerel sayaca yazılmış olabilir.
        await _yedek.SifirlaAsync(anahtar, ct);
        try
        {
            await redis.GetDatabase().KeyDeleteAsync(OnEk + anahtar);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Login sayacı Redis'te sıfırlanamadı (pencere süresiyle kendiliğinden düşer).");
        }
    }
}
