using Npgsql;

namespace ECSPros.Api.Services;

/// <summary>
/// Aynı periyodik işin birden fazla Worker node'unda eşzamanlı çalışmasını PostgreSQL
/// session advisory lock ile engeller. Handle yaşadığı sürece fiziksel bağlantı tutulur;
/// normal bitişte açıkça unlock edilir, process/VM kaybında PostgreSQL bağlantıyı kapatıp
/// kilidi otomatik bırakır. Beklemez: başka node işi aldıysa bu tur atlanır.
/// </summary>
public sealed class DistributedWorkerLock(NpgsqlDataSource dataSource)
{
    /// <summary>İşi mutlaka çalıştırması gereken istekler için lock boşalana kadar bekler.</summary>
    public async Task<IAsyncDisposable> AcquireAsync(string jobName, CancellationToken ct)
    {
        var connection = await dataSource.OpenConnectionAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand(
                "SELECT pg_advisory_lock(hashtextextended(@jobName, 8317))", connection);
            command.Parameters.AddWithValue("jobName", jobName);
            await command.ExecuteScalarAsync(ct);
            return new Handle(connection, jobName);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task<IAsyncDisposable?> TryAcquireAsync(string jobName, CancellationToken ct)
    {
        var connection = await dataSource.OpenConnectionAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand(
                "SELECT pg_try_advisory_lock(hashtextextended(@jobName, 8317))", connection);
            command.Parameters.AddWithValue("jobName", jobName);
            var acquired = (bool)(await command.ExecuteScalarAsync(ct) ?? false);
            if (!acquired)
            {
                await connection.DisposeAsync();
                return null;
            }

            return new Handle(connection, jobName);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class Handle(NpgsqlConnection connection, string jobName) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new NpgsqlCommand(
                    "SELECT pg_advisory_unlock(hashtextextended(@jobName, 8317))", connection);
                command.Parameters.AddWithValue("jobName", jobName);
                await command.ExecuteScalarAsync(CancellationToken.None);
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
