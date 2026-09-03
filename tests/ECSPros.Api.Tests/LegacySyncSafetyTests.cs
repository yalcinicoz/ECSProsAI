using ECSPros.Api.Services.Legacy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class LegacySyncSafetyTests
{
    [TestMethod]
    public async Task PriceVeStockKapaliyken_HicbirVeriTabaniBaglantisiAcmaz()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Legacy:MySqlConnection"] = "Server=127.0.0.1;Port=1;Database=forbidden;User Id=forbidden",
                ["Legacy:Sync:DryRun"] = "true",
                ["Legacy:Sync:Prices"] = "false",
                ["Legacy:Sync:Stock"] = "false"
            })
            .Build();

        await using var dataSource = NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Database=forbidden;Username=forbidden;Timeout=1");
        var service = new LegacySyncService(
            dataSource,
            configuration,
            NullLogger<LegacySyncService>.Instance);

        var report = await service.SyncPriceAndStockAsync(CancellationToken.None);

        Assert.IsTrue(report.Success, report.Error);
        Assert.IsTrue(report.DryRun);
        Assert.AreEqual(0, report.Changed);
        StringAssert.Contains(report.Detail, "dilim kapalı");
    }
}
