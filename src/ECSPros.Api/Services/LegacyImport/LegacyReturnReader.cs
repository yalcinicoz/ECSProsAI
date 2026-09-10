using System.Data.Common;
using MySql.Data.MySqlClient;

namespace ECSPros.Api.Services.LegacyImport;

/// <summary>
/// <paramref name="PaidToMemberAmount"/>/<paramref name="PaidToMemberAt"/>: <c>webuyeparalari</c> (musteriIstegi=2,
/// odemeTarihi dolu) toplamı — opiadesiparisler.uyeyeOdeme* kolonları eski kodda hiç yazılmadığından KULLANILMAZ.
/// <paramref name="CreditToMemberAmount"/>: üyeye açılmış para iadesi alacağı (ödenmiş+ödenmemiş);
/// <paramref name="ExchangeCreditAmount"/>: Değişim (musteriIstegi=1) bakiye alacağı.
/// </summary>
public sealed record LegacyReturnSourceRow(
    int Id, int OrderId, DateTime? ReturnDate, int RawType, int RawStatus,
    decimal ReturnAmount, DateTime? CreatedAt, decimal PaidToMemberAmount,
    DateTime? PaidToMemberAt, int RawRefundMethod, bool Integrated,
    decimal CreditToMemberAmount = 0m, decimal ExchangeCreditAmount = 0m, int OrderPaymentTypeId = 0);

/// <summary><paramref name="Barcode"/>: oporderlines.barcode — yeni siteden eskiye yazılan (outbox) siparişlerin
/// kalemlerinde LegacyOrderLineId yoktur; eşleşme varyant barkoduyla yapılır (outbox'ın kendi anahtarı).</summary>
public sealed record LegacyReturnItemSourceRow(
    int Id, int ReturnId, int OrderLineId, int ReasonId, string Reason,
    int RawCustomerRequest, decimal Amount, int OrderLineQuantity, string Barcode = "");

public sealed record LegacyReturnLogSourceRow(
    int Id, int OrderId, int OrderLineId, int RawStatus, DateTime? CreatedAt);

public sealed record LegacyReturnSnapshot(
    IReadOnlyList<LegacyReturnSourceRow> Returns,
    IReadOnlyList<LegacyReturnItemSourceRow> Items,
    IReadOnlyList<LegacyReturnLogSourceRow> Logs);

public interface ILegacyReturnReader
{
    Task<LegacyReturnSnapshot> ReadAsync(int platformId, CancellationToken ct);
    /// <summary>Yalnız verilen eski sipariş Id'lerinin iadeleri (canlı senkron: eskiye bağlı siparişler).</summary>
    Task<LegacyReturnSnapshot> ReadForOrdersAsync(IReadOnlyCollection<int> legacyOrderIds, CancellationToken ct);
}

/// <summary>Legacy iade aggregate'ini tek repeatable-read READ ONLY transaction içinde okur.</summary>
public sealed class LegacyReturnReader(ILegacyReadSource source) : ILegacyReturnReader
{
    public Task<LegacyReturnSnapshot> ReadAsync(int platformId, CancellationToken ct) =>
        ReadCoreAsync("o.platformId=@platformId", cmd => cmd.Parameters.AddWithValue("@platformId", platformId), ct);

    public Task<LegacyReturnSnapshot> ReadForOrdersAsync(IReadOnlyCollection<int> legacyOrderIds, CancellationToken ct)
    {
        if (legacyOrderIds.Count == 0)
            return Task.FromResult(new LegacyReturnSnapshot([], [], []));
        // Id listesi tamsayı — parametre yerine doğrudan yazılır (SQL enjeksiyonu söz konusu değil).
        var liste = string.Join(",", legacyOrderIds.Distinct());
        return ReadCoreAsync($"o.Id IN ({liste})", _ => { }, ct);
    }

    private Task<LegacyReturnSnapshot> ReadCoreAsync(string orderFilter, Action<MySqlCommand> bind, CancellationToken ct) =>
        source.ExecuteReadAsync<LegacyReturnSnapshot>(async (connection, transaction, token) =>
        {
            var returns = await ReadReturnsAsync(connection, transaction, orderFilter, bind, token);
            var items = await ReadItemsAsync(connection, transaction, orderFilter, bind, token);
            var logs = await ReadLogsAsync(connection, transaction, orderFilter, bind, token);
            return new(returns, items, logs);
        }, ct);

    private static async Task<IReadOnlyList<LegacyReturnSourceRow>> ReadReturnsAsync(
        MySqlConnection connection, MySqlTransaction transaction, string orderFilter, Action<MySqlCommand> bind, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // Üyeye ödeme bilgisi webuyeparalari'ndan (2026-09-10): musteriIstegi=2 para iadesi alacağı (odemeTarihi dolu =
        // ödendi), musteriIstegi=1 değişim bakiyesi. opiadesiparisler.uyeyeOdeme* kolonları eski kodda hiç yazılmıyor.
        command.CommandText = $"""
            SELECT r.Id,r.orderId,
                   CASE WHEN r.iadeTarihi IS NULL OR YEAR(r.iadeTarihi)=0 THEN NULL ELSE r.iadeTarihi END,
                   r.iadeTipi,r.durumu,r.iadeTutari,
                   CASE WHEN r.kayitZamani IS NULL OR YEAR(r.kayitZamani)=0 THEN NULL ELSE r.kayitZamani END,
                   COALESCE(w.odenen,0),
                   w.odemeTarihi,
                   COALESCE(w.odemeTipi,0),r.entegreEdildi,
                   COALESCE(w.alacak,0),COALESCE(w.degisim,0),COALESCE(o.paymentTypeId,0)
              FROM opiadesiparisler r
              JOIN oporders o ON o.Id=r.orderId
              LEFT JOIN (
                    SELECT iadeSiparislerId,
                           SUM(CASE WHEN musteriIstegi=2 AND odemeTarihi IS NOT NULL THEN alacak-borc ELSE 0 END) odenen,
                           SUM(CASE WHEN musteriIstegi=2 THEN alacak-borc ELSE 0 END) alacak,
                           SUM(CASE WHEN musteriIstegi=1 THEN alacak-borc ELSE 0 END) degisim,
                           MAX(CASE WHEN musteriIstegi=2 THEN odemeTarihi END) odemeTarihi,
                           MAX(CASE WHEN musteriIstegi=2 THEN odemeTipi END) odemeTipi
                      FROM webuyeparalari
                     WHERE iadeSiparislerId IS NOT NULL AND iadeSiparislerId>0
                     GROUP BY iadeSiparislerId) w ON w.iadeSiparislerId=r.Id
             WHERE {orderFilter}
             ORDER BY r.Id
            """;
        bind(command);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<LegacyReturnSourceRow>();
        while (await reader.ReadAsync(ct))
            rows.Add(new(
                reader.GetInt32(0), Int(reader, 1), Date(reader, 2), Int(reader, 3), Int(reader, 4),
                Decimal(reader, 5), Date(reader, 6), Decimal(reader, 7), Date(reader, 8),
                Int(reader, 9), Bool(reader, 10), Decimal(reader, 11), Decimal(reader, 12), Int(reader, 13)));
        return rows;
    }

    private static async Task<IReadOnlyList<LegacyReturnItemSourceRow>> ReadItemsAsync(
        MySqlConnection connection, MySqlTransaction transaction, string orderFilter, Action<MySqlCommand> bind, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT i.Id,i.iadeSiparislerId,i.orderLineId,i.nedeni,n.aciklama,
                   i.musteriIstegi,i.tutari,l.quantity,l.barcode
              FROM opiadeurunler i
              JOIN opiadesiparisler r ON r.Id=i.iadeSiparislerId
              JOIN oporders o ON o.Id=r.orderId
              JOIN oporderlines l ON l.Id=i.orderLineId AND l.orderId=r.orderId
              LEFT JOIN dfiadenedenleri n ON n.Id=i.nedeni
             WHERE {orderFilter}
             ORDER BY i.iadeSiparislerId,i.Id
            """;
        bind(command);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<LegacyReturnItemSourceRow>();
        while (await reader.ReadAsync(ct))
            rows.Add(new(
                reader.GetInt32(0), Int(reader, 1), Int(reader, 2), Int(reader, 3), Text(reader, 4),
                Int(reader, 5), Decimal(reader, 6), Int(reader, 7), Text(reader, 8)));
        return rows;
    }

    private static async Task<IReadOnlyList<LegacyReturnLogSourceRow>> ReadLogsAsync(
        MySqlConnection connection, MySqlTransaction transaction, string orderFilter, Action<MySqlCommand> bind, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT l.Id,l.siparisId,l.siparisSatirId,l.durum,
                   CASE WHEN l.islemZamani IS NULL OR YEAR(l.islemZamani)=0 THEN NULL ELSE l.islemZamani END
              FROM opiadelog l
              JOIN oporders o ON o.Id=l.siparisId
             WHERE {orderFilter}
             ORDER BY l.Id
            """;
        bind(command);
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
