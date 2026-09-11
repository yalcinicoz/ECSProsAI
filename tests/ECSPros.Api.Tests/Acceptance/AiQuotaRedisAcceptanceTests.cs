using ECSPros.Api.Services.AiReporting;
using StackExchange.Redis;

namespace ECSPros.Api.Tests.Acceptance;

/// <summary>
/// Requires a dedicated disposable Redis on 127.0.0.1:16379 and explicit opt-in.
/// Never reads appsettings, never FLUSHes a database. Two independent client connections,
/// not an actual two-process/API-node deployment test.
/// </summary>
[TestClass]
public sealed class AiQuotaRedisAcceptanceTests
{
    [TestMethod]
    public Task ConcurrentClientsCannotExceedUserLimit() => Run(async (a, b, keys) =>
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(i =>
            Reserve(i % 2 == 0 ? a : b, keys[..3], 5, 50, 100)));
        Assert.AreEqual(5, results.Count(x => x == 0));
        Assert.IsTrue(results.Where(x => x != 0).All(x => x > 0));
        Assert.AreEqual("5", (string?)await a.StringGetAsync(keys[2]));
    });

    [TestMethod]
    public Task DifferentUsersShareFirmLimitAtomically() => Run(async (a, b, keys) =>
    {
        var secondUser = new[] { keys[3], keys[4], keys[2] };
        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(i =>
            Reserve(i % 2 == 0 ? a : b, i % 2 == 0 ? keys[..3] : secondUser, 50, 50, 7)));
        Assert.AreEqual(7, results.Count(x => x == 0));
        Assert.AreEqual("7", (string?)await a.StringGetAsync(keys[2]));
        var first = (long)await a.StringGetAsync(keys[1]);
        var second = (long)await a.StringGetAsync(keys[4]);
        Assert.AreEqual(7L, first + second);
    });

    [TestMethod]
    public Task ExpiringShortWindowDoesNotResetDailyCounters() => Run(async (a, b, keys) =>
    {
        Assert.AreEqual(0L, await Reserve(a, keys[..3], 1, 2, 2, 100));
        // Wait for actual TTL expiry; bounded polling avoids assuming exact scheduler timing.
        for (var i = 0; i < 40 && await a.KeyExistsAsync(keys[0]); i++) await Task.Delay(50);
        Assert.IsFalse(await a.KeyExistsAsync(keys[0]));
        Assert.AreEqual(0L, await Reserve(b, keys[..3], 1, 2, 2, 100));
        for (var i = 0; i < 40 && await a.KeyExistsAsync(keys[0]); i++) await Task.Delay(50);
        Assert.IsTrue(await Reserve(a, keys[..3], 1, 2, 2, 100) > 0);
        Assert.AreEqual("2", (string?)await a.StringGetAsync(keys[2]));
    });

    [TestMethod]
    public Task CorruptStateFailsWithoutIncrementingOtherCounters() => Run(async (a, b, keys) =>
    {
        await a.StringSetAsync(keys[2], "invalid", TimeSpan.FromMinutes(1));
        Assert.AreEqual(-1L, await Reserve(b, keys[..3], 5, 50, 100));
        Assert.IsFalse(await a.KeyExistsAsync(keys[0]));
        Assert.IsFalse(await a.KeyExistsAsync(keys[1]));
    });

    private static async Task<long> Reserve(IDatabase db, RedisKey[] keys, int minute, int day, int firm, int minuteMs = 60000) =>
        (long)await db.ScriptEvaluateAsync(RedisAiReportQuotaStore.Script, keys,
            new RedisValue[] { minute, day, firm, minuteMs, 86400000, 86400000 }).WaitAsync(TimeSpan.FromSeconds(5));

    private static async Task Run(Func<IDatabase, IDatabase, RedisKey[], Task> test)
    {
        if (Environment.GetEnvironmentVariable("ECSPROS_ACCEPTANCE_AI_QUOTA_DISPOSABLE_REDIS") != "1")
            Assert.Inconclusive("Dedicated disposable localhost:16379 Redis and explicit write opt-in required.");
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = true, ConnectTimeout = 2000, AsyncTimeout = 2000,
            ConnectRetry = 0, AllowAdmin = false
        };
        options.EndPoints.Add("127.0.0.1", 16379);
        using var first = await ConnectionMultiplexer.ConnectAsync(options);
        using var second = await ConnectionMultiplexer.ConnectAsync(options);
        var prefix = $"ECSPros:{{ai-quota-acceptance-{Guid.NewGuid():N}}}:";
        RedisKey[] keys = { prefix + "u1:minute", prefix + "u1:day", prefix + "firm:day", prefix + "u2:minute", prefix + "u2:day" };
        try { await test(first.GetDatabase(), second.GetDatabase(), keys); }
        finally
        {
            // Only these five newly generated test keys; no scan, wildcard, FLUSHDB or shared keys.
            await first.GetDatabase().KeyDeleteAsync(keys).WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
