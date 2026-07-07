using ECSPros.Shared.Contracts;

namespace ECSPros.Shared.Infrastructure.Caching;

/// <summary>
/// Redis yapılandırılmamışsa kullanılan boş cache: her okuma miss, her yazma no-op.
/// ICacheService HER ZAMAN kayıtlı olmalı — bağlantı dizesi silinse bile ICacheService
/// enjekte eden handler'lar DI hatasıyla patlamamalı, site cache'siz çalışmaya devam etmeli.
/// </summary>
public class NoOpCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
        => Task.FromResult<T?>(null);

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
        => Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task RemoveByPatternAsync(string pattern, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => Task.FromResult(false);
}
