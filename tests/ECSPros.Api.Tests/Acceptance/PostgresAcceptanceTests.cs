using Npgsql;

namespace ECSPros.Api.Tests.Acceptance;

[TestClass]
[TestCategory("Acceptance")]
[DoNotParallelize]
public sealed class PostgresAcceptanceTests
{
    [TestMethod]
    public async Task ReadinessTarget_YazilabilirPrimarydir()
    {
        var connectionString = AcceptanceTestEnvironment.RequirePostgres(requiresWrite: false);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT NOT pg_is_in_recovery()", connection);

        Assert.AreEqual(true, await command.ExecuteScalarAsync(),
            "Acceptance bağlantısı yazılabilir primary yerine standby/recovery node'una ulaştı.");
    }

    [TestMethod]
    public async Task FeedLease_MigrationRegresyonuVeEszamanliClaimGecer()
    {
        var connectionString = AcceptanceTestEnvironment.RequirePostgres(requiresWrite: true);

        await RunRollbackRegressionScriptAsync(connectionString);
        await RunConcurrentClaimScenarioAsync(connectionString);
        await RunRetryLimitScenarioAsync(connectionString);
    }

    private static async Task RunRetryLimitScenarioAsync(string connectionString)
    {
        const int maxAttempts = 5;
        var jobId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        try
        {
            await using (var insert = new NpgsqlCommand("""
                INSERT INTO integration.feed_jobs
                    ("Id", "FirmPlatformId", "RequestedAt", "Status", "AttemptCount", "LeaseOwner",
                     "LeaseUntil", "CreatedAt", "UpdatedAt", "IsDeleted")
                VALUES (@job, @platform, NOW(), 'processing', @maxAttempts, 'dead-node',
                        NOW() - INTERVAL '1 second', NOW(), NOW(), false)
                """, connection))
            {
                insert.Parameters.AddWithValue("job", jobId);
                insert.Parameters.AddWithValue("platform", platformId);
                insert.Parameters.AddWithValue("maxAttempts", maxAttempts);
                await insert.ExecuteNonQueryAsync();
            }

            await using (var exhaust = new NpgsqlCommand("""
                UPDATE integration.feed_jobs
                SET "Status" = 'failed', "CompletedAt" = NOW(), "LeaseOwner" = NULL,
                    "LeaseUntil" = NULL,
                    "LastError" = COALESCE("LastError", 'Worker kaybı sonrası maksimum deneme sayısına ulaşıldı.'),
                    "UpdatedAt" = NOW()
                WHERE "Id" = @job AND "IsDeleted" = false AND "Status" = 'processing'
                  AND "LeaseUntil" <= NOW() AND "AttemptCount" >= @maxAttempts
                """, connection))
            {
                exhaust.Parameters.AddWithValue("job", jobId);
                exhaust.Parameters.AddWithValue("maxAttempts", maxAttempts);
                Assert.AreEqual(1, await exhaust.ExecuteNonQueryAsync(),
                    "Retry limiti dolan expired lease failed durumuna geçirilmedi.");
            }

            await using var verify = new NpgsqlCommand(
                "SELECT \"Status\", \"LeaseOwner\" IS NULL, \"LeaseUntil\" IS NULL FROM integration.feed_jobs WHERE \"Id\" = @job",
                connection);
            verify.Parameters.AddWithValue("job", jobId);
            await using var reader = await verify.ExecuteReaderAsync();
            Assert.IsTrue(await reader.ReadAsync());
            Assert.AreEqual("failed", reader.GetString(0));
            Assert.IsTrue(reader.GetBoolean(1));
            Assert.IsTrue(reader.GetBoolean(2));
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand(
                "DELETE FROM integration.feed_jobs WHERE \"Id\" = @job", connection);
            cleanup.Parameters.AddWithValue("job", jobId);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    [TestMethod]
    public async Task AdvisoryLock_BaglantiKapanincaDigerNodeTarafindanAlinir()
    {
        // Process/VM kaybında fiziksel socket kapanır. Varsayılan pooling ile CloseAsync bağlantıyı
        // havuza döndürür ve PostgreSQL session'ı yaşamaya devam eder; bu crash semantiği değildir.
        var builder = new NpgsqlConnectionStringBuilder(
            AcceptanceTestEnvironment.RequirePostgres(requiresWrite: false))
        {
            Pooling = false
        };
        var connectionString = builder.ConnectionString;
        var lockKey = (long)Random.Shared.Next(1, int.MaxValue);
        await using var first = new NpgsqlConnection(connectionString);
        await using var second = new NpgsqlConnection(connectionString);
        await first.OpenAsync();
        await second.OpenAsync();

        Assert.IsTrue(await TryAdvisoryLockAsync(first, lockKey), "İlk node advisory lock alamadı.");
        Assert.IsFalse(await TryAdvisoryLockAsync(second, lockKey), "Aktif lock ikinci node tarafından da alındı.");

        await first.CloseAsync();

        Assert.IsTrue(await TryAdvisoryLockAsync(second, lockKey), "Bağlantı kapanınca advisory lock serbest kalmadı.");
        await using var unlock = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", second);
        unlock.Parameters.AddWithValue("key", lockKey);
        Assert.AreEqual(true, await unlock.ExecuteScalarAsync());
    }

    private static async Task RunRollbackRegressionScriptAsync(string connectionString)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Acceptance", "feed-job-lease-regression.sql");
        Assert.IsTrue(File.Exists(scriptPath), "Feed lease regresyon SQL dosyası test çıktısında bulunamadı.");
        var sql = string.Join(Environment.NewLine,
            File.ReadLines(scriptPath).Where(line => !line.TrimStart().StartsWith('\\')));

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 60 };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task RunConcurrentClaimScenarioAsync(string connectionString)
    {
        var jobId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        await using var setup = new NpgsqlConnection(connectionString);
        await setup.OpenAsync();
        try
        {
            await using (var insert = new NpgsqlCommand("""
                INSERT INTO integration.feed_jobs
                    ("Id", "FirmPlatformId", "RequestedAt", "Status", "AttemptCount", "CreatedAt", "UpdatedAt", "IsDeleted")
                VALUES (@job, @platform, NOW(), 'pending', 0, NOW(), NOW(), false)
                """, setup))
            {
                insert.Parameters.AddWithValue("job", jobId);
                insert.Parameters.AddWithValue("platform", platformId);
                await insert.ExecuteNonQueryAsync();
            }

            await using var first = new NpgsqlConnection(connectionString);
            await using var second = new NpgsqlConnection(connectionString);
            await first.OpenAsync();
            await second.OpenAsync();
            var claims = await Task.WhenAll(
                ClaimAsync(first, jobId, "acceptance-node-1"),
                ClaimAsync(second, jobId, "acceptance-node-2"));

            Assert.AreEqual(1, claims.Count(id => id == jobId), "Aynı pending iş birden fazla node tarafından claim edildi.");

            await using (var expire = new NpgsqlCommand(
                "UPDATE integration.feed_jobs SET \"LeaseUntil\" = NOW() - INTERVAL '1 second' WHERE \"Id\" = @job", setup))
            {
                expire.Parameters.AddWithValue("job", jobId);
                Assert.AreEqual(1, await expire.ExecuteNonQueryAsync());
            }

            Assert.AreEqual(jobId, await ClaimAsync(first, jobId, "acceptance-node-recovery"),
                "Süresi dolan lease başka node tarafından devralınamadı.");

            await using (var complete = new NpgsqlCommand("""
                UPDATE integration.feed_jobs
                SET "Status" = 'completed', "CompletedAt" = NOW(), "LeaseOwner" = NULL, "LeaseUntil" = NULL
                WHERE "Id" = @job AND "LeaseOwner" = 'acceptance-node-recovery'
                """, setup))
            {
                complete.Parameters.AddWithValue("job", jobId);
                Assert.AreEqual(1, await complete.ExecuteNonQueryAsync());
            }

            Assert.IsNull(await ClaimAsync(second, jobId, "acceptance-node-after-complete"),
                "Tamamlanan feed işi yeniden claim edildi.");
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand(
                "DELETE FROM integration.feed_jobs WHERE \"Id\" = @job", setup);
            cleanup.Parameters.AddWithValue("job", jobId);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static async Task<Guid?> ClaimAsync(NpgsqlConnection connection, Guid jobId, string owner)
    {
        await using var command = new NpgsqlCommand("""
            WITH candidate AS (
                SELECT "Id"
                FROM integration.feed_jobs
                WHERE "Id" = @job
                  AND "IsDeleted" = false
                  AND ("Status" = 'pending' OR ("Status" = 'processing' AND "LeaseUntil" <= NOW()))
                FOR UPDATE SKIP LOCKED
            )
            UPDATE integration.feed_jobs AS jobs
            SET "Status" = 'processing', "LeaseOwner" = @owner,
                "LeaseUntil" = NOW() + INTERVAL '5 minutes',
                "AttemptCount" = jobs."AttemptCount" + 1,
                "StartedAt" = COALESCE(jobs."StartedAt", NOW()), "UpdatedAt" = NOW()
            FROM candidate
            WHERE jobs."Id" = candidate."Id"
            RETURNING jobs."Id"
            """, connection);
        command.Parameters.AddWithValue("job", jobId);
        command.Parameters.AddWithValue("owner", owner);
        return await command.ExecuteScalarAsync() is Guid claimed ? claimed : null;
    }

    private static async Task<bool> TryAdvisoryLockAsync(NpgsqlConnection connection, long key)
    {
        await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", connection);
        command.Parameters.AddWithValue("key", key);
        return await command.ExecuteScalarAsync() is true;
    }
}
