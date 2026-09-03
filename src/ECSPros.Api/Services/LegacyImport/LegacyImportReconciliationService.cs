using Npgsql;

namespace ECSPros.Api.Services.LegacyImport;

public sealed record LegacyEntityReconciliation(
    string Entity, int SourceCount, int TargetMatchedCount, IReadOnlyList<int> MissingSourceIds)
{
    public bool IsComplete => SourceCount == TargetMatchedCount && MissingSourceIds.Count == 0;
}

public sealed record LegacyImportReconciliationReport(
    DateTime CheckedAtUtc, IReadOnlyList<LegacyEntityReconciliation> Entities)
{
    public bool IsComplete => Entities.All(x => x.IsComplete);
    public int TotalMissing => Entities.Sum(x => x.MissingSourceIds.Count);
}

/// <summary>
/// Kaynak ve hedef Legacy*Id kümelerini karşılaştırır. Salt okunurdur; log katmanına yalnız sayım verir,
/// eksik ID listesi PII içermez ve kontrollü kabul raporunda kullanılabilir.
/// </summary>
public sealed class LegacyImportReconciliationService(
    ILegacyMemberAddressReader memberReader,
    ILegacyOrderAggregateReader orderReader,
    ILegacyInvoiceReader invoiceReader,
    ILegacyReturnReader returnReader,
    NpgsqlDataSource dataSource,
    LegacyReadImportOptions options)
{
    public async Task<LegacyImportReconciliationReport> RunAsync(CancellationToken ct)
    {
        var members = await memberReader.ReadAsync(options.PlatformId, ct);
        var orders = await orderReader.ReadAsync(options.PlatformId, ct);
        var invoices = await invoiceReader.ReadAsync(options.PlatformId, ct);
        var returns = await returnReader.ReadAsync(options.PlatformId, ct);

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var reports = new List<LegacyEntityReconciliation>
        {
            await CompareAsync(connection, "members", members.Members.Select(x => x.Id),
                "SELECT \"LegacyMemberId\" FROM crm.crm_members WHERE \"LegacyMemberId\"=ANY(@ids) AND NOT \"IsDeleted\"", ct),
            await CompareAsync(connection, "addresses", members.Addresses.Select(x => x.Id),
                "SELECT \"LegacyAddressId\" FROM crm.crm_addresses WHERE \"LegacyAddressId\"=ANY(@ids) AND NOT \"IsDeleted\"", ct),
            await CompareAsync(connection, "orders", orders.Orders.Select(x => x.Id),
                "SELECT \"LegacyOrderId\" FROM \"order\".ord_orders WHERE \"LegacyOrderId\"=ANY(@ids) AND NOT \"IsDeleted\"", ct),
            await CompareAsync(connection, "order-lines", orders.Lines.Select(x => x.Id),
                "SELECT \"LegacyOrderLineId\" FROM \"order\".ord_order_items WHERE \"LegacyOrderLineId\"=ANY(@ids) AND NOT \"IsDeleted\"", ct),
            await CompareAsync(connection, "order-payments", orders.Payments.Select(x => x.Id),
                "SELECT \"LegacyOrderPaymentId\" FROM \"order\".ord_order_payments WHERE \"LegacyOrderPaymentId\"=ANY(@ids) AND NOT \"IsDeleted\"", ct),
            await CompareAsync(connection, "invoices", invoices.Select(x => x.Id),
                "SELECT \"LegacyInvoiceId\" FROM \"order\".ord_invoices WHERE \"LegacyInvoiceId\"=ANY(@ids) AND NOT \"IsDeleted\"", ct),
            await CompareAsync(connection, "returns", returns.Returns.Select(x => x.Id),
                "SELECT \"LegacyReturnId\" FROM \"order\".ord_returns WHERE \"LegacyReturnId\"=ANY(@ids) AND NOT \"IsDeleted\"", ct),
            await CompareAsync(connection, "return-items", returns.Items.Select(x => x.Id),
                "SELECT \"LegacyReturnItemId\" FROM \"order\".ord_return_items WHERE \"LegacyReturnItemId\"=ANY(@ids) AND NOT \"IsDeleted\"", ct)
        };
        return new(DateTime.UtcNow, reports);
    }

    private static async Task<LegacyEntityReconciliation> CompareAsync(
        NpgsqlConnection connection, string entity, IEnumerable<int> sourceValues, string sql, CancellationToken ct)
    {
        var source = sourceValues.Distinct().Order().ToArray();
        if (source.Length == 0) return new(entity, 0, 0, []);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("ids", source);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var target = new HashSet<int>();
        while (await reader.ReadAsync(ct))
            if (!reader.IsDBNull(0)) target.Add(reader.GetInt32(0));
        var missing = source.Where(x => !target.Contains(x)).ToArray();
        return new(entity, source.Length, target.Count, missing);
    }
}
