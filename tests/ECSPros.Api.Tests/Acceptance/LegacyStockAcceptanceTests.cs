using ECSPros.Api.Services.LegacyImport;
using ECSPros.Api.Services.LegacyStock;
using ECSPros.Shared.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace ECSPros.Api.Tests.Acceptance;

[TestClass]
[TestCategory("Acceptance")]
[DoNotParallelize]
public sealed class LegacyStockAcceptanceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LegacyMySql_StockOnly_DryRun_HedefiDegistirmez()
    {
        if (!AcceptanceTestEnvironment.GetBoolean(
                "ECSPROS_ACCEPTANCE_LEGACY_STOCK_DRYRUN",
                "Acceptance:LegacyStock:AllowTargetDryRun"))
        {
            Assert.Inconclusive(
                "Stock-only hedef testi için ECSPROS_ACCEPTANCE_LEGACY_STOCK_DRYRUN=true açıkça verilmelidir.");
        }

        var targetConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_TARGET",
            "ConnectionStrings:DefaultConnection",
            "hedef PostgreSQL bağlantısı");
        var legacyConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_LEGACY_MYSQL",
            "LegacyReadImport:ConnectionString",
            "legacy MySQL bağlantısı");

        await using var dataSource = NpgsqlDataSource.Create(targetConnection);
        var source = new MySqlLegacyReadSource(new LegacyReadImportOptions
        {
            ConnectionString = legacyConnection,
            CommandTimeoutSeconds = 300
        });
        var options = new LegacyStockSyncOptions
        {
            DryRun = true,
            MinimumSourceRows = 1
        };
        var service = new LegacyStockSyncService(
            dataSource,
            source,
            options,
            new NoOpCacheBustPublisher(),
            NullLogger<LegacyStockSyncService>.Instance);

        var before = await ReadStockFingerprintAsync(dataSource);
        var report = await service.SyncAsync(CancellationToken.None);
        var after = await ReadStockFingerprintAsync(dataSource);

        Assert.IsTrue(report.Success, report.Error);
        Assert.IsTrue(report.DryRun);
        StringAssert.Contains(report.Detail, "kaynakSatır=");
        Assert.AreEqual(before, after, "Dry-run hedef stok kayıtlarını değiştirdi.");

        TestContext.WriteLine(
            $"changed={report.Changed}; rows={before.Rows}; quantity={before.Quantity}; durationMs={report.DurationMs}");
    }

    private static async Task<(long Rows, decimal Quantity)> ReadStockFingerprintAsync(
        NpgsqlDataSource dataSource)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT count(*), COALESCE(sum("Quantity"), 0)
            FROM inventory.inv_stocks
            """);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.IsTrue(await reader.ReadAsync());
        return (reader.GetInt64(0), reader.GetDecimal(1));
    }

    private sealed class NoOpCacheBustPublisher : ICacheBustPublisher
    {
        public void Bust(string anahtar) { }
    }
}
