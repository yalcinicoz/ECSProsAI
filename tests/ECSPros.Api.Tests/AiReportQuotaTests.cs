using ECSPros.Api.Services.AiReporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class AiReportQuotaTests
{
    private static Dictionary<string, string?> Config() => new()
    {
        ["AiReporting:Quota:Scope"] = "test",
        ["AiReporting:Quota:UserPerMinute"] = "3",
        ["AiReporting:Quota:UserPer24Hours"] = "20",
        ["AiReporting:Quota:FirmPer24Hours"] = "100"
    };
    private static AiReportQuota Quota(Dictionary<string, string?> config, IAiReportQuotaStore store) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(config).Build(), store);

    [TestMethod]
    [DataRow("AiReporting:Quota:Scope", "bad{scope}")]
    [DataRow("AiReporting:Quota:UserPerMinute", "0")]
    [DataRow("AiReporting:Quota:UserPer24Hours", "-1")]
    [DataRow("AiReporting:Quota:FirmPer24Hours", "1000001")]
    public async Task InvalidSettingsNeverReachStore(string key, string value)
    {
        var config = Config(); config[key] = value;
        var store = new Store();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Quota(config, store).ReserveAsync(Guid.NewGuid(), Guid.NewGuid(), default));
        Assert.AreEqual(0, store.Calls);
    }

    [TestMethod]
    public async Task MissingSettingsFailClosed()
    {
        var store = new Store();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Quota(new(), store).ReserveAsync(Guid.NewGuid(), Guid.NewGuid(), default));
        Assert.AreEqual(0, store.Calls);
    }

    [TestMethod]
    public async Task BothScopesAndLimitsArePassedInOneReservation_AndDenialPreserved()
    {
        var store = new Store(); var user = Guid.NewGuid(); var firm = Guid.NewGuid();
        var result = await Quota(Config(), store).ReserveAsync(user, firm, default);
        Assert.IsFalse(result.Allowed); Assert.AreEqual(60, result.RetryAfterSeconds);
        Assert.AreEqual((user, firm), store.Ids); Assert.AreEqual(1, store.Calls);
    }

    [TestMethod]
    public async Task MissingRedisHasNoMemoryFallback()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => new RedisAiReportQuotaStore(services)
            .ReserveAsync("test", Guid.NewGuid(), Guid.NewGuid(), 3, 20, 100, default));
    }

    [TestMethod]
    public async Task CancelledRequestNeverReserves()
    {
        var store = new Store();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => Quota(Config(), store)
            .ReserveAsync(Guid.NewGuid(), Guid.NewGuid(), new CancellationToken(true)));
        Assert.AreEqual(0, store.Calls);
    }

    private sealed class Store : IAiReportQuotaStore
    {
        public int Calls;
        public (Guid, Guid) Ids;
        public Task<AiQuotaDecision> ReserveAsync(string scope, Guid userId, Guid firmId, int perMinute, int userDaily, int firmDaily, CancellationToken ct)
        {
            Calls++; Ids = (userId, firmId);
            Assert.AreEqual("test", scope);
            Assert.AreEqual((3, 20, 100), (perMinute, userDaily, firmDaily));
            return Task.FromResult(new AiQuotaDecision(false, 60));
        }
    }
}
