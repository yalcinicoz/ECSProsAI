using System.Data.Common;
using MySql.Data.MySqlClient;

namespace ECSPros.Api.Services.LegacyImport;

public sealed record LegacyInvoiceSourceRow(
    int Id,
    int OrderId,
    DateTime? InvoiceDate,
    string InvoiceNumber,
    string Ettn,
    string SourcePlatformInvoiceNumber,
    string DestinationPlatformInvoiceNumber,
    string ShippingBarcode,
    string CourierTrackingNumber,
    int CourierId,
    int Desi,
    bool IsSentToCourier,
    string ShippingResponse,
    bool IsSentToIntegrator,
    string ShippingRecordId,
    int ShippingStatus,
    bool SendToCourier,
    bool SendCourierSms,
    bool IsEArchive,
    bool SendInvoice,
    bool PlatformIntegrated,
    string InvoiceUrl,
    string InvoiceType);

public interface ILegacyInvoiceReader
{
    Task<IReadOnlyList<LegacyInvoiceSourceRow>> ReadAsync(int platformId, CancellationToken ct);
}

/// <summary>Production MySQL fatura üst kayıtlarını READ ONLY snapshot olarak okur.</summary>
public sealed class LegacyInvoiceReader(ILegacyReadSource source) : ILegacyInvoiceReader
{
    public Task<IReadOnlyList<LegacyInvoiceSourceRow>> ReadAsync(int platformId, CancellationToken ct) =>
        source.ExecuteReadAsync<IReadOnlyList<LegacyInvoiceSourceRow>>(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                SELECT Id, orderId,
                       CASE WHEN invoiceDate IS NULL OR YEAR(invoiceDate)=0 THEN NULL
                            ELSE TIMESTAMP(invoiceDate, invoiceTime) END,
                       invoiceNumber, ettn, sourcePlatformInvoiceNumber, destPlatformInvoiceNumber,
                       shippingBarcode, courierTrackingNumber, courierId, desi, isSentToCourier,
                       shippingResponse, isSentToIntegrator, shippingRecordId, shippingStatus,
                       kargoGonder, kargoSMSGonder, isEArsiv, faturaGonder,
                       platformEntegrasyonuYapildi, invoiceUrl, invoiceType
                  FROM opinvoices
                 WHERE platformId = @platformId
                 ORDER BY Id
                """;
            command.Parameters.AddWithValue("@platformId", platformId);
            await using var reader = await command.ExecuteReaderAsync(token);
            var rows = new List<LegacyInvoiceSourceRow>();
            while (await reader.ReadAsync(token))
            {
                rows.Add(new(
                    reader.GetInt32(0), Int(reader, 1), Date(reader, 2), Text(reader, 3), Text(reader, 4),
                    Text(reader, 5), Text(reader, 6), Text(reader, 7), Text(reader, 8), Int(reader, 9),
                    Int(reader, 10), Bool(reader, 11), Text(reader, 12), Bool(reader, 13), Text(reader, 14),
                    Int(reader, 15), Bool(reader, 16), Bool(reader, 17), Bool(reader, 18), Bool(reader, 19),
                    Bool(reader, 20), Text(reader, 21), Text(reader, 22)));
            }
            return rows;
        }, ct);

    private static string Text(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal))?.Trim() ?? string.Empty;
    private static int Int(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
    private static bool Bool(DbDataReader reader, int ordinal) =>
        !reader.IsDBNull(ordinal) && Convert.ToInt64(reader.GetValue(ordinal)) != 0;
    private static DateTime? Date(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
}
