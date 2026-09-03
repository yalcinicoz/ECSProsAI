using MySql.Data.MySqlClient;

namespace ECSPros.Api.Services.LegacyImport;

public interface ILegacyReadSource
{
    bool IsConfigured { get; }
    Task<LegacySourceProbe> ProbeAsync(int platformId, CancellationToken ct);

    /// <summary>
    /// Her okuma işini MySQL READ ONLY transaction içinde çalıştırır ve sonuç ne olursa olsun rollback eder.
    /// Delegate transaction dışında komut çalıştırmamalıdır.
    /// </summary>
    Task<T> ExecuteReadAsync<T>(
        Func<MySqlConnection, MySqlTransaction, CancellationToken, Task<T>> read,
        CancellationToken ct);
}

public sealed record LegacySourceProbe(
    string Database,
    string Version,
    int PlatformId,
    long MemberCount,
    long OrderCount,
    long InvoiceCount,
    long ReturnCount);
