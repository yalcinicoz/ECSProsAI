using System.Data.Common;
using MySql.Data.MySqlClient;

namespace ECSPros.Api.Services.LegacyImport;

public sealed record LegacyReturnSourceRow(
    int Id, int OrderId, DateTime? ReturnDate, int RawType, int RawStatus,
    decimal ReturnAmount, DateTime? CreatedAt, decimal PaidToMemberAmount,
    DateTime? PaidToMemberAt, int RawRefundMethod, bool Integrated);

public sealed record LegacyReturnItemSourceRow(
    int Id, int ReturnId, int OrderLineId, int ReasonId, string Reason,
    int RawCustomerRequest, decimal Amount, int OrderLineQuantity);

public sealed record LegacyReturnLogSourceRow(
    int Id, int OrderId, int OrderLineId, int RawStatus, DateTime? CreatedAt);

public sealed record LegacyReturnSnapshot(
    IReadOnlyList<LegacyReturnSourceRow> Returns,
    IReadOnlyList<LegacyReturnItemSourceRow> Items,
    IReadOnlyList<LegacyReturnLogSourceRow> Logs);

public interface ILegacyReturnReader
{
    Task<LegacyReturnSnapshot> ReadAsync(int platformId, CancellationToken ct);
}

/// <summary>Legacy iade aggregate'ini tek repeatable-read READ ONLY transaction içinde okur.</summary>
public sealed class LegacyReturnReader(ILegacyReadSource source) : ILegacyReturnReader
{
    public Task<LegacyReturnSnapshot> ReadAsync(int platformId, CancellationToken ct) =>
        source.ExecuteReadAsync<LegacyReturnSnapshot>(async (connection, transaction, token) =>
        {
            var returns = await ReadReturnsAsync(connection, transaction, platformId, token);
            var items = await ReadItemsAsync(connection, transaction, platformId, token);
            var logs = await ReadLogsAsync(connection, transaction, platformId, token);
            return new(returns, items, logs);
        }, ct);

    private static async Task<IReadOnlyList<LegacyReturnSourceRow>> ReadReturnsAsync(
        MySqlConnection connection, MySqlTransaction transaction, int platformId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT r.Id,r.orderId,
                   CASE WHEN r.iadeTarihi IS NULL OR YEAR(r.iadeTarihi)=0 THEN NULL ELSE r.iadeTarihi END,
                   r.iadeTipi,r.durumu,r.iadeTutari,
                   CASE WHEN r.kayitZamani IS NULL OR YEAR(r.kayitZamani)=0 THEN NULL ELSE r.kayitZamani END,
                   r.uyeyeOdenenTutar,
                   CASE WHEN r.uyeyeOdemeTarihi IS NULL OR YEAR(r.uyeyeOdemeTarihi)=0 THEN NULL ELSE r.uyeyeOdemeTarihi END,
                   r.uyeyeOdemeTipi,r.entegreEdildi
              FROM opiadesiparisler r
              JOIN oporders o ON o.Id=r.orderId
             WHERE o.platformId=@platformId
             ORDER BY r.Id
            """;
        command.Parameters.AddWithValue("@platformId", platformId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<LegacyReturnSourceRow>();
        while (await reader.ReadAsync(ct))
            rows.Add(new(
                reader.GetInt32(0), Int(reader, 1), Date(reader, 2), Int(reader, 3), Int(reader, 4),
                Decimal(reader, 5), Date(reader, 6), Decimal(reader, 7), Date(reader, 8),
                Int(reader, 9), Bool(reader, 10)));
        return rows;
    }

    private static async Task<IReadOnlyList<LegacyReturnItemSourceRow>> ReadItemsAsync(
        MySqlConnection connection, MySqlTransaction transaction, int platformId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT i.Id,i.iadeSiparislerId,i.orderLineId,i.nedeni,n.aciklama,
                   i.musteriIstegi,i.tutari,l.quantity
              FROM opiadeurunler i
              JOIN opiadesiparisler r ON r.Id=i.iadeSiparislerId
              JOIN oporders o ON o.Id=r.orderId
              JOIN oporderlines l ON l.Id=i.orderLineId AND l.orderId=r.orderId
              LEFT JOIN dfiadenedenleri n ON n.Id=i.nedeni
             WHERE o.platformId=@platformId
             ORDER BY i.iadeSiparislerId,i.Id
            """;
        command.Parameters.AddWithValue("@platformId", platformId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<LegacyReturnItemSourceRow>();
        while (await reader.ReadAsync(ct))
            rows.Add(new(
                reader.GetInt32(0), Int(reader, 1), Int(reader, 2), Int(reader, 3), Text(reader, 4),
                Int(reader, 5), Decimal(reader, 6), Int(reader, 7)));
        return rows;
    }

    private static async Task<IReadOnlyList<LegacyReturnLogSourceRow>> ReadLogsAsync(
        MySqlConnection connection, MySqlTransaction transaction, int platformId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT l.Id,l.siparisId,l.siparisSatirId,l.durum,
                   CASE WHEN l.islemZamani IS NULL OR YEAR(l.islemZamani)=0 THEN NULL ELSE l.islemZamani END
              FROM opiadelog l
              JOIN oporders o ON o.Id=l.siparisId
             WHERE o.platformId=@platformId
             ORDER BY l.Id
            """;
        command.Parameters.AddWithValue("@platformId", platformId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<LegacyReturnLogSourceRow>();
        while (await reader.ReadAsync(ct))
            rows.Add(new(reader.GetInt32(0), Int(reader, 1), Int(reader, 2), Int(reader, 3), Date(reader, 4)));
        return rows;
    }

    private static string Text(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal))?.Trim() ?? string.Empty;
    private static int Int(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
    private static decimal Decimal(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(reader.GetValue(ordinal));
    private static bool Bool(DbDataReader reader, int ordinal) => !reader.IsDBNull(ordinal) && Convert.ToInt64(reader.GetValue(ordinal)) != 0;
    private static DateTime? Date(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
}
