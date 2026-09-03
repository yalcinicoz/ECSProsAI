using System.Data;
using MySql.Data.MySqlClient;

namespace ECSPros.Api.Services.LegacyImport;

/// <summary>
/// Production MySQL'i yalnız READ ONLY transaction ile okur. Her işlem rollback edilir; bağlantı
/// hesabının ayrıca SELECT-only olması deployment ön koşuludur.
/// </summary>
public sealed class MySqlLegacyReadSource(LegacyReadImportOptions options) : ILegacyReadSource
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.ConnectionString);

    public Task<LegacySourceProbe> ProbeAsync(int platformId, CancellationToken ct)
    {
        if (platformId <= 0) throw new ArgumentOutOfRangeException(nameof(platformId));
        return ExecuteReadAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandTimeout = options.CommandTimeoutSeconds;
            command.CommandText = """
                SELECT DATABASE(), VERSION(), @@session.transaction_read_only,
                       (SELECT COUNT(*) FROM webmembers WHERE platformId = @platformId),
                       (SELECT COUNT(*) FROM oporders WHERE platformId = @platformId),
                       (SELECT COUNT(*) FROM opinvoices WHERE platformId = @platformId),
                       (SELECT COUNT(*)
                          FROM opiadesiparisler r
                          JOIN oporders o ON o.Id = r.orderId
                         WHERE o.platformId = @platformId)
                """;
            command.Parameters.AddWithValue("@platformId", platformId);
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, token);
            if (!await reader.ReadAsync(token))
                throw new InvalidOperationException("Legacy MySQL probe sonuç döndürmedi.");
            if (reader.GetInt32(2) != 1)
                throw new InvalidOperationException("Legacy MySQL oturumu READ ONLY olarak doğrulanamadı.");

            return new LegacySourceProbe(
                reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                platformId,
                reader.GetInt64(3),
                reader.GetInt64(4),
                reader.GetInt64(5),
                reader.GetInt64(6));
        }, ct);
    }

    public async Task<T> ExecuteReadAsync<T>(
        Func<MySqlConnection, MySqlTransaction, CancellationToken, Task<T>> read,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(read);
        if (!IsConfigured)
            throw new InvalidOperationException("LegacyReadImport ConnectionString yapılandırılmamış.");

        var builder = new MySqlConnectionStringBuilder(options.ConnectionString)
        {
            DefaultCommandTimeout = (uint)options.CommandTimeoutSeconds,
            // READ ONLY session ayarının başka bir connection pool tüketicisine taşınmasını önler.
            // Geçici import düşük frekanslıdır; güvenlik bağlantı yeniden kullanımından önceliklidir.
            Pooling = false
        };
        await using var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync(ct);

        await using (var readOnly = connection.CreateCommand())
        {
            readOnly.CommandTimeout = options.CommandTimeoutSeconds;
            readOnly.CommandText = "SET SESSION TRANSACTION READ ONLY";
            await readOnly.ExecuteNonQueryAsync(ct);
        }

        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        try
        {
            return await read(connection, transaction, ct);
        }
        finally
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch when (ct.IsCancellationRequested) { }
        }
    }
}
