using StackExchange.Redis;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// FAZ 10 / A3 — mobil cihaz doğrulama state'i (challenge / nonce / oturum secret'ı).
/// Çoklu düğümde attestation A düğümünde, imzalı istek B düğümünde işlenebilsin diye
/// state Redis'te tutulur. GÜVENLİK KURALI: Redis erişilemezse FAIL-CLOSED —
/// tek-düğüm belleğe düşülmez (rapor P0-1; replay koruması düğüm-yerel belleğe
/// düşerse anlamsızlaşır). Erişilemezlik <see cref="DeviceStateErisimHatasi"/> ile
/// bildirilir; uçlar bunu anlaşılır 503'e çevirir.
/// </summary>
public interface IDeviceStateStore
{
    /// <summary>Challenge'ı kaydeder (tek kullanımlık, ttl sonunda düşer).</summary>
    Task ChallengeKaydetAsync(string challenge, TimeSpan ttl);

    /// <summary>Tek kullanımlık tüketim (atomik sil-ve-dön): true = challenge vardı ve
    /// tüketildi; false = yok/süresi dolmuş/daha önce kullanılmış.</summary>
    Task<bool> ChallengeTuketAsync(string challenge);

    /// <summary>Nonce ilk kez mi görülüyor (SET NX): true = ilk kullanım (kaydedildi),
    /// false = replay.</summary>
    Task<bool> NonceIlkKullanimMiAsync(string jti, string nonce, TimeSpan pencere);

    /// <summary>Attestation sonrası üretilen oturum imza secret'ını saklar.</summary>
    Task SecretKaydetAsync(string jti, string secret, TimeSpan ttl);

    /// <summary>Oturum secret'ı (yoksa null — token süresi dolmuş ya da hiç attestation olmamış).</summary>
    Task<string?> SecretGetirAsync(string jti);
}

/// <summary>Device state deposuna erişilemedi (Redis kapalı/yapılandırılmamış) — fail-closed.</summary>
public sealed class DeviceStateErisimHatasi : Exception
{
    public DeviceStateErisimHatasi(string mesaj, Exception? ic = null) : base(mesaj, ic) { }
}

public sealed class RedisDeviceStateStore(
    IConnectionMultiplexer redis,
    ILogger<RedisDeviceStateStore> logger) : IDeviceStateStore
{
    private const string OnEk = "ECSPros:device:";

    private IDatabase Db => redis.GetDatabase();

    public Task ChallengeKaydetAsync(string challenge, TimeSpan ttl)
        => Sar(() => Db.StringSetAsync($"{OnEk}challenge:{challenge}", "1", ttl, When.NotExists));

    public Task<bool> ChallengeTuketAsync(string challenge)
        // KeyDelete atomik "vardı ve silindi" döner — challenge iki kez tüketilemez.
        => Sar(() => Db.KeyDeleteAsync($"{OnEk}challenge:{challenge}"));

    public Task<bool> NonceIlkKullanimMiAsync(string jti, string nonce, TimeSpan pencere)
        => Sar(() => Db.StringSetAsync($"{OnEk}nonce:{jti}:{nonce}", "1", pencere, When.NotExists));

    public Task SecretKaydetAsync(string jti, string secret, TimeSpan ttl)
        => Sar(() => Db.StringSetAsync($"{OnEk}secret:{jti}", secret, ttl));

    public async Task<string?> SecretGetirAsync(string jti)
    {
        var deger = await Sar(() => Db.StringGetAsync($"{OnEk}secret:{jti}"));
        return deger.IsNullOrEmpty ? null : (string?)deger;
    }

    private async Task<T> Sar<T>(Func<Task<T>> islem)
    {
        try
        {
            return await islem();
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogError(ex, "Device state Redis işlemi başarısız — fail-closed.");
            throw new DeviceStateErisimHatasi("Cihaz doğrulama deposuna erişilemiyor.", ex);
        }
    }
}

/// <summary>
/// Redis yapılandırılmamış ortam (ör. bağlantı dizesi boş): mobil attestation bilinçli
/// olarak devre dışıdır (fail-closed). Web sitesi bu yoldan geçmez — etkilenmez.
/// </summary>
public sealed class RedisYapilandirilmamisDeviceStateStore : IDeviceStateStore
{
    private static DeviceStateErisimHatasi Hata() =>
        new("Cihaz doğrulama deposu için Redis yapılandırılmamış (fail-closed).");

    public Task ChallengeKaydetAsync(string challenge, TimeSpan ttl) => throw Hata();
    public Task<bool> ChallengeTuketAsync(string challenge) => throw Hata();
    public Task<bool> NonceIlkKullanimMiAsync(string jti, string nonce, TimeSpan pencere) => throw Hata();
    public Task SecretKaydetAsync(string jti, string secret, TimeSpan ttl) => throw Hata();
    public Task<string?> SecretGetirAsync(string jti) => throw Hata();
}
