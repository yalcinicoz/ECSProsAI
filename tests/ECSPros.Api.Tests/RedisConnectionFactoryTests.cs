using ECSPros.Shared.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class RedisConnectionFactoryTests
{
    [TestMethod]
    public void LegacyConnection_CacheVeStateIcinGeriyeUyumludur()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Redis"] = "127.0.0.1:6379,password=test-password"
        });

        Assert.IsTrue(RedisConnectionFactory.IsCacheConfigured(configuration));
        Assert.IsTrue(RedisConnectionFactory.IsStateConfigured(configuration));
        Assert.AreEqual("127.0.0.1:6379", RedisConnectionFactory.CreateCache(configuration).EndPoints[0].ToString());
    }

    [TestMethod]
    public void SentinelMode_ServiceNameVeGuvenliTimeoutlariUygular()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["ConnectionStrings:RedisState"] = "sentinel-1:26379,sentinel-2:26379,sentinel-3:26379",
            ["Redis:State:Mode"] = "Sentinel",
            ["Redis:State:ServiceName"] = "ecspros-state",
            ["Redis:State:ConnectTimeoutMs"] = "2500",
            ["Redis:State:ConnectRetry"] = "2",
            ["Redis:State:AsyncTimeoutMs"] = "1200",
            ["Redis:State:SyncTimeoutMs"] = "1300"
        });

        var options = RedisConnectionFactory.CreateState(configuration);

        Assert.AreEqual("ecspros-state", options.ServiceName);
        Assert.AreEqual(string.Empty, options.TieBreaker);
        Assert.IsFalse(options.AbortOnConnectFail);
        Assert.AreEqual(2500, options.ConnectTimeout);
        Assert.AreEqual(2, options.ConnectRetry);
        Assert.AreEqual(1200, options.AsyncTimeout);
        Assert.AreEqual(1300, options.SyncTimeout);
    }

    [TestMethod]
    public void SentinelMode_ServiceNameOlmadanReddedilir()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["ConnectionStrings:RedisState"] = "127.0.0.1:26379",
            ["Redis:State:Mode"] = "Sentinel"
        });

        Assert.ThrowsExactly<InvalidOperationException>(() => RedisConnectionFactory.CreateState(configuration));
    }

    [TestMethod]
    public void BilinmeyenModeReddedilir()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["ConnectionStrings:RedisCache"] = "127.0.0.1:6379",
            ["Redis:Cache:Mode"] = "ClusterMaybe"
        });

        Assert.ThrowsExactly<InvalidOperationException>(() => RedisConnectionFactory.CreateCache(configuration));
    }

    [TestMethod]
    [DataRow("ConnectTimeoutMs", "99")]
    [DataRow("ConnectTimeoutMs", "60001")]
    [DataRow("ConnectRetry", "-1")]
    [DataRow("ConnectRetry", "11")]
    [DataRow("AsyncTimeoutMs", "0")]
    [DataRow("SyncTimeoutMs", "60001")]
    public void GecersizTimeoutVeyaRetryStartupOncesiReddedilir(string key, string value)
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["ConnectionStrings:RedisCache"] = "127.0.0.1:6379",
            [$"Redis:Cache:{key}"] = value
        });

        Assert.ThrowsExactly<InvalidOperationException>(() => RedisConnectionFactory.CreateCache(configuration));
    }

    private static IConfiguration Build(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
