using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace ECSPros.Api.Services.Health;

/// <summary>Faz 1: PostgreSQL canlılık — paylaşılan NpgsqlDataSource üzerinden SELECT 1 (3 sn sınır).</summary>
public sealed class DbHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            await using var conn = await dataSource.OpenConnectionAsync(cts.Token);
            await using var cmd = new NpgsqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync(cts.Token);
            return HealthCheckResult.Healthy("PostgreSQL erişilebilir.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL erişilemiyor.", ex);
        }
    }
}

/// <summary>
/// FAZ 10 / A8: Data Protection key ring okunabilirliği — Protect/Unprotect turu.
/// Key ring (DB birincil + dosya yedekli, A1) okunamazsa entegrasyon kimlik bilgileri
/// çözülemez; /ready bu durumda 503 döner ve nginx upstream düğümü trafikten çıkarır.
/// </summary>
public sealed class DataProtectionHealthCheck(
    Microsoft.AspNetCore.DataProtection.IDataProtectionProvider provider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var protector = provider.CreateProtector("ECSPros.HealthCheck");
            var geri = protector.Unprotect(protector.Protect("ping"));
            return Task.FromResult(geri == "ping"
                ? HealthCheckResult.Healthy("Data Protection key ring okunabilir.")
                : HealthCheckResult.Unhealthy("Data Protection tur doğrulaması başarısız."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Data Protection key ring okunamıyor.", ex));
        }
    }
}

/// <summary>
/// FAZ 10 / A3: /ready için Redis STATE kontrolü — cihaz doğrulama state'i (challenge/nonce/
/// secret) Redis'te ve fail-closed olduğundan, Redis yapılandırılmış ama erişilemiyorsa düğüm
/// hazır DEĞİLDİR (503 → nginx upstream düğümü çıkarır). Redis hiç yapılandırılmamışsa
/// Degraded döner (bilinçli ops durumu: mobil attestation kapalı, site çalışır).
/// /health bu kontrolü ÇALIŞTIRMAZ — oradaki degraded=200 cache davranışı korunur.
/// </summary>
public sealed class RedisStateHealthCheck(IServiceProvider services, IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Redis")))
            return HealthCheckResult.Degraded("Redis yapılandırılmamış — mobil attestation fail-closed, site çalışır.");
        try
        {
            var redis = services.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>();
            await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy("Redis state deposu erişilebilir.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Redis erişilemiyor — cihaz doğrulama state'i kullanılamaz (fail-closed).", ex);
        }
    }
}

/// <summary>
/// Faz 1: Redis canlılık — IDistributedCache yaz-oku turu (bağlantı seçenekleri zaten 1-1.5 sn timeout'lu).
/// Redis yapılandırılmamışsa (NoOpCacheService/MemoryDistributedCache) Healthy döner — cache opsiyoneldir;
/// yapılandırılmış ama erişilemezse DEGRADED (site cache'siz de çalışır — CLAUDE.md Redis kuralı).
/// </summary>
public sealed class RedisHealthCheck(IDistributedCache cache, IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Redis")))
            return HealthCheckResult.Healthy("Redis yapılandırılmamış (opsiyonel).");
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            var key = "health:ping";
            await cache.SetStringAsync(key, "1", new DistributedCacheEntryOptions
            { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10) }, cts.Token);
            var v = await cache.GetStringAsync(key, cts.Token);
            return v == "1"
                ? HealthCheckResult.Healthy("Redis erişilebilir.")
                : HealthCheckResult.Degraded("Redis yaz-oku doğrulanamadı.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Redis erişilemiyor (site cache'siz çalışır).", ex);
        }
    }
}
