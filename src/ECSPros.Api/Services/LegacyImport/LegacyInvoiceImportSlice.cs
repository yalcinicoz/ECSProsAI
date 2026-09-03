using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace ECSPros.Api.Services.LegacyImport;

/// <summary>
/// Tarihsel legacy faturaları doğrudan snapshot olarak yazar. Domain event, integratör, ERP veya kargo
/// çağrısı üretmez. Hedefte tanımlı olmayan seri için tahmin yapmadan bütün dilimi durdurur.
/// </summary>
public sealed class LegacyInvoiceImportSlice(
    ILegacyInvoiceReader reader,
    NpgsqlDataSource dataSource,
    ILegacyImportCheckpointStore checkpoints,
    LegacyReadImportOptions options,
    ILogger<LegacyInvoiceImportSlice> logger) : ILegacyCommerceImportSlice
{
    private readonly TimeZoneInfo _sourceTimeZone = ResolveTimeZone(options.SourceTimeZoneId);
    public string Slice => LegacyImportSlices.Invoices;

    public async Task<LegacyImportSliceReport> RunAsync(CancellationToken ct)
    {
        try
        {
            var source = await reader.ReadAsync(options.PlatformId, ct);
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            var references = await LoadReferencesAsync(connection, source, ct);
            var (prepared, errors) = Prepare(source, references);
            if (errors.Count > 0)
            {
                foreach (var error in errors.Take(20)) logger.LogWarning("Legacy fatura hazırlama engeli: {Error}", error);
                return Fail(
                    $"{errors.Count} fatura/eşleme engeli bulundu; hiçbir hedef yazısı yapılmadı. " +
                    $"İlk engeller: {string.Join(" | ", errors.Take(10))}", errors.Count);
            }

            var potentialChanged = prepared.Sum(x => 1 + x.OrderItems.Count);
            if (options.DryRun) return new(Slice, true, true, potentialChanged, 0);

            var changed = 0;
            await using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                foreach (var invoice in prepared)
                {
                    changed += await UpsertInvoiceAsync(connection, transaction, invoice, ct);
                    changed += await ReconcileItemsAsync(connection, transaction, invoice, ct);
                }
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }

            var watermark = source.Select(x => Utc(x.InvoiceDate)).Where(x => x.HasValue)
                .Select(x => x!.Value).DefaultIfEmpty(DateTime.UtcNow).Max();
            var lastId = source.Select(x => (long)x.Id).DefaultIfEmpty().Max();
            await checkpoints.SaveSuccessAsync(Slice, options.PlatformId, watermark, lastId, ct);
            return new(Slice, true, false, changed, 0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Legacy fatura importu başarısız");
            if (!options.DryRun)
            {
                try { await checkpoints.SaveErrorAsync(Slice, options.PlatformId, ex.Message, ct); }
                catch (Exception logEx) { logger.LogWarning(logEx, "Legacy fatura checkpoint hatası yazılamadı"); }
            }
            return Fail(ex.Message, 0);
        }
    }

    private LegacyImportSliceReport Fail(string error, int skipped) =>
        new(Slice, false, options.DryRun, 0, skipped, error);

    private static async Task<TargetReferences> LoadReferencesAsync(
        NpgsqlConnection connection, IReadOnlyCollection<LegacyInvoiceSourceRow> source, CancellationToken ct)
    {
        var orderIds = source.Select(x => x.OrderId).Distinct().ToArray();
        var orders = new Dictionary<int, TargetOrder>();
        if (orderIds.Length > 0)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT "Id","LegacyOrderId","BillingRecipientName","BillingTaxOffice","BillingTaxNumber",
                       "BillingCompanyName","BillingAddressLine","Subtotal","TotalDiscount","TotalTax","GrandTotal"
                  FROM "order".ord_orders
                 WHERE "LegacyOrderId" = ANY(@ids) AND NOT "IsDeleted"
                """;
            command.Parameters.AddWithValue("ids", orderIds);
            await using var dbReader = await command.ExecuteReaderAsync(ct);
            while (await dbReader.ReadAsync(ct))
            {
                var legacyId = dbReader.GetInt32(1);
                orders[legacyId] = new(
                    dbReader.GetGuid(0), legacyId, Text(dbReader, 2), TextOrNull(dbReader, 3),
                    TextOrNull(dbReader, 4), TextOrNull(dbReader, 5), Text(dbReader, 6),
                    dbReader.GetDecimal(7), dbReader.GetDecimal(8), dbReader.GetDecimal(9), dbReader.GetDecimal(10));
            }
        }

        var items = new Dictionary<Guid, IReadOnlyList<TargetOrderItem>>();
        if (orders.Count > 0)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT "Id","OrderId","ProductName","VariantInfo","Quantity","UnitPrice",
                       "DiscountAmount","TaxAmount","Total"
                  FROM "order".ord_order_items
                 WHERE "OrderId" = ANY(@ids) AND NOT "IsDeleted"
                 ORDER BY "OrderId","CreatedAt","Id"
                """;
            command.Parameters.AddWithValue("ids", orders.Values.Select(x => x.Id).ToArray());
            await using var dbReader = await command.ExecuteReaderAsync(ct);
            var rows = new List<TargetOrderItem>();
            while (await dbReader.ReadAsync(ct))
                rows.Add(new(
                    dbReader.GetGuid(0), dbReader.GetGuid(1), Text(dbReader, 2), Text(dbReader, 3),
                    dbReader.GetInt32(4), dbReader.GetDecimal(5), dbReader.GetDecimal(6),
                    dbReader.GetDecimal(7), dbReader.GetDecimal(8)));
            items = rows.GroupBy(x => x.OrderId).ToDictionary(x => x.Key, x => (IReadOnlyList<TargetOrderItem>)x.ToList());
        }

        var series = new List<TargetSeries>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT "Id","EArchiveSerial","EInvoiceSerial","ExportSerial"
                  FROM "order".ord_invoice_series
                 WHERE "IsActive" AND NOT "IsDeleted"
                """;
            await using var dbReader = await command.ExecuteReaderAsync(ct);
            while (await dbReader.ReadAsync(ct))
                series.Add(new(dbReader.GetGuid(0), Text(dbReader, 1), Text(dbReader, 2), Text(dbReader, 3)));
        }

        var existing = new Dictionary<int, TargetInvoice>();
        var owners = new Dictionary<string, TargetInvoice>(StringComparer.OrdinalIgnoreCase);
        var sourceIds = source.Select(x => x.Id).ToArray();
        if (sourceIds.Length > 0)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT "Id","LegacyInvoiceId","InvoiceSerial","InvoiceYear","InvoiceSequence","IsDeleted"
                  FROM "order".ord_invoices
                 WHERE "LegacyInvoiceId" = ANY(@ids)
                    OR "InvoiceNumber" = ANY(@numbers)
                """;
            command.Parameters.AddWithValue("ids", sourceIds);
            command.Parameters.AddWithValue("numbers", source.Select(x => x.InvoiceNumber).Distinct().ToArray());
            await using var dbReader = await command.ExecuteReaderAsync(ct);
            while (await dbReader.ReadAsync(ct))
            {
                var row = new TargetInvoice(
                    dbReader.GetGuid(0), dbReader.IsDBNull(1) ? null : dbReader.GetInt32(1),
                    Text(dbReader, 2), Text(dbReader, 3), dbReader.GetInt32(4), dbReader.GetBoolean(5));
                if (row.LegacyId.HasValue) existing.TryAdd(row.LegacyId.Value, row);
                if (!row.IsDeleted) owners.TryAdd(Key(row.Serial, row.Year, row.Sequence), row);
            }
        }
        return new(orders, items, series, existing, owners);
    }

    private (List<PreparedInvoice> Prepared, List<string> Errors) Prepare(
        IReadOnlyCollection<LegacyInvoiceSourceRow> source, TargetReferences references)
    {
        var prepared = new List<PreparedInvoice>();
        var errors = new List<string>();
        foreach (var duplicate in source.GroupBy(x => x.InvoiceNumber, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
            errors.Add($"kaynak fatura numarası tekrarlı: {duplicate.Key}");

        foreach (var row in source)
        {
            var rowErrors = new List<string>();
            var number = LegacyInvoiceNumberParser.Parse(row.InvoiceNumber);
            if (number is null) rowErrors.Add($"fatura {row.Id}: numara biçimi geçersiz");
            if (!references.Orders.TryGetValue(row.OrderId, out var order))
                rowErrors.Add($"fatura {row.Id}: legacy sipariş {row.OrderId} hedefte yok");
            var invoiceType = row.IsEArchive || row.InvoiceType.Equals("arsiv", StringComparison.OrdinalIgnoreCase)
                ? "e_archive" : "e_invoice";
            TargetSeries? series = null;
            if (number is not null)
            {
                series = references.Series.SingleOrDefault(x => invoiceType == "e_archive"
                    ? x.EArchiveSerial.Equals(number.Serial, StringComparison.OrdinalIgnoreCase)
                    : x.EInvoiceSerial.Equals(number.Serial, StringComparison.OrdinalIgnoreCase));
                if (series is null) rowErrors.Add($"fatura {row.Id}: hedefte {number.Serial} aktif fatura serisi yok");
            }
            references.Existing.TryGetValue(row.Id, out var existing);
            if (existing is { IsDeleted: true }) rowErrors.Add($"fatura {row.Id}: hedef legacy kayıt silinmiş");
            if (number is not null && references.NumberOwners.TryGetValue(Key(number.Serial, number.Year, number.Sequence), out var owner)
                && owner.Id != existing?.Id)
                rowErrors.Add($"fatura {row.Id}: hedef numara başka kayda ait");
            var date = Utc(row.InvoiceDate);
            if (date is null) rowErrors.Add($"fatura {row.Id}: geçerli tarih yok");
            if (row.InvoiceUrl.Length > 500)
                rowErrors.Add($"fatura {row.Id}: görüntüleme URL'si hedef sınırı olan 500 karakteri aşıyor");
            IReadOnlyList<TargetOrderItem> orderItems = [];
            if (order is not null) references.OrderItems.TryGetValue(order.Id, out orderItems!);
            if (order is not null && orderItems.Count == 0) rowErrors.Add($"fatura {row.Id}: hedef sipariş kalemi yok");
            if (rowErrors.Count > 0) { errors.AddRange(rowErrors); continue; }
            prepared.Add(new(
                row, existing?.Id ?? Guid.NewGuid(), existing is not null, order!, orderItems, series!,
                number!, invoiceType, date!.Value));
        }
        return (prepared, errors);
    }

    private static async Task<int> UpsertInvoiceAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, PreparedInvoice invoice, CancellationToken ct)
    {
        var metadata = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["legacyInvoiceId"] = invoice.Source.Id,
            ["ettn"] = NullIfEmpty(invoice.Source.Ettn),
            ["sourcePlatformInvoiceNumber"] = NullIfEmpty(invoice.Source.SourcePlatformInvoiceNumber),
            ["destinationPlatformInvoiceNumber"] = NullIfEmpty(invoice.Source.DestinationPlatformInvoiceNumber),
            ["shippingBarcode"] = NullIfEmpty(invoice.Source.ShippingBarcode),
            ["courierTrackingNumber"] = NullIfEmpty(invoice.Source.CourierTrackingNumber),
            ["shippingRecordId"] = NullIfEmpty(invoice.Source.ShippingRecordId),
            ["shippingStatus"] = invoice.Source.ShippingStatus,
            ["shippingResponse"] = NullIfEmpty(invoice.Source.ShippingResponse),
            ["platformIntegrated"] = invoice.Source.PlatformIntegrated
        });
        var p = Parameters(invoice, metadata);
        if (invoice.Exists)
            return await ExecuteAsync(connection, transaction, """
                UPDATE "order".ord_invoices SET
                    "OrderId"=@orderId,"InvoiceSeriesId"=@seriesId,"InvoiceType"=@type,
                    "InvoiceSerial"=@serial,"InvoiceYear"=@year,"InvoiceSequence"=@sequence,
                    "InvoiceNumber"=@number,"InvoiceDate"=@date,"RecipientName"=@recipient,
                    "RecipientTaxOffice"=@taxOffice,"RecipientTaxNumber"=@taxNumber,
                    "RecipientCompanyName"=@company,"RecipientAddress"=@address,
                    "Subtotal"=@subtotal,"TotalDiscount"=@discount,"TotalTax"=@tax,"GrandTotal"=@total,
                    "IntegratorStatus"=@integratorStatus,"IntegratorSentAt"=@integratorSentAt,
                    "IntegratorResponse"=CAST(@metadata AS jsonb),"IntegratorInvoiceUrl"=@url,
                    "ErpStatus"='legacy_imported',"Status"=@status,"UpdatedAt"=@date
                 WHERE "Id"=@id AND "LegacyInvoiceId"=@legacyId AND NOT "IsDeleted"
                   AND ROW("OrderId","InvoiceSeriesId","InvoiceType","InvoiceSerial","InvoiceYear",
                           "InvoiceSequence","InvoiceNumber","InvoiceDate","RecipientName","RecipientTaxOffice",
                           "RecipientTaxNumber","RecipientCompanyName","RecipientAddress","Subtotal",
                           "TotalDiscount","TotalTax","GrandTotal","IntegratorStatus","IntegratorSentAt",
                           "IntegratorResponse","IntegratorInvoiceUrl","ErpStatus","Status")
                       IS DISTINCT FROM
                       ROW(@orderId,@seriesId,@type,@serial,@year,@sequence,@number,@date,@recipient,@taxOffice,
                           @taxNumber,@company,@address,@subtotal,@discount,@tax,@total,@integratorStatus,
                           @integratorSentAt,CAST(@metadata AS jsonb),@url,'legacy_imported',@status)
                """, ct, p);
        return await ExecuteAsync(connection, transaction, """
            INSERT INTO "order".ord_invoices
                ("Id","LegacyInvoiceId","OrderId","InvoiceSeriesId","InvoiceType","InvoiceSerial",
                 "InvoiceYear","InvoiceSequence","InvoiceNumber","InvoiceDate","RecipientName",
                 "RecipientTaxOffice","RecipientTaxNumber","RecipientCompanyName","RecipientAddress",
                 "Subtotal","TotalDiscount","TotalTax","GrandTotal","IntegratorStatus","IntegratorSentAt",
                 "IntegratorResponse","IntegratorInvoiceUrl","ErpStatus","Status","CreatedAt","IsDeleted")
            VALUES (@id,@legacyId,@orderId,@seriesId,@type,@serial,@year,@sequence,@number,@date,@recipient,
                    @taxOffice,@taxNumber,@company,@address,@subtotal,@discount,@tax,@total,@integratorStatus,
                    @integratorSentAt,CAST(@metadata AS jsonb),@url,'legacy_imported',@status,@date,false)
            """, ct, p);
    }

    private static async Task<int> ReconcileItemsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, PreparedInvoice invoice, CancellationToken ct)
    {
        var existing = new Dictionary<Guid, TargetInvoiceItem>();
        var untracked = 0;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                SELECT "Id","OrderItemId","IsDeleted" FROM "order".ord_invoice_items
                 WHERE "InvoiceId"=@id FOR UPDATE
                """;
            command.Parameters.AddWithValue("id", invoice.Id);
            await using var dbReader = await command.ExecuteReaderAsync(ct);
            while (await dbReader.ReadAsync(ct))
            {
                if (dbReader.IsDBNull(1)) { if (!dbReader.GetBoolean(2)) untracked++; continue; }
                existing[dbReader.GetGuid(1)] = new(dbReader.GetGuid(0), dbReader.GetGuid(1), dbReader.GetBoolean(2));
            }
        }
        if (invoice.Exists && untracked > 0)
            throw new InvalidOperationException($"Legacy fatura {invoice.Source.Id} için {untracked} kimliksiz hedef kalem bulundu; transaction geri alındı.");

        var changed = 0;
        var sourceIds = invoice.OrderItems.Select(x => x.Id).ToHashSet();
        foreach (var item in invoice.OrderItems)
        {
            existing.TryGetValue(item.Id, out var target);
            if (target is { IsDeleted: true }) throw new InvalidOperationException($"Legacy fatura kalemi silinmiş; yeniden açılmadı: {invoice.Source.Id}");
            var description = string.Join(" — ", new[] { item.ProductName, item.VariantInfo }.Where(x => !string.IsNullOrWhiteSpace(x)));
            if (target is null)
                changed += await ExecuteAsync(connection, transaction, """
                    INSERT INTO "order".ord_invoice_items
                        ("Id","InvoiceId","OrderItemId","Description","Quantity","UnitPrice",
                         "DiscountAmount","TaxRate","TaxAmount","Total","CreatedAt","IsDeleted")
                    VALUES (@id,@invoiceId,@orderItemId,@description,@quantity,@price,@discount,0,@tax,@total,@createdAt,false)
                    """, ct, ("id", Guid.NewGuid()), ("invoiceId", invoice.Id), ("orderItemId", item.Id),
                    ("description", description), ("quantity", (decimal)item.Quantity), ("price", item.UnitPrice),
                    ("discount", item.DiscountAmount), ("tax", item.TaxAmount), ("total", item.Total), ("createdAt", invoice.DateUtc));
            else
                changed += await ExecuteAsync(connection, transaction, """
                    UPDATE "order".ord_invoice_items SET
                        "Description"=@description,"Quantity"=@quantity,"UnitPrice"=@price,
                        "DiscountAmount"=@discount,"TaxRate"=0,"TaxAmount"=@tax,"Total"=@total,"UpdatedAt"=@updatedAt
                     WHERE "Id"=@id AND "InvoiceId"=@invoiceId AND "OrderItemId"=@orderItemId AND NOT "IsDeleted"
                       AND ROW("Description","Quantity","UnitPrice","DiscountAmount","TaxRate","TaxAmount","Total")
                           IS DISTINCT FROM ROW(@description,@quantity,@price,@discount,0,@tax,@total)
                    """, ct, ("id", target.Id), ("invoiceId", invoice.Id), ("orderItemId", item.Id),
                    ("description", description), ("quantity", (decimal)item.Quantity), ("price", item.UnitPrice),
                    ("discount", item.DiscountAmount), ("tax", item.TaxAmount), ("total", item.Total), ("updatedAt", invoice.DateUtc));
        }
        foreach (var removed in existing.Values.Where(x => !sourceIds.Contains(x.OrderItemId) && !x.IsDeleted))
            changed += await ExecuteAsync(connection, transaction, """
                UPDATE "order".ord_invoice_items SET "IsDeleted"=true,"DeletedAt"=now(),"UpdatedAt"=now()
                 WHERE "Id"=@id AND "InvoiceId"=@invoiceId AND "OrderItemId"=@orderItemId
                """, ct, ("id", removed.Id), ("invoiceId", invoice.Id), ("orderItemId", removed.OrderItemId));
        return changed;
    }

    private static (string Name, object? Value)[] Parameters(PreparedInvoice x, string metadata) =>
    [
        ("id", x.Id), ("legacyId", x.Source.Id), ("orderId", x.Order.Id), ("seriesId", x.Series.Id),
        ("type", x.InvoiceType), ("serial", x.Number.Serial), ("year", x.Number.Year),
        ("sequence", x.Number.Sequence), ("number", x.Source.InvoiceNumber), ("date", x.DateUtc),
        ("recipient", x.Order.RecipientName), ("taxOffice", x.Order.TaxOffice),
        ("taxNumber", x.Order.TaxNumber), ("company", x.Order.Company), ("address", x.Order.Address),
        ("subtotal", x.Order.Subtotal), ("discount", x.Order.Discount), ("tax", x.Order.Tax),
        ("total", x.Order.Total), ("integratorStatus", x.Source.IsSentToIntegrator ? "legacy_sent" : "legacy_pending"),
        ("integratorSentAt", x.Source.IsSentToIntegrator ? x.DateUtc : null), ("metadata", metadata),
        ("url", NullIfEmpty(x.Source.InvoiceUrl)), ("status", x.Source.SendInvoice ? "issued" : "legacy_pending")
    ];

    private static async Task<int> ExecuteAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken ct,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters) AddParameter(command, name, value);
        return await command.ExecuteNonQueryAsync(ct);
    }

    private static void AddParameter(NpgsqlCommand command, string name, object? value)
    {
        if (value is not null) { command.Parameters.AddWithValue(name, value); return; }
        var type = name == "integratorSentAt" ? NpgsqlDbType.TimestampTz : NpgsqlDbType.Text;
        command.Parameters.Add(name, type).Value = DBNull.Value;
    }

    private DateTime? Utc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } utc => utc,
        { Kind: DateTimeKind.Local } local => local.ToUniversalTime(),
        { } unspecified => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(unspecified, DateTimeKind.Unspecified), _sourceTimeZone)
    };
    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); }
            catch { return TimeZoneInfo.Utc; }
        }
    }
    private static string Key(string serial, string year, int sequence) => $"{serial.ToUpperInvariant()}|{year}|{sequence}";
    private static string Text(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    private static string? TextOrNull(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record TargetOrder(
        Guid Id, int LegacyId, string RecipientName, string? TaxOffice, string? TaxNumber, string? Company,
        string Address, decimal Subtotal, decimal Discount, decimal Tax, decimal Total);
    private sealed record TargetOrderItem(
        Guid Id, Guid OrderId, string ProductName, string VariantInfo, int Quantity, decimal UnitPrice,
        decimal DiscountAmount, decimal TaxAmount, decimal Total);
    private sealed record TargetSeries(Guid Id, string EArchiveSerial, string EInvoiceSerial, string ExportSerial);
    private sealed record TargetInvoice(Guid Id, int? LegacyId, string Serial, string Year, int Sequence, bool IsDeleted);
    private sealed record TargetInvoiceItem(Guid Id, Guid OrderItemId, bool IsDeleted);
    private sealed record TargetReferences(
        IReadOnlyDictionary<int, TargetOrder> Orders,
        IReadOnlyDictionary<Guid, IReadOnlyList<TargetOrderItem>> OrderItems,
        IReadOnlyList<TargetSeries> Series,
        IReadOnlyDictionary<int, TargetInvoice> Existing,
        IReadOnlyDictionary<string, TargetInvoice> NumberOwners);
    private sealed record PreparedInvoice(
        LegacyInvoiceSourceRow Source, Guid Id, bool Exists, TargetOrder Order,
        IReadOnlyList<TargetOrderItem> OrderItems, TargetSeries Series,
        LegacyInvoiceNumber Number, string InvoiceType, DateTime DateUtc);
}
