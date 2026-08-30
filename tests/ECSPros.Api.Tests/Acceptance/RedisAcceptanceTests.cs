using StackExchange.Redis;

namespace ECSPros.Api.Tests.Acceptance;

[TestClass]
[TestCategory("Acceptance")]
[TestCategory("Redis")]
[DoNotParallelize]
public sealed class RedisAcceptanceTests
{
    [TestMethod]
    public async Task IkiBaglanti_StateVePubSubAkisiniPaylasir()
    {
        var connectionString = AcceptanceTestEnvironment.RequireRedis();

        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = Math.Min(options.ConnectTimeout, 5000);
        options.SyncTimeout = Math.Min(options.SyncTimeout, 5000);
        options.ClientName = "ECSPros-Acceptance-Tests";
        await using var first = await ConnectionMultiplexer.ConnectAsync(options);
        await using var second = await ConnectionMultiplexer.ConnectAsync(options);
        var suffix = Guid.NewGuid().ToString("N");
        var key = $"ecspros:acceptance:{suffix}";
        var channel = RedisChannel.Literal($"ecspros:acceptance:pubsub:{suffix}");
        var expected = $"message-{suffix}";
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var db = first.GetDatabase();
        var subscriber = second.GetSubscriber();

        try
        {
            Assert.IsTrue(await db.StringSetAsync(key, expected, TimeSpan.FromMinutes(1)));
            Assert.AreEqual(expected, (string?)await second.GetDatabase().StringGetAsync(key),
                "İkinci Redis bağlantısı state değerini okuyamadı.");

            await subscriber.SubscribeAsync(channel, (_, value) => received.TrySetResult(value.ToString()));
            Assert.AreEqual(1L, await first.GetSubscriber().PublishAsync(channel, expected),
                "Acceptance kanalında beklenen subscriber bulunamadı.");
            var actual = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.AreEqual(expected, actual, "Redis pub/sub mesajı ikinci bağlantıya ulaşmadı.");
        }
        finally
        {
            try { await subscriber.UnsubscribeAsync(channel); }
            catch { }
            try { await db.KeyDeleteAsync(key); }
            catch { }
        }
    }
}
