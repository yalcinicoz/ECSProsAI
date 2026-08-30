using Npgsql;
using Microsoft.Extensions.Configuration;

namespace ECSPros.Api.Tests.Acceptance;

internal static class AcceptanceTestEnvironment
{
    private const string ConnectionVariable = "ECSPROS_ACCEPTANCE_POSTGRES";
    private const string WriteVariable = "ECSPROS_ACCEPTANCE_ALLOW_DB_WRITE";
    private static readonly Lazy<IConfigurationRoot> LocalConfiguration = new(LoadLocalConfiguration);

    public static string RequirePostgres(bool requiresWrite)
    {
        var connectionString = Get(ConnectionVariable, "Acceptance:Postgres:ConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
            Assert.Inconclusive($"{ConnectionVariable} verilmedi; PostgreSQL acceptance testi atlandı.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var database = builder.Database?.Trim() ?? string.Empty;
        if (!database.Contains("test", StringComparison.OrdinalIgnoreCase) &&
            !database.Contains("acceptance", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail(
                $"Güvenlik kapısı: acceptance bağlantısındaki Database adı 'test' veya 'acceptance' içermelidir. " +
                $"Canlı/veri taşıyan DB üzerinde test çalıştırılmaz.");
        }

        if (requiresWrite && !GetBoolean(WriteVariable, "Acceptance:Postgres:AllowWrite"))
        {
            Assert.Inconclusive($"Yazmalı acceptance testi için {WriteVariable}=true açıkça verilmelidir.");
        }

        builder.ApplicationName = "ECSPros-Acceptance-Tests";
        builder.Timeout = Math.Min(builder.Timeout <= 0 ? 5 : builder.Timeout, 10);
        builder.CommandTimeout = Math.Min(builder.CommandTimeout <= 0 ? 30 : builder.CommandTimeout, 60);
        return builder.ConnectionString;
    }

    public static string RequireRedis()
    {
        const string connectionVariable = "ECSPROS_ACCEPTANCE_REDIS";
        var connectionString = Get(connectionVariable, "Acceptance:Redis:ConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
            Assert.Inconclusive($"{connectionVariable} veya appsettingsTest.json Redis bağlantısı verilmedi; test atlandı.");
        if (!GetBoolean("ECSPROS_ACCEPTANCE_REDIS_ALLOW_WRITE", "Acceptance:Redis:AllowWrite"))
            Assert.Inconclusive("Redis yazmalı acceptance testi için AllowWrite=true açıkça verilmelidir.");
        return connectionString;
    }

    public static string Require(string environmentVariable, string configurationKey, string description)
    {
        var value = Get(environmentVariable, configurationKey);
        if (string.IsNullOrWhiteSpace(value))
            Assert.Inconclusive($"{description} verilmedi; acceptance testi atlandı.");
        return value;
    }

    public static string? Optional(string environmentVariable, string configurationKey)
        => Get(environmentVariable, configurationKey);

    public static bool GetBoolean(string environmentVariable, string configurationKey)
        => bool.TryParse(Get(environmentVariable, configurationKey), out var value) && value;

    private static string? Get(string environmentVariable, string configurationKey)
    {
        var environmentValue = Environment.GetEnvironmentVariable(environmentVariable);
        return !string.IsNullOrWhiteSpace(environmentValue)
            ? environmentValue
            : LocalConfiguration.Value[configurationKey];
    }

    private static IConfigurationRoot LoadLocalConfiguration()
    {
        var path = FindLocalSettings(Directory.GetCurrentDirectory())
                   ?? FindLocalSettings(AppContext.BaseDirectory);
        var builder = new ConfigurationBuilder();
        if (path is not null)
            builder.AddJsonFile(path, optional: false, reloadOnChange: false);
        return builder.Build();
    }

    private static string? FindLocalSettings(string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "appsettingsTest.json");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        return null;
    }
}
