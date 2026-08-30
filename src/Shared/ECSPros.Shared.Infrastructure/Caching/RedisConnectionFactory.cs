using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace ECSPros.Shared.Infrastructure.Caching;

/// <summary>
/// Cache ile kritik state trafiğinin ayrı Redis kümelerine yönlendirilebilmesini sağlar.
/// Yeni bağlantı adları yoksa mevcut ConnectionStrings:Redis ayarına geri düşerek eski
/// kurulumlarla uyumluluğu korur. Sentinel modunda ServiceName primary discovery'yi açar.
/// </summary>
public static class RedisConnectionFactory
{
    public const string LegacyConnectionName = "Redis";
    public const string CacheConnectionName = "RedisCache";
    public const string StateConnectionName = "RedisState";

    public static bool IsCacheConfigured(IConfiguration configuration) =>
        HasConnection(configuration, CacheConnectionName);

    public static bool IsStateConfigured(IConfiguration configuration) =>
        HasConnection(configuration, StateConnectionName);

    public static ConfigurationOptions CreateCache(IConfiguration configuration) =>
        Create(configuration, "Redis:Cache", CacheConnectionName);

    public static ConfigurationOptions CreateState(IConfiguration configuration) =>
        Create(configuration, "Redis:State", StateConnectionName);

    private static bool HasConnection(IConfiguration configuration, string connectionName) =>
        !string.IsNullOrWhiteSpace(configuration.GetConnectionString(connectionName)) ||
        !string.IsNullOrWhiteSpace(configuration.GetConnectionString(LegacyConnectionName));

    private static ConfigurationOptions Create(
        IConfiguration configuration,
        string sectionPath,
        string connectionName)
    {
        var connectionString = configuration.GetConnectionString(connectionName)
            ?? configuration.GetConnectionString(LegacyConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"ConnectionStrings:{connectionName} veya ConnectionStrings:{LegacyConnectionName} gerekli.");

        var options = ConfigurationOptions.Parse(connectionString);
        var section = configuration.GetSection(sectionPath);
        var mode = section["Mode"]?.Trim();
        if (!string.IsNullOrEmpty(mode) &&
            !mode.Equals("Standalone", StringComparison.OrdinalIgnoreCase) &&
            !mode.Equals("Sentinel", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{sectionPath}:Mode yalnız Standalone veya Sentinel olabilir.");

        if (mode?.Equals("Sentinel", StringComparison.OrdinalIgnoreCase) == true)
        {
            var serviceName = section["ServiceName"]?.Trim();
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new InvalidOperationException($"{sectionPath}:ServiceName Sentinel modunda zorunludur.");

            options.ServiceName = serviceName;
            options.TieBreaker = string.Empty;
        }

        var connectTimeout = Bounded(section, "ConnectTimeoutMs", 1500, 100, 60_000);
        var connectRetry = Bounded(section, "ConnectRetry", 1, 0, 10);
        var asyncTimeout = Bounded(section, "AsyncTimeoutMs", 1000, 100, 60_000);
        var syncTimeout = Bounded(section, "SyncTimeoutMs", 1000, 100, 60_000);

        options.AbortOnConnectFail = false;
        options.ConnectTimeout = connectTimeout;
        options.ConnectRetry = connectRetry;
        options.AsyncTimeout = asyncTimeout;
        options.SyncTimeout = syncTimeout;
        options.ClientName = section["ClientName"] ?? options.ClientName;

        return options;
    }

    private static int Bounded(
        IConfigurationSection section, string key, int defaultValue, int minimum, int maximum)
    {
        var value = section.GetValue(key, defaultValue);
        if (value < minimum || value > maximum)
            throw new InvalidOperationException(
                $"{section.Path}:{key} {minimum}-{maximum} aralığında olmalıdır.");
        return value;
    }
}
