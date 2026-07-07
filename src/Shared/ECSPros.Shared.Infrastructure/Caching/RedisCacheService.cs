using System.Text.Json;
using ECSPros.Shared.Contracts;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ECSPros.Shared.Infrastructure.Caching;

/// <summary>
/// Redis destekli cache — HATA-GÜVENLİ tasarım:
/// Redis'e erişilemezse hiçbir çağıran etkilenmez (okuma miss döner, yazma sessizce atlanır)
/// ve devre kesici sayesinde timeout maliyeti art arda ödenmez. Bu sınıf ASLA exception
/// fırlatmamalıdır — geçmişte Redis arızası site genelinde 6-22 sn'lik yavaşlamalara yol
/// açtı (bkz. PROGRESS.md 2026-07-06 Redis notları); bu tasarım o senaryoyu yapısal olarak
/// imkânsız kılar. Handler'lardaki ayrıca try/catch'ler artık zorunlu değil ama zararsız.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheService> _logger;

    // Devre kesici: art arda hata görülürse bu süre boyunca Redis'e hiç gidilmez.
    // Statik — servis singleton ama olası çoklu kayıtlara karşı da tek devre davranır.
    private static readonly TimeSpan CircuitOpenDuration = TimeSpan.FromMinutes(2);
    private static long _circuitOpenedAtTicks; // 0 = devre kapalı (Redis denenir)

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RedisCacheService(IDistributedCache cache, ILogger<RedisCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    private static bool CircuitOpen
    {
        get
        {
            var openedAt = Interlocked.Read(ref _circuitOpenedAtTicks);
            if (openedAt == 0) return false;
            if (DateTime.UtcNow.Ticks - openedAt < CircuitOpenDuration.Ticks) return true;
            Interlocked.Exchange(ref _circuitOpenedAtTicks, 0); // süre doldu — tekrar dene
            return false;
        }
    }

    private void TripCircuit(string operation, Exception ex)
    {
        // Devre zaten açıksa tekrar loglamayalım (log spam koruması)
        if (Interlocked.Exchange(ref _circuitOpenedAtTicks, DateTime.UtcNow.Ticks) == 0)
        {
            _logger.LogWarning(ex,
                "Redis cache erişilemiyor ({Operation}) — {Minutes} dk boyunca cache atlanacak, site cache'siz devam ediyor.",
                operation, CircuitOpenDuration.TotalMinutes);
        }
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        if (CircuitOpen) return null;
        try
        {
            var data = await _cache.GetStringAsync(key, ct);
            return data is null ? null : JsonSerializer.Deserialize<T>(data, _jsonOptions);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return null; }
        catch (Exception ex)
        {
            TripCircuit($"GET {key}", ex);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
    {
        if (CircuitOpen) return;
        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(5)
            };

            var json = JsonSerializer.Serialize(value, _jsonOptions);
            await _cache.SetStringAsync(key, json, options, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            TripCircuit($"SET {key}", ex);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        if (CircuitOpen) return;
        try
        {
            await _cache.RemoveAsync(key, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            TripCircuit($"REMOVE {key}", ex);
        }
    }

    public Task RemoveByPatternAsync(string pattern, CancellationToken ct = default)
    {
        // Pattern-based removal requires StackExchange.Redis directly (IDistributedCache doesn't support it)
        // For now, log a warning — implement via IConnectionMultiplexer when needed
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        if (CircuitOpen) return false;
        try
        {
            var data = await _cache.GetStringAsync(key, ct);
            return data is not null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return false; }
        catch (Exception ex)
        {
            TripCircuit($"EXISTS {key}", ex);
            return false;
        }
    }
}
