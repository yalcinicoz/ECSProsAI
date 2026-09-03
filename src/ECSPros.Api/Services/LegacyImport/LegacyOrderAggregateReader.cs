using System.Data.Common;
using MySql.Data.MySqlClient;

namespace ECSPros.Api.Services.LegacyImport;

public sealed record LegacyOrderSourceRow(
    int Id,
    string OrderNumber,
    string SourcePlatformOrderNumber,
    string DestinationPlatformOrderNumber,
    string SourceOrderId,
    string SourceName,
    string RawStatus,
    int PaymentTypeId,
    DateTime? OrderDate,
    int MemberId,
    int ShippingAddressId,
    int InvoiceAddressId,
    string MemberFirstName,
    string MemberLastName,
    string MemberEmail,
    string MemberPhone,
    string Currency,
    decimal ExchangeRate,
    decimal ProductTotal,
    decimal TaxTotal,
    decimal ExpenseTotal,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal OrderTotal,
    decimal PaidTotal,
    decimal PayableAmount,
    string CustomerNote,
    string CourierName,
    string TrackingNumber,
    string InvoiceNumber,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record LegacyOrderAddressSourceRow(
    int Id,
    string AddressLine,
    string PostalCode,
    string NeighborhoodName,
    string DistrictName,
    string CityName,
    string CountryName,
    string ContactFirstName,
    string ContactLastName,
    string ContactPhone,
    string InvoiceTitle,
    string TaxOffice,
    string TaxNumber);

public sealed record LegacyOrderLineSourceRow(
    int Id,
    int OrderId,
    int ProductVariantId,
    string Barcode,
    string ProductCode,
    string ProductName,
    string Color,
    string VariantValue,
    decimal SellingPrice,
    int Quantity,
    decimal DiscountAmount,
    int RawStatus,
    DateTime? CreatedAt);

public sealed record LegacyOrderPaymentSourceRow(
    int Id,
    int OrderId,
    int PaymentTypeId,
    string PaymentTypeCode,
    string PaymentTypeTitle,
    string Description,
    decimal Amount,
    int? InstallmentCount,
    bool IsPaid,
    string GibCode,
    DateTime? CreatedAt);

public sealed record LegacyOrderAggregateSnapshot(
    IReadOnlyList<LegacyOrderSourceRow> Orders,
    IReadOnlyList<LegacyOrderAddressSourceRow> Addresses,
    IReadOnlyList<LegacyOrderLineSourceRow> Lines,
    IReadOnlyList<LegacyOrderPaymentSourceRow> Payments);

public interface ILegacyOrderAggregateReader
{
    Task<LegacyOrderAggregateSnapshot> ReadAsync(int platformId, CancellationToken ct);
}

/// <summary>
/// Production MySQL sipariş aggregate'ini tek repeatable-read READ ONLY transaction içinde okur.
/// Kart numarası ve kart sahibi gibi ödeme sırları sorguya bilerek dahil edilmez.
/// </summary>
public sealed class LegacyOrderAggregateReader(ILegacyReadSource source) : ILegacyOrderAggregateReader
{
    public Task<LegacyOrderAggregateSnapshot> ReadAsync(int platformId, CancellationToken ct) =>
        source.ExecuteReadAsync<LegacyOrderAggregateSnapshot>(async (connection, transaction, token) =>
        {
            var orders = await ReadOrdersAsync(connection, transaction, platformId, token);
            var addresses = await ReadAddressesAsync(connection, transaction, platformId, token);
            var lines = await ReadLinesAsync(connection, transaction, platformId, token);
            var payments = await ReadPaymentsAsync(connection, transaction, platformId, token);
            return new(orders, addresses, lines, payments);
        }, ct);

    private static async Task<IReadOnlyList<LegacyOrderSourceRow>> ReadOrdersAsync(
        MySqlConnection connection, MySqlTransaction transaction, int platformId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Id, orderNumber, sourcePlatformOrderNumber, destPlatformOrderNumber,
                   kaynakSiparisId, siparisKaynagi, orderStatus, paymentTypeId,
                   CASE WHEN orderDate IS NULL OR YEAR(orderDate)=0 THEN NULL
                        ELSE TIMESTAMP(orderDate, orderTime) END,
                   memberId, shippingAddressId, invoiceAddressId,
                   memberFirstName, memberLastName, memberEMail, memberPhone,
                   currency, exchangeRate, productTotal, taxTotal, expenseTotal, subTotal,
                   discountTotal, orderTotal, paidTotal, payableAmount, customerNote,
                   courierName, COALESCE(NULLIF(courierTrackingNumber,''), shippingBarcode),
                   invoiceNumber,
                   CASE WHEN createdDate IS NULL OR YEAR(createdDate)=0 THEN NULL ELSE createdDate END,
                   CASE WHEN updatedDate IS NULL OR YEAR(updatedDate)=0 THEN NULL ELSE updatedDate END
              FROM oporders
             WHERE platformId = @platformId
             ORDER BY Id
            """;
        command.Parameters.AddWithValue("@platformId", platformId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<LegacyOrderSourceRow>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new(
                reader.GetInt32(0), Text(reader, 1), Text(reader, 2), Text(reader, 3),
                Text(reader, 4), Text(reader, 5), Text(reader, 6), Int(reader, 7), Date(reader, 8),
                Int(reader, 9), Int(reader, 10), Int(reader, 11), Text(reader, 12), Text(reader, 13),
                Text(reader, 14), Text(reader, 15), Text(reader, 16), Decimal(reader, 17),
                Decimal(reader, 18), Decimal(reader, 19), Decimal(reader, 20), Decimal(reader, 21),
                Decimal(reader, 22), Decimal(reader, 23), Decimal(reader, 24), Decimal(reader, 25),
                Text(reader, 26), Text(reader, 27), Text(reader, 28), Text(reader, 29),
                Date(reader, 30), Date(reader, 31)));
        }
        return rows;
    }

    private static async Task<IReadOnlyList<LegacyOrderAddressSourceRow>> ReadAddressesAsync(
        MySqlConnection connection, MySqlTransaction transaction, int platformId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT DISTINCT a.Id, a.addressDetail, a.postalCode, a.neighborhoodName,
                   a.districtName, a.cityName, a.countryName, a.contactFirstName,
                   a.contactLastName, a.contactPhone, a.invoiceTitle, a.taxOffice, a.taxNumber
              FROM webmemberaddresses a
              JOIN oporders o ON a.Id = o.shippingAddressId OR a.Id = o.invoiceAddressId
             WHERE o.platformId = @platformId
             ORDER BY a.Id
            """;
        command.Parameters.AddWithValue("@platformId", platformId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<LegacyOrderAddressSourceRow>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new(
                reader.GetInt32(0), Text(reader, 1), Text(reader, 2), Text(reader, 3),
                Text(reader, 4), Text(reader, 5), Text(reader, 6), Text(reader, 7),
                Text(reader, 8), Text(reader, 9), Text(reader, 10), Text(reader, 11), Text(reader, 12)));
        }
        return rows;
    }

    private static async Task<IReadOnlyList<LegacyOrderLineSourceRow>> ReadLinesAsync(
        MySqlConnection connection, MySqlTransaction transaction, int platformId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT l.Id, l.orderId, l.productVariantId, l.barcode, l.productCode,
                   l.productName, l.color, l.variantValue, l.sellingPrice, l.quantity,
                   l.discountAmount, l.status,
                   CASE WHEN l.createdDate IS NULL OR YEAR(l.createdDate)=0 THEN NULL ELSE l.createdDate END
              FROM oporderlines l
              JOIN oporders o ON o.Id = l.orderId
             WHERE o.platformId = @platformId
             ORDER BY l.orderId, l.Id
            """;
        command.Parameters.AddWithValue("@platformId", platformId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<LegacyOrderLineSourceRow>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new(
                reader.GetInt32(0), reader.GetInt32(1), Int(reader, 2), Text(reader, 3),
                Text(reader, 4), Text(reader, 5), Text(reader, 6), Text(reader, 7),
                Decimal(reader, 8), Int(reader, 9), Decimal(reader, 10), Int(reader, 11), Date(reader, 12)));
        }
        return rows;
    }

    private static async Task<IReadOnlyList<LegacyOrderPaymentSourceRow>> ReadPaymentsAsync(
        MySqlConnection connection, MySqlTransaction transaction, int platformId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT p.Id, p.orderId, p.paymentTypeId, t.code, t.Title,
                   p.paymentDescription, p.paymentAmount, p.installmentCount,
                   p.isPaid, p.gibCode,
                   CASE WHEN p.createdDate IS NULL OR YEAR(p.createdDate)=0 THEN NULL ELSE p.createdDate END
              FROM oporderpayments p
              JOIN oporders o ON o.Id = p.orderId
              LEFT JOIN dfpaymenttypes t ON t.Id = p.paymentTypeId
             WHERE o.platformId = @platformId
             ORDER BY p.orderId, p.Id
            """;
        command.Parameters.AddWithValue("@platformId", platformId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<LegacyOrderPaymentSourceRow>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new(
                reader.GetInt32(0), reader.GetInt32(1), Int(reader, 2), Text(reader, 3),
                Text(reader, 4), Text(reader, 5), Decimal(reader, 6), NullableInt(reader, 7),
                Bool(reader, 8), Text(reader, 9), Date(reader, 10)));
        }
        return rows;
    }

    private static string Text(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal))?.Trim() ?? string.Empty;

    private static int Int(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));

    private static int? NullableInt(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));

    private static decimal Decimal(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(reader.GetValue(ordinal));

    private static DateTime? Date(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);

    private static bool Bool(DbDataReader reader, int ordinal) =>
        !reader.IsDBNull(ordinal) && Convert.ToInt64(reader.GetValue(ordinal)) != 0;
}
