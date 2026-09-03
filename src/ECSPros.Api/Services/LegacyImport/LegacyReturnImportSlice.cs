using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace ECSPros.Api.Services.LegacyImport;

/// <summary>
/// Legacy iadeleri tarihsel snapshot olarak yazar. Domain event, stok iadesi ve gerçek para iadesi üretmez.
/// Eksik sipariş/satır/neden eşlemesinde bütün dilim transaction öncesinde durur.
/// </summary>
public sealed class LegacyReturnImportSlice(
    ILegacyReturnReader reader,
    NpgsqlDataSource dataSource,
    ILegacyImportCheckpointStore checkpoints,
    LegacyReadImportOptions options,
    ILogger<LegacyReturnImportSlice> logger) : ILegacyCommerceImportSlice
{
    private readonly TimeZoneInfo _sourceTimeZone = ResolveTimeZone(options.SourceTimeZoneId);
    public string Slice => LegacyImportSlices.Returns;

    public async Task<LegacyImportSliceReport> RunAsync(CancellationToken ct)
    {
        try
        {
            var snapshot = await reader.ReadAsync(options.PlatformId, ct);
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            var references = await LoadReferencesAsync(connection, snapshot, ct);
            var (prepared, errors) = Prepare(snapshot, references);
            if (errors.Count > 0)
            {
                foreach (var error in errors.Take(20)) logger.LogWarning("Legacy iade hazırlama engeli: {Error}", error);
                return Fail(
                    $"{errors.Count} iade/eşleme engeli bulundu; hiçbir hedef yazısı yapılmadı. " +
                    $"İlk engeller: {string.Join(" | ", errors.Take(10))}", errors.Count);
            }
            var potentialChanged = prepared.Sum(x => 1 + x.Items.Count);
            if (options.DryRun) return new(Slice, true, true, potentialChanged, 0);

            var changed = 0;
            await using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                foreach (var item in prepared)
                {
                    changed += await UpsertReturnAsync(connection, transaction, item, ct);
                    changed += await ReconcileItemsAsync(connection, transaction, item, ct);
                }
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }

            var watermark = snapshot.Returns.Select(x => x.CreatedAt ?? x.ReturnDate)
                .Concat(snapshot.Logs.Select(x => x.CreatedAt)).Where(x => x.HasValue)
                .Select(x => Utc(x)!.Value).DefaultIfEmpty(DateTime.UtcNow).Max();
            var lastId = snapshot.Returns.Select(x => (long)x.Id)
                .Concat(snapshot.Items.Select(x => (long)x.Id)).Concat(snapshot.Logs.Select(x => (long)x.Id))
                .DefaultIfEmpty().Max();
            await checkpoints.SaveSuccessAsync(Slice, options.PlatformId, watermark, lastId, ct);
            return new(Slice, true, false, changed, 0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Legacy iade importu başarısız");
            if (!options.DryRun)
            {
                try { await checkpoints.SaveErrorAsync(Slice, options.PlatformId, ex.Message, ct); }
                catch (Exception logEx) { logger.LogWarning(logEx, "Legacy iade checkpoint hatası yazılamadı"); }
            }
            return Fail(ex.Message, 0);
        }
    }

    private LegacyImportSliceReport Fail(string error, int skipped) => new(Slice, false, options.DryRun, 0, skipped, error);

    private static async Task<TargetReferences> LoadReferencesAsync(
        NpgsqlConnection connection, LegacyReturnSnapshot snapshot, CancellationToken ct)
    {
        var orderIds = snapshot.Returns.Select(x => x.OrderId).Distinct().ToArray();
        var orders = new Dictionary<int, TargetOrder>();
        if (orderIds.Length > 0)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT "Id","LegacyOrderId","MemberId" FROM "order".ord_orders
                 WHERE "LegacyOrderId"=ANY(@ids) AND NOT "IsDeleted"
                """;
            command.Parameters.AddWithValue("ids", orderIds);
            await using var dbReader = await command.ExecuteReaderAsync(ct);
            while (await dbReader.ReadAsync(ct))
                orders[dbReader.GetInt32(1)] = new(dbReader.GetGuid(0), dbReader.GetInt32(1), dbReader.IsDBNull(2) ? null : dbReader.GetGuid(2));
        }

        var lineIds = snapshot.Items.Select(x => x.OrderLineId).Distinct().ToArray();
        var orderItems = new Dictionary<int, TargetOrderItem>();
        if (lineIds.Length > 0)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT "Id","LegacyOrderLineId","OrderId","VariantId","Quantity"
                  FROM "order".ord_order_items
                 WHERE "LegacyOrderLineId"=ANY(@ids) AND NOT "IsDeleted"
                """;
            command.Parameters.AddWithValue("ids", lineIds);
            await using var dbReader = await command.ExecuteReaderAsync(ct);
            while (await dbReader.ReadAsync(ct))
                orderItems[dbReader.GetInt32(1)] = new(
                    dbReader.GetGuid(0), dbReader.GetInt32(1), dbReader.GetGuid(2), dbReader.GetGuid(3), dbReader.GetInt32(4));
        }

        var reasons = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT "Code","Id" FROM core.core_return_reasons
                 WHERE "IsActive" AND NOT "IsDeleted"
                """;
            await using var dbReader = await command.ExecuteReaderAsync(ct);
            while (await dbReader.ReadAsync(ct)) reasons[dbReader.GetString(0)] = dbReader.GetGuid(1);
        }

        var existing = new Dictionary<int, TargetReturn>();
        var numberOwners = new Dictionary<string, TargetReturn>(StringComparer.OrdinalIgnoreCase);
        var sourceIds = snapshot.Returns.Select(x => x.Id).ToArray();
        if (sourceIds.Length > 0)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT "Id","LegacyReturnId","ReturnNumber","IsDeleted" FROM "order".ord_returns
                 WHERE "LegacyReturnId"=ANY(@ids) OR "ReturnNumber"=ANY(@numbers)
                """;
            command.Parameters.AddWithValue("ids", sourceIds);
            command.Parameters.AddWithValue("numbers", sourceIds.Select(ReturnNumber).ToArray());
            await using var dbReader = await command.ExecuteReaderAsync(ct);
            while (await dbReader.ReadAsync(ct))
            {
                var row = new TargetReturn(
                    dbReader.GetGuid(0), dbReader.IsDBNull(1) ? null : dbReader.GetInt32(1),
                    dbReader.GetString(2), dbReader.GetBoolean(3));
                if (row.LegacyId.HasValue) existing.TryAdd(row.LegacyId.Value, row);
                if (!row.IsDeleted) numberOwners.TryAdd(row.ReturnNumber, row);
            }
        }
        return new(orders, orderItems, reasons, existing, numberOwners);
    }

    private (List<PreparedReturn> Prepared, List<string> Errors) Prepare(
        LegacyReturnSnapshot snapshot, TargetReferences references)
    {
        var prepared = new List<PreparedReturn>();
        var errors = new List<string>();
        var items = snapshot.Items.GroupBy(x => x.ReturnId).ToDictionary(x => x.Key, x => x.ToList());
        var logs = snapshot.Logs.GroupBy(x => x.OrderId).ToDictionary(x => x.Key, x => x.ToList());
        foreach (var source in snapshot.Returns)
        {
            var rowErrors = new List<string>();
            if (!references.Orders.TryGetValue(source.OrderId, out var order))
                rowErrors.Add($"iade {source.Id}: legacy sipariş {source.OrderId} hedefte yok");
            else if (!order.MemberId.HasValue)
                rowErrors.Add($"iade {source.Id}: hedef siparişin üyesi yok");
            items.TryGetValue(source.Id, out var sourceItems); sourceItems ??= [];
            if (sourceItems.Count == 0) rowErrors.Add($"iade {source.Id}: kaynak kalemi yok");
            var resolved = new List<PreparedReturnItem>();
            foreach (var sourceItem in sourceItems)
            {
                if (!references.OrderItems.TryGetValue(sourceItem.OrderLineId, out var orderItem))
                {
                    rowErrors.Add($"iade {source.Id}: legacy sipariş satırı {sourceItem.OrderLineId} hedefte yok");
                    continue;
                }
                if (order is not null && orderItem.OrderId != order.Id)
                {
                    rowErrors.Add($"iade {source.Id}: sipariş satırı başka siparişe ait");
                    continue;
                }
                if (sourceItem.OrderLineQuantity <= 0 || sourceItem.OrderLineQuantity != orderItem.Quantity)
                {
                    rowErrors.Add($"iade {source.Id}: kalem {sourceItem.Id} miktarı uyuşmuyor");
                    continue;
                }
                var reasonCode = LegacyReturnMappings.ReasonCode(sourceItem.ReasonId);
                if (!references.Reasons.TryGetValue(reasonCode, out var reasonId))
                {
                    rowErrors.Add($"iade {source.Id}: hedef iade nedeni {reasonCode} yok");
                    continue;
                }
                resolved.Add(new(sourceItem, orderItem, reasonId));
            }
            references.Existing.TryGetValue(source.Id, out var existing);
            if (existing is { IsDeleted: true }) rowErrors.Add($"iade {source.Id}: hedef legacy kayıt silinmiş");
            var number = ReturnNumber(source.Id);
            if (references.NumberOwners.TryGetValue(number, out var owner) && owner.Id != existing?.Id)
                rowErrors.Add($"iade {source.Id}: hedef iade numarası başka kayda ait");
            var date = Utc(source.CreatedAt ?? source.ReturnDate);
            if (date is null) rowErrors.Add($"iade {source.Id}: geçerli tarih yok");
            if (source.ReturnAmount < 0 || source.PaidToMemberAmount < 0)
                rowErrors.Add($"iade {source.Id}: negatif tutar bulundu");
            if (sourceItems.Any(x => x.Amount < 0))
                rowErrors.Add($"iade {source.Id}: negatif kalem tutarı bulundu");
            var itemTotal = sourceItems.Sum(x => x.Amount);
            var useItemTotal = string.Equals(
                options.ReturnAmountMismatchPolicy,
                LegacyReturnAmountMismatchPolicies.UseItemTotal,
                StringComparison.OrdinalIgnoreCase);
            if (!useItemTotal && Math.Abs(itemTotal - source.ReturnAmount) > 0.02m)
                rowErrors.Add($"iade {source.Id}: üst/kalem tutarı uyuşmuyor");
            if (rowErrors.Count > 0) { errors.AddRange(rowErrors); continue; }
            logs.TryGetValue(source.OrderId, out var returnLogs); returnLogs ??= [];
            prepared.Add(new(
                source, existing?.Id ?? Guid.NewGuid(), existing is not null, order!, number,
                date!.Value, useItemTotal ? itemTotal : source.ReturnAmount, itemTotal,
                useItemTotal ? "item_total" : "header", resolved, returnLogs));
        }
        return (prepared, errors);
    }

    private static async Task<int> UpsertReturnAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, PreparedReturn item, CancellationToken ct)
    {
        var metadata = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["legacyImport"] = new Dictionary<string, object?>
            {
                ["returnId"] = item.Source.Id,
                ["rawType"] = item.Source.RawType,
                ["rawStatus"] = item.Source.RawStatus,
                ["rawRefundMethod"] = item.Source.RawRefundMethod,
                ["sourceHeaderAmount"] = item.Source.ReturnAmount,
                ["sourceItemTotal"] = item.ItemTotal,
                ["resolvedRefundAmount"] = item.RefundAmount,
                ["refundAmountBasis"] = item.RefundAmountBasis,
                ["paidToMemberAmount"] = item.Source.PaidToMemberAmount,
                ["paidToMemberAt"] = item.Source.PaidToMemberAt,
                ["integrated"] = item.Source.Integrated,
                ["logCount"] = item.Logs.Count,
                ["lastLogStatus"] = item.Logs.OrderByDescending(x => x.CreatedAt).FirstOrDefault()?.RawStatus
            }
        });
        var p = Parameters(item, metadata);
        if (item.Exists)
            return await ExecuteAsync(connection, transaction, """
                UPDATE "order".ord_returns SET
                    "ReturnNumber"=@number,"OrderId"=@orderId,"MemberId"=@memberId,"ReturnType"=@type,
                    "Status"='legacy_imported',"InspectionNotes"=@metadata,"RefundMethod"=@refundMethod,
                    "RefundStatus"=@refundStatus,"RefundAmount"=@amount,"UpdatedAt"=@date
                 WHERE "Id"=@id AND "LegacyReturnId"=@legacyId AND NOT "IsDeleted"
                   AND ROW("ReturnNumber","OrderId","MemberId","ReturnType","Status","InspectionNotes",
                           "RefundMethod","RefundStatus","RefundAmount")
                       IS DISTINCT FROM ROW(@number,@orderId,@memberId,@type,'legacy_imported',@metadata,
                                            @refundMethod,@refundStatus,@amount)
                """, ct, p);
        return await ExecuteAsync(connection, transaction, """
            INSERT INTO "order".ord_returns
                ("Id","LegacyReturnId","ReturnNumber","OrderId","MemberId","ReturnType","Status",
                 "InspectionNotes","RefundMethod","RefundStatus","RefundAmount","ImageUrls","CreatedAt","IsDeleted")
            VALUES (@id,@legacyId,@number,@orderId,@memberId,@type,'legacy_imported',@metadata,
                    @refundMethod,@refundStatus,@amount,ARRAY[]::text[],@date,false)
            """, ct, p);
    }

    private static async Task<int> ReconcileItemsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, PreparedReturn item, CancellationToken ct)
    {
        var existing = new Dictionary<int, TargetReturnItem>();
        var untracked = 0;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                SELECT "Id","LegacyReturnItemId","IsDeleted" FROM "order".ord_return_items
                 WHERE "ReturnId"=@id FOR UPDATE
                """;
            command.Parameters.AddWithValue("id", item.Id);
            await using var dbReader = await command.ExecuteReaderAsync(ct);
            while (await dbReader.ReadAsync(ct))
            {
                if (dbReader.IsDBNull(1)) { if (!dbReader.GetBoolean(2)) untracked++; continue; }
                existing[dbReader.GetInt32(1)] = new(dbReader.GetGuid(0), dbReader.GetInt32(1), dbReader.GetBoolean(2));
            }
        }
        if (item.Exists && untracked > 0)
            throw new InvalidOperationException($"Legacy iade {item.Source.Id} için {untracked} kimliksiz hedef kalem bulundu; transaction geri alındı.");

        var changed = 0;
        var sourceIds = item.Items.Select(x => x.Source.Id).ToHashSet();
        foreach (var sourceItem in item.Items)
        {
            existing.TryGetValue(sourceItem.Source.Id, out var target);
            if (target is { IsDeleted: true }) throw new InvalidOperationException($"Legacy iade kalemi silinmiş; yeniden açılmadı: {sourceItem.Source.Id}");
            var unit = sourceItem.Source.OrderLineQuantity == 0 ? 0 : sourceItem.Source.Amount / sourceItem.Source.OrderLineQuantity;
            var notes = $"legacyCustomerRequest={sourceItem.Source.RawCustomerRequest}; legacyReason={sourceItem.Source.Reason}";
            if (target is null)
                changed += await ExecuteAsync(connection, transaction, """
                    INSERT INTO "order".ord_return_items
                        ("Id","LegacyReturnItemId","ReturnId","OrderItemId","VariantId","Quantity",
                         "ReturnReasonId","CustomerNotes","UnitRefundAmount","TotalRefundAmount","Status","CreatedAt","IsDeleted")
                    VALUES (@id,@legacyId,@returnId,@orderItemId,@variantId,@quantity,@reasonId,@notes,
                            @unit,@total,'legacy_imported',@createdAt,false)
                    """, ct, ("id", Guid.NewGuid()), ("legacyId", sourceItem.Source.Id), ("returnId", item.Id),
                    ("orderItemId", sourceItem.OrderItem.Id), ("variantId", sourceItem.OrderItem.VariantId),
                    ("quantity", sourceItem.Source.OrderLineQuantity), ("reasonId", sourceItem.ReasonId),
                    ("notes", notes), ("unit", unit), ("total", sourceItem.Source.Amount), ("createdAt", item.DateUtc));
            else
                changed += await ExecuteAsync(connection, transaction, """
                    UPDATE "order".ord_return_items SET
                        "OrderItemId"=@orderItemId,"VariantId"=@variantId,"Quantity"=@quantity,
                        "ReturnReasonId"=@reasonId,"CustomerNotes"=@notes,"UnitRefundAmount"=@unit,
                        "TotalRefundAmount"=@total,"Status"='legacy_imported',"UpdatedAt"=@updatedAt
                     WHERE "Id"=@id AND "ReturnId"=@returnId AND "LegacyReturnItemId"=@legacyId AND NOT "IsDeleted"
                       AND ROW("OrderItemId","VariantId","Quantity","ReturnReasonId","CustomerNotes",
                               "UnitRefundAmount","TotalRefundAmount","Status")
                           IS DISTINCT FROM ROW(@orderItemId,@variantId,@quantity,@reasonId,@notes,
                                                @unit,@total,'legacy_imported')
                    """, ct, ("id", target.Id), ("legacyId", sourceItem.Source.Id), ("returnId", item.Id),
                    ("orderItemId", sourceItem.OrderItem.Id), ("variantId", sourceItem.OrderItem.VariantId),
                    ("quantity", sourceItem.Source.OrderLineQuantity), ("reasonId", sourceItem.ReasonId),
                    ("notes", notes), ("unit", unit), ("total", sourceItem.Source.Amount), ("updatedAt", item.DateUtc));
        }
        foreach (var removed in existing.Values.Where(x => !sourceIds.Contains(x.LegacyId) && !x.IsDeleted))
            changed += await ExecuteAsync(connection, transaction, """
                UPDATE "order".ord_return_items SET "IsDeleted"=true,"DeletedAt"=now(),"UpdatedAt"=now()
                 WHERE "Id"=@id AND "ReturnId"=@returnId AND "LegacyReturnItemId"=@legacyId
                """, ct, ("id", removed.Id), ("returnId", item.Id), ("legacyId", removed.LegacyId));
        return changed;
    }

    private static (string Name, object? Value)[] Parameters(PreparedReturn x, string metadata) =>
    [
        ("id", x.Id), ("legacyId", x.Source.Id), ("number", x.Number), ("orderId", x.Order.Id),
        ("memberId", x.Order.MemberId!.Value), ("type", LegacyReturnMappings.ReturnType(x.Source.RawType)),
        ("metadata", metadata), ("refundMethod", LegacyReturnMappings.RefundMethod(x.Source.RawRefundMethod)),
        ("refundStatus", x.Source.PaidToMemberAt.HasValue ? "legacy_paid" : "legacy_pending"),
        ("amount", x.RefundAmount), ("date", x.DateUtc)
    ];

    private static async Task<int> ExecuteAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken ct,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
        {
            if (value is not null) command.Parameters.AddWithValue(name, value);
            else command.Parameters.Add(name, NpgsqlDbType.Text).Value = DBNull.Value;
        }
        return await command.ExecuteNonQueryAsync(ct);
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
    private static string ReturnNumber(int sourceId) => $"LRET-{sourceId}";

    private sealed record TargetOrder(Guid Id, int LegacyId, Guid? MemberId);
    private sealed record TargetOrderItem(Guid Id, int LegacyId, Guid OrderId, Guid VariantId, int Quantity);
    private sealed record TargetReturn(Guid Id, int? LegacyId, string ReturnNumber, bool IsDeleted);
    private sealed record TargetReturnItem(Guid Id, int LegacyId, bool IsDeleted);
    private sealed record TargetReferences(
        IReadOnlyDictionary<int, TargetOrder> Orders,
        IReadOnlyDictionary<int, TargetOrderItem> OrderItems,
        IReadOnlyDictionary<string, Guid> Reasons,
        IReadOnlyDictionary<int, TargetReturn> Existing,
        IReadOnlyDictionary<string, TargetReturn> NumberOwners);
    private sealed record PreparedReturnItem(LegacyReturnItemSourceRow Source, TargetOrderItem OrderItem, Guid ReasonId);
    private sealed record PreparedReturn(
        LegacyReturnSourceRow Source, Guid Id, bool Exists, TargetOrder Order, string Number,
        DateTime DateUtc, decimal RefundAmount, decimal ItemTotal, string RefundAmountBasis,
        IReadOnlyList<PreparedReturnItem> Items, IReadOnlyList<LegacyReturnLogSourceRow> Logs);
}
