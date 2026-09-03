using System.Globalization;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace ECSPros.Api.Services.LegacyImport;

/// <summary>
/// Legacy sipariş/satır/ödeme snapshot'ını yalnız Legacy*Id kayıtlarına uygular. Domain event,
/// stok, rezervasyon, bildirim, ödeme veya outbound üretmez; PostgreSQL'e doğrudan tarihsel snapshot yazar.
/// </summary>
public sealed class LegacyOrderImportSlice(
    ILegacyOrderAggregateReader reader,
    NpgsqlDataSource dataSource,
    ILegacyImportCheckpointStore checkpoints,
    LegacyReadImportOptions options,
    ILogger<LegacyOrderImportSlice> logger) : ILegacyCommerceImportSlice
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private readonly TimeZoneInfo _sourceTimeZone = ResolveTimeZone(options.SourceTimeZoneId);
    public string Slice => LegacyImportSlices.Orders;

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
                foreach (var error in errors.Take(20))
                    logger.LogWarning("Legacy sipariş hazırlama engeli: {Error}", error);
                var sample = string.Join(" | ", errors.Take(10));
                return Fail(
                    $"{errors.Count} sipariş/eşleme engeli bulundu; hiçbir hedef yazısı yapılmadı. İlk engeller: {sample}",
                    errors.Count);
            }

            var potentialChanged = prepared.Sum(x => 1 + x.Lines.Count + x.Payments.Count);
            if (options.DryRun)
                return new(Slice, true, true, potentialChanged, 0);

            await using var transaction = await connection.BeginTransactionAsync(ct);
            var changed = 0;
            try
            {
                foreach (var order in prepared)
                {
                    changed += await UpsertOrderAsync(connection, transaction, order, ct);
                    changed += await ReconcileItemsAsync(connection, transaction, order, ct);
                    changed += await ReconcilePaymentsAsync(connection, transaction, order, ct);
                }
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }

            var watermark = snapshot.Orders.Select(x => x.UpdatedAt ?? x.CreatedAt ?? x.OrderDate)
                .Concat(snapshot.Lines.Select(x => x.CreatedAt))
                .Concat(snapshot.Payments.Select(x => x.CreatedAt))
                .Where(x => x.HasValue).Select(x => Utc(x)!.Value)
                .DefaultIfEmpty(DateTime.UtcNow).Max();
            var lastId = snapshot.Orders.Select(x => (long)x.Id)
                .Concat(snapshot.Lines.Select(x => (long)x.Id))
                .Concat(snapshot.Payments.Select(x => (long)x.Id))
                .DefaultIfEmpty().Max();
            await checkpoints.SaveSuccessAsync(Slice, options.PlatformId, watermark, lastId, ct);
            return new(Slice, true, false, changed, 0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Legacy sipariş aggregate importu başarısız");
            if (!options.DryRun)
            {
                try { await checkpoints.SaveErrorAsync(Slice, options.PlatformId, ex.Message, ct); }
                catch (Exception logEx) { logger.LogWarning(logEx, "Legacy sipariş checkpoint hatası yazılamadı"); }
            }
            return Fail(ex.Message, 0);
        }
    }

    private LegacyImportSliceReport Fail(string error, int skipped) =>
        new(Slice, false, options.DryRun, 0, skipped, error);

    private async Task<TargetReferences> LoadReferencesAsync(
        NpgsqlConnection connection, LegacyOrderAggregateSnapshot snapshot, CancellationToken ct)
    {
        var platformId = await ScalarGuidAsync(connection, """
            SELECT "Id" FROM core.core_firm_platforms
             WHERE "Code" = @code AND "IsActive" AND NOT "IsDeleted"
             LIMIT 1
            """, ct, ("code", options.FirmPlatformCode));
        if (platformId is null)
            throw new InvalidOperationException(
                $"Aktif hedef firma platformu bulunamadı: {options.FirmPlatformCode}");

        var memberIds = snapshot.Orders.Select(x => x.MemberId).Where(x => x > 0).Distinct().ToArray();
        var members = new Dictionary<int, Guid>();
        if (memberIds.Length > 0)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT "LegacyMemberId", "Id"
                  FROM crm.crm_members
                 WHERE "LegacyMemberId" = ANY(@ids) AND NOT "IsDeleted"
                """;
            command.Parameters.AddWithValue("ids", memberIds);
            await using var dbReader = await command.ExecuteReaderAsync(ct);
            while (await dbReader.ReadAsync(ct)) members[dbReader.GetInt32(0)] = dbReader.GetGuid(1);
        }

        var barcodes = snapshot.Lines.Select(x => x.Barcode.Trim())
            .Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var variants = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        if (barcodes.Length > 0)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT "Id", "Barcode", "Sku"
                  FROM catalog.product_variants
                 WHERE NOT "IsDeleted" AND ("Barcode" = ANY(@values) OR "Sku" = ANY(@values))
                """;
            command.Parameters.AddWithValue("values", barcodes);
            await using var dbReader = await command.ExecuteReaderAsync(ct);
            while (await dbReader.ReadAsync(ct))
            {
                var id = dbReader.GetGuid(0);
                if (!dbReader.IsDBNull(1) && !string.IsNullOrWhiteSpace(dbReader.GetString(1)))
                    variants.TryAdd(dbReader.GetString(1).Trim(), id);
                if (!dbReader.IsDBNull(2) && !string.IsNullOrWhiteSpace(dbReader.GetString(2)))
                    variants.TryAdd(dbReader.GetString(2).Trim(), id);
            }
        }

        var paymentMethods = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT "Code", "Id" FROM core.core_payment_methods
                 WHERE "IsActive" AND NOT "IsDeleted"
                """;
            await using var dbReader = await command.ExecuteReaderAsync(ct);
            while (await dbReader.ReadAsync(ct)) paymentMethods[dbReader.GetString(0)] = dbReader.GetGuid(1);
        }

        var countries = new Dictionary<string, GeoCountry>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT "Id", "Code", COALESCE("NameI18n"->>'tr', "Code")
                  FROM crm.crm_countries WHERE NOT "IsDeleted"
                """;
            await using var dbReader = await command.ExecuteReaderAsync(ct);
            while (await dbReader.ReadAsync(ct))
            {
                var row = new GeoCountry(dbReader.GetGuid(0), dbReader.GetString(1), dbReader.GetString(2));
                countries.TryAdd(row.Code, row);
                countries.TryAdd(Key(row.Name), row);
            }
        }

        var cities = new Dictionary<string, GeoCity>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT "Id", "CountryId", COALESCE("NameI18n"->>'tr', "Code")
                  FROM crm.crm_cities WHERE NOT "IsDeleted"
                """;
            await using var dbReader = await command.ExecuteReaderAsync(ct);
            while (await dbReader.ReadAsync(ct))
            {
                var row = new GeoCity(dbReader.GetGuid(0), dbReader.GetGuid(1), dbReader.GetString(2));
                cities.TryAdd($"{row.CountryId:N}|{Key(row.Name)}", row);
            }
        }

        var districts = new Dictionary<string, GeoDistrict>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT "Id", "CityId", COALESCE("NameI18n"->>'tr', "Code")
                  FROM crm.crm_districts WHERE NOT "IsDeleted"
                """;
            await using var dbReader = await command.ExecuteReaderAsync(ct);
            while (await dbReader.ReadAsync(ct))
            {
                var row = new GeoDistrict(dbReader.GetGuid(0), dbReader.GetGuid(1), dbReader.GetString(2));
                districts.TryAdd($"{row.CityId:N}|{Key(row.Name)}", row);
            }
        }

        var neighborhoods = new Dictionary<string, Guid>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT "Id", "DistrictId", COALESCE("NameI18n"->>'tr', "Code")
                  FROM crm.crm_neighborhoods WHERE NOT "IsDeleted"
                """;
            await using var dbReader = await command.ExecuteReaderAsync(ct);
            while (await dbReader.ReadAsync(ct))
                neighborhoods.TryAdd($"{dbReader.GetGuid(1):N}|{Key(dbReader.GetString(2))}", dbReader.GetGuid(0));
        }

        var sourceIds = snapshot.Orders.Select(x => x.Id).ToArray();
        var orderNumbers = snapshot.Orders.Select(x => x.OrderNumber).Distinct().ToArray();
        var existingByLegacyId = new Dictionary<int, TargetOrder>();
        var orderNumberOwners = new Dictionary<string, TargetOrder>(StringComparer.OrdinalIgnoreCase);
        if (sourceIds.Length > 0)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT "Id", "LegacyOrderId", "FirmPlatformId", "OrderNumber", "IsDeleted"
                  FROM "order".ord_orders
                 WHERE "LegacyOrderId" = ANY(@legacyIds)
                    OR ("FirmPlatformId" = @platformId AND "OrderNumber" = ANY(@orderNumbers))
                """;
            command.Parameters.AddWithValue("legacyIds", sourceIds);
            command.Parameters.AddWithValue("platformId", platformId.Value);
            command.Parameters.AddWithValue("orderNumbers", orderNumbers);
            await using var dbReader = await command.ExecuteReaderAsync(ct);
            while (await dbReader.ReadAsync(ct))
            {
                var row = new TargetOrder(
                    dbReader.GetGuid(0), dbReader.IsDBNull(1) ? null : dbReader.GetInt32(1),
                    dbReader.GetGuid(2), dbReader.GetString(3), dbReader.GetBoolean(4));
                if (row.LegacyOrderId.HasValue) existingByLegacyId.TryAdd(row.LegacyOrderId.Value, row);
                if (!row.IsDeleted) orderNumberOwners.TryAdd(row.OrderNumber, row);
            }
        }

        return new(
            platformId.Value, members, variants, paymentMethods, countries, cities,
            districts, neighborhoods, existingByLegacyId, orderNumberOwners);
    }

    private (List<PreparedOrder> Prepared, List<string> Errors) Prepare(
        LegacyOrderAggregateSnapshot snapshot, TargetReferences references)
    {
        var errors = new List<string>();
        var prepared = new List<PreparedOrder>();
        var addresses = snapshot.Addresses.ToDictionary(x => x.Id);
        var linesByOrder = snapshot.Lines.GroupBy(x => x.OrderId).ToDictionary(x => x.Key, x => x.ToList());
        var paymentsByOrder = snapshot.Payments.GroupBy(x => x.OrderId).ToDictionary(x => x.Key, x => x.ToList());

        foreach (var duplicate in snapshot.Orders
                     .Where(x => !string.IsNullOrWhiteSpace(x.OrderNumber))
                     .GroupBy(x => x.OrderNumber.Trim(), StringComparer.OrdinalIgnoreCase)
                     .Where(x => x.Count() > 1))
            errors.Add($"kaynak sipariş numarası tekrarlı: {duplicate.Key}");

        foreach (var source in snapshot.Orders)
        {
            var orderErrors = new List<string>();
            var status = LegacyOrderStatusMapper.Map(source.RawStatus);
            if (status is null) orderErrors.Add($"sipariş {source.Id}: bilinmeyen durum");

            Guid? memberId = null;
            if (source.MemberId > 0)
            {
                if (references.Members.TryGetValue(source.MemberId, out var mappedMember))
                    memberId = mappedMember;
                else
                    orderErrors.Add($"sipariş {source.Id}: legacy üye {source.MemberId} hedefte yok");
            }

            if (!addresses.TryGetValue(source.ShippingAddressId, out var shippingSource))
                orderErrors.Add($"sipariş {source.Id}: teslimat adresi {source.ShippingAddressId} kaynakta yok");
            var shipping = shippingSource is null ? null : ResolveAddress(shippingSource, references);
            if (shippingSource is not null && shipping is null)
                orderErrors.Add($"sipariş {source.Id}: teslimat geo eşleşmedi");

            LegacyOrderAddressSourceRow? billingSource = null;
            if (source.InvoiceAddressId > 0) addresses.TryGetValue(source.InvoiceAddressId, out billingSource);
            billingSource ??= shippingSource;
            var billing = billingSource is null ? null : ResolveAddress(billingSource, references);
            if (billingSource is not null && billing is null)
                orderErrors.Add($"sipariş {source.Id}: fatura geo eşleşmedi");

            linesByOrder.TryGetValue(source.Id, out var sourceLines);
            sourceLines ??= [];
            if (sourceLines.Count == 0) orderErrors.Add($"sipariş {source.Id}: kaynak satırı yok");
            var resolvedLines = new List<PreparedLine>();
            foreach (var line in sourceLines)
            {
                if (string.IsNullOrWhiteSpace(line.Barcode) ||
                    !references.Variants.TryGetValue(line.Barcode.Trim(), out var variantId))
                {
                    orderErrors.Add($"sipariş {source.Id}: satır {line.Id} barkodu hedefte yok");
                    continue;
                }
                if (line.Quantity <= 0)
                {
                    orderErrors.Add($"sipariş {source.Id}: satır {line.Id} adedi geçersiz");
                    continue;
                }
                resolvedLines.Add(new(line, variantId));
            }

            paymentsByOrder.TryGetValue(source.Id, out var sourcePayments);
            sourcePayments ??= [];
            var resolvedPayments = new List<PreparedPayment>();
            var orderPaymentMethod = PaymentMethodValue(source.PaymentTypeId);
            if (orderPaymentMethod is null)
                orderErrors.Add($"sipariş {source.Id}: üst kayıt ödeme tipi eşleşmedi");
            foreach (var payment in sourcePayments)
            {
                var code = PaymentMethodCode(payment.PaymentTypeId);
                if (code is null || !references.PaymentMethods.TryGetValue(code, out var methodId))
                {
                    orderErrors.Add($"sipariş {source.Id}: ödeme {payment.Id} tipi eşleşmedi");
                    continue;
                }
                resolvedPayments.Add(new(payment, methodId));
            }

            references.ExistingByLegacyId.TryGetValue(source.Id, out var existing);
            if (existing is { IsDeleted: true })
                orderErrors.Add($"sipariş {source.Id}: hedef legacy kayıt silinmiş; yeniden açılmadı");
            if (existing is not null && existing.FirmPlatformId != references.PlatformId)
                orderErrors.Add($"sipariş {source.Id}: hedef platform kimliği uyuşmuyor");
            if (references.OrderNumberOwners.TryGetValue(source.OrderNumber, out var numberOwner)
                && numberOwner.Id != existing?.Id)
                orderErrors.Add($"sipariş {source.Id}: hedef sipariş numarası başka kayda ait");

            var sourceDate = Utc(source.OrderDate ?? source.CreatedAt);
            if (sourceDate is null) orderErrors.Add($"sipariş {source.Id}: geçerli sipariş tarihi yok");
            if (string.IsNullOrWhiteSpace(source.OrderNumber)) orderErrors.Add($"sipariş {source.Id}: sipariş numarası boş");

            if (orderErrors.Count > 0)
            {
                errors.AddRange(orderErrors);
                continue;
            }

            var paid = resolvedPayments.Any(x => x.Source.IsPaid) || source.PaidTotal > 0;
            var paymentStatus = status!.Status == "cancelled" ? "cancelled" : paid ? "paid" : "unpaid";
            prepared.Add(new(
                source, existing?.Id ?? Guid.NewGuid(), existing is not null, references.PlatformId,
                memberId, status.Status, paymentStatus, orderPaymentMethod!, shipping!, billing!,
                sourceDate!.Value, resolvedLines, resolvedPayments));
        }

        return (prepared, errors);
    }

    private static ResolvedAddress? ResolveAddress(
        LegacyOrderAddressSourceRow source, TargetReferences references)
    {
        var countryKey = string.IsNullOrWhiteSpace(source.CountryName) ? "TR" : Key(source.CountryName);
        if (!references.Countries.TryGetValue(countryKey, out var country)
            && !references.Countries.TryGetValue("TR", out country))
            return null;
        if (!references.Cities.TryGetValue($"{country.Id:N}|{Key(source.CityName)}", out var city))
            return null;
        if (!references.Districts.TryGetValue($"{city.Id:N}|{Key(source.DistrictName)}", out var district))
            return null;
        Guid? neighborhoodId = null;
        if (!string.IsNullOrWhiteSpace(source.NeighborhoodName)
            && references.Neighborhoods.TryGetValue($"{district.Id:N}|{Key(source.NeighborhoodName)}", out var neighborhood))
            neighborhoodId = neighborhood;
        return new(source, country.Id, city.Id, district.Id, neighborhoodId);
    }

    private static async Task<int> UpsertOrderAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, PreparedOrder order, CancellationToken ct)
    {
        var source = order.Source;
        var externalNumber = FirstNotEmpty(
            source.SourceOrderId, source.SourcePlatformOrderNumber,
            source.DestinationPlatformOrderNumber, source.OrderNumber);
        var recipientName = FirstNotEmpty(
            $"{order.Shipping.Source.ContactFirstName} {order.Shipping.Source.ContactLastName}".Trim(),
            $"{source.MemberFirstName} {source.MemberLastName}".Trim(), "Legacy Müşteri");
        var recipientPhone = FirstNotEmpty(order.Shipping.Source.ContactPhone, source.MemberPhone, "-");
        var notes = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["legacyImport"] = new Dictionary<string, object?>
            {
                ["orderId"] = source.Id,
                ["rawStatus"] = source.RawStatus,
                ["source"] = source.SourceName,
                ["sourceOrderId"] = source.SourceOrderId,
                ["invoiceNumber"] = source.InvoiceNumber,
                ["courier"] = source.CourierName,
                ["trackingNumber"] = source.TrackingNumber
            },
            ["legacyCustomerNote"] = NullIfEmpty(source.CustomerNote)
        });

        if (order.Exists)
        {
            return await ExecuteAsync(connection, transaction, """
                UPDATE "order".ord_orders SET
                    "OrderNumber"=@orderNumber, "OrderNumberSource"='external', "ExternalOrderNumber"=@external,
                    "MemberId"=@memberId, "Status"=@status, "PaymentStatus"=@paymentStatus,
                    "PaymentMethod"=@paymentMethod, "CurrencyCode"=@currency, "InvoiceCurrencyCode"=@currency,
                    "ExchangeRate"=@exchangeRate, "ShippingRecipientName"=@recipientName,
                    "ShippingRecipientPhone"=@recipientPhone, "ShippingCountryId"=@shippingCountryId,
                    "ShippingCityId"=@shippingCityId, "ShippingDistrictId"=@shippingDistrictId,
                    "ShippingNeighborhoodId"=@shippingNeighborhoodId, "ShippingAddressLine"=@shippingAddressLine,
                    "ShippingPostalCode"=@shippingPostalCode, "RequestedCargoName"=@cargoName,
                    "BillingSameAsShipping"=@billingSame, "BillingRecipientName"=@billingRecipientName,
                    "BillingTaxOffice"=@billingTaxOffice, "BillingTaxNumber"=@billingTaxNumber,
                    "BillingCompanyName"=@billingCompanyName, "BillingCountryId"=@billingCountryId,
                    "BillingCityId"=@billingCityId, "BillingDistrictId"=@billingDistrictId,
                    "BillingAddressLine"=@billingAddressLine, "Subtotal"=@subtotal,
                    "TotalDiscount"=@discount, "TotalExpense"=@expense, "TotalTax"=@tax,
                    "GrandTotal"=@grandTotal,
                    "CustomerNotes"=COALESCE("CustomerNotes",'{}'::jsonb) || CAST(@notes AS jsonb),
                    "UpdatedAt"=@updatedAt
                 WHERE "Id"=@id AND "LegacyOrderId"=@legacyId AND NOT "IsDeleted"
                   AND (
                       "OrderNumber" IS DISTINCT FROM @orderNumber OR
                       "OrderNumberSource" IS DISTINCT FROM 'external' OR
                       "ExternalOrderNumber" IS DISTINCT FROM @external OR
                       "MemberId" IS DISTINCT FROM @memberId OR
                       "Status" IS DISTINCT FROM @status OR
                       "PaymentStatus" IS DISTINCT FROM @paymentStatus OR
                       "PaymentMethod" IS DISTINCT FROM @paymentMethod OR
                       "CurrencyCode" IS DISTINCT FROM @currency OR
                       "InvoiceCurrencyCode" IS DISTINCT FROM @currency OR
                       "ExchangeRate" IS DISTINCT FROM @exchangeRate OR
                       "ShippingRecipientName" IS DISTINCT FROM @recipientName OR
                       "ShippingRecipientPhone" IS DISTINCT FROM @recipientPhone OR
                       "ShippingCountryId" IS DISTINCT FROM @shippingCountryId OR
                       "ShippingCityId" IS DISTINCT FROM @shippingCityId OR
                       "ShippingDistrictId" IS DISTINCT FROM @shippingDistrictId OR
                       "ShippingNeighborhoodId" IS DISTINCT FROM @shippingNeighborhoodId OR
                       "ShippingAddressLine" IS DISTINCT FROM @shippingAddressLine OR
                       "ShippingPostalCode" IS DISTINCT FROM @shippingPostalCode OR
                       "RequestedCargoName" IS DISTINCT FROM @cargoName OR
                       "BillingSameAsShipping" IS DISTINCT FROM @billingSame OR
                       "BillingRecipientName" IS DISTINCT FROM @billingRecipientName OR
                       "BillingTaxOffice" IS DISTINCT FROM @billingTaxOffice OR
                       "BillingTaxNumber" IS DISTINCT FROM @billingTaxNumber OR
                       "BillingCompanyName" IS DISTINCT FROM @billingCompanyName OR
                       "BillingCountryId" IS DISTINCT FROM @billingCountryId OR
                       "BillingCityId" IS DISTINCT FROM @billingCityId OR
                       "BillingDistrictId" IS DISTINCT FROM @billingDistrictId OR
                       "BillingAddressLine" IS DISTINCT FROM @billingAddressLine OR
                       "Subtotal" IS DISTINCT FROM @subtotal OR
                       "TotalDiscount" IS DISTINCT FROM @discount OR
                       "TotalExpense" IS DISTINCT FROM @expense OR
                       "TotalTax" IS DISTINCT FROM @tax OR
                       "GrandTotal" IS DISTINCT FROM @grandTotal OR
                       "CustomerNotes" IS DISTINCT FROM
                           (COALESCE("CustomerNotes",'{}'::jsonb) || CAST(@notes AS jsonb))
                   )
                """, ct, OrderParameters(order, externalNumber, recipientName, recipientPhone, notes));
        }
        return await ExecuteAsync(connection, transaction, """
                INSERT INTO "order".ord_orders
                    ("Id","LegacyOrderId","OrderNumber","OrderNumberSource","ExternalOrderNumber",
                     "FirmPlatformId","MemberId","Status","PaymentStatus","PaymentMethod","OrderType",
                     "RequiresApproval","CurrencyCode","InvoiceCurrencyCode","ExchangeRate",
                     "ShippingRecipientName","ShippingRecipientPhone","ShippingCountryId","ShippingCityId",
                     "ShippingDistrictId","ShippingNeighborhoodId","ShippingAddressLine","ShippingPostalCode",
                     "RequestedCargoName","BillingSameAsShipping","BillingRecipientName","BillingTaxOffice",
                     "BillingTaxNumber","BillingCompanyName","BillingCountryId","BillingCityId","BillingDistrictId",
                     "BillingAddressLine","Subtotal","TotalDiscount","TotalExpense","TotalTax","GrandTotal",
                     "CustomerNotes","InternalNotes","ConfirmationRequired","CreatedAt","IsDeleted")
                VALUES
                    (@id,@legacyId,@orderNumber,'external',@external,@platformId,@memberId,@status,@paymentStatus,
                     @paymentMethod,'retail',false,@currency,@currency,@exchangeRate,@recipientName,@recipientPhone,
                     @shippingCountryId,@shippingCityId,@shippingDistrictId,@shippingNeighborhoodId,
                     @shippingAddressLine,@shippingPostalCode,@cargoName,@billingSame,@billingRecipientName,
                     @billingTaxOffice,@billingTaxNumber,@billingCompanyName,@billingCountryId,@billingCityId,
                     @billingDistrictId,@billingAddressLine,@subtotal,@discount,@expense,@tax,@grandTotal,
                     CAST(@notes AS jsonb),'[Legacy MySQL geçici import]',false,@createdAt,false)
                """, ct, OrderParameters(order, externalNumber, recipientName, recipientPhone, notes));
    }

    private static (string Name, object? Value)[] OrderParameters(
        PreparedOrder order, string externalNumber, string recipientName, string recipientPhone, string notes)
    {
        var source = order.Source;
        var sameAddress = source.ShippingAddressId == source.InvoiceAddressId;
        return
        [
            ("id", order.Id), ("legacyId", source.Id), ("orderNumber", source.OrderNumber),
            ("external", externalNumber), ("platformId", order.PlatformId), ("memberId", order.MemberId),
            ("status", order.Status), ("paymentStatus", order.PaymentStatus), ("paymentMethod", order.PaymentMethod),
            ("currency", string.IsNullOrWhiteSpace(source.Currency) ? "TRY" : source.Currency[..Math.Min(3, source.Currency.Length)].ToUpperInvariant()),
            ("exchangeRate", source.ExchangeRate <= 0 ? 1m : source.ExchangeRate),
            ("recipientName", recipientName), ("recipientPhone", recipientPhone),
            ("shippingCountryId", order.Shipping.CountryId), ("shippingCityId", order.Shipping.CityId),
            ("shippingDistrictId", order.Shipping.DistrictId), ("shippingNeighborhoodId", order.Shipping.NeighborhoodId),
            ("shippingAddressLine", FirstNotEmpty(order.Shipping.Source.AddressLine, "-")),
            ("shippingPostalCode", NullIfEmpty(order.Shipping.Source.PostalCode)),
            ("cargoName", NullIfEmpty(source.CourierName)), ("billingSame", sameAddress),
            ("billingRecipientName", FirstNotEmpty(
                $"{order.Billing.Source.ContactFirstName} {order.Billing.Source.ContactLastName}".Trim(), recipientName)),
            ("billingTaxOffice", NullIfEmpty(order.Billing.Source.TaxOffice)),
            ("billingTaxNumber", NullIfEmpty(order.Billing.Source.TaxNumber)),
            ("billingCompanyName", NullIfEmpty(order.Billing.Source.InvoiceTitle)),
            ("billingCountryId", order.Billing.CountryId), ("billingCityId", order.Billing.CityId),
            ("billingDistrictId", order.Billing.DistrictId),
            ("billingAddressLine", FirstNotEmpty(order.Billing.Source.AddressLine, "-")),
            ("subtotal", source.Subtotal), ("discount", source.DiscountTotal),
            ("expense", source.ExpenseTotal), ("tax", source.TaxTotal), ("grandTotal", source.OrderTotal),
            ("notes", notes), ("createdAt", order.SourceDateUtc),
            ("updatedAt", order.SourceDateUtc > DateTime.UnixEpoch ? order.SourceDateUtc : DateTime.UtcNow)
        ];
    }

    private static async Task<int> ReconcileItemsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, PreparedOrder order, CancellationToken ct)
    {
        var existing = new List<TargetItem>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                SELECT "Id","LegacyOrderLineId","VariantId","Quantity","UnitPrice","DiscountAmount","IsDeleted"
                  FROM "order".ord_order_items WHERE "OrderId"=@orderId
                 ORDER BY "CreatedAt","Id"
                 FOR UPDATE
                """;
            command.Parameters.AddWithValue("orderId", order.Id);
            await using var dbReader = await command.ExecuteReaderAsync(ct);
            while (await dbReader.ReadAsync(ct))
                existing.Add(new(
                    dbReader.GetGuid(0), dbReader.IsDBNull(1) ? null : dbReader.GetInt32(1),
                    dbReader.GetGuid(2), dbReader.GetInt32(3), dbReader.GetDecimal(4),
                    dbReader.GetDecimal(5), dbReader.GetBoolean(6)));
        }

        var byLegacy = existing.Where(x => x.LegacyId.HasValue).ToDictionary(x => x.LegacyId!.Value);
        var unmatched = existing.Where(x => !x.LegacyId.HasValue && !x.IsDeleted)
            .GroupBy(ItemSignature).ToDictionary(x => x.Key, x => new Queue<TargetItem>(x));
        var sourceIds = order.Lines.Select(x => x.Source.Id).ToHashSet();

        var changed = 0;
        foreach (var line in order.Lines)
        {
            TargetItem? target = null;
            if (byLegacy.TryGetValue(line.Source.Id, out var legacyTarget)) target = legacyTarget;
            else if (unmatched.TryGetValue(ItemSignature(line), out var candidates) && candidates.Count > 0)
                target = candidates.Dequeue();
            if (target is { IsDeleted: true })
                throw new InvalidOperationException($"Legacy sipariş satırı silinmiş; yeniden açılmadı: {line.Source.Id}");

            var itemId = target?.Id ?? Guid.NewGuid();
            var subtotal = line.Source.SellingPrice * line.Source.Quantity;
            var total = subtotal - line.Source.DiscountAmount;
            if (target is null)
            {
                changed += await ExecuteAsync(connection, transaction, """
                    INSERT INTO "order".ord_order_items
                        ("Id","LegacyOrderLineId","OrderId","VariantId","Sku","ProductName","VariantInfo",
                         "Quantity","UnitPrice","Subtotal","DiscountAmount","TaxAmount","Total","Status",
                         "SortingBinQuantity","FinalSortQuantity","FinalScanQuantity","CreatedAt","IsDeleted")
                    VALUES (@id,@legacyId,@orderId,@variantId,@sku,@productName,@variantInfo,@quantity,@unitPrice,
                            @subtotal,@discount,0,@total,@status,0,0,0,@createdAt,false)
                    """, ct,
                    ("id", itemId), ("legacyId", line.Source.Id), ("orderId", order.Id),
                    ("variantId", line.VariantId), ("sku", FirstNotEmpty(line.Source.Barcode, line.Source.ProductCode, "-")),
                    ("productName", FirstNotEmpty(line.Source.ProductName, line.Source.ProductCode, "Legacy Ürün")),
                    ("variantInfo", $"{line.Source.Color} {line.Source.VariantValue}".Trim()),
                    ("quantity", line.Source.Quantity), ("unitPrice", line.Source.SellingPrice),
                    ("subtotal", subtotal), ("discount", line.Source.DiscountAmount), ("total", total),
                    ("status", order.Status), ("createdAt", order.SourceDateUtc));
            }
            else
            {
                changed += await ExecuteAsync(connection, transaction, """
                    UPDATE "order".ord_order_items SET
                        "LegacyOrderLineId"=@legacyId,"VariantId"=@variantId,"Sku"=@sku,
                        "ProductName"=@productName,"VariantInfo"=@variantInfo,"Quantity"=@quantity,
                        "UnitPrice"=@unitPrice,"Subtotal"=@subtotal,"DiscountAmount"=@discount,
                        "Total"=@total,"Status"=@status,"UpdatedAt"=@updatedAt
                     WHERE "Id"=@id AND "OrderId"=@orderId AND NOT "IsDeleted"
                       AND (
                           "LegacyOrderLineId" IS DISTINCT FROM @legacyId OR
                           "VariantId" IS DISTINCT FROM @variantId OR
                           "Sku" IS DISTINCT FROM @sku OR
                           "ProductName" IS DISTINCT FROM @productName OR
                           "VariantInfo" IS DISTINCT FROM @variantInfo OR
                           "Quantity" IS DISTINCT FROM @quantity OR
                           "UnitPrice" IS DISTINCT FROM @unitPrice OR
                           "Subtotal" IS DISTINCT FROM @subtotal OR
                           "DiscountAmount" IS DISTINCT FROM @discount OR
                           "Total" IS DISTINCT FROM @total OR
                           "Status" IS DISTINCT FROM @status
                       )
                    """, ct,
                    ("id", itemId), ("legacyId", line.Source.Id), ("orderId", order.Id),
                    ("variantId", line.VariantId), ("sku", FirstNotEmpty(line.Source.Barcode, line.Source.ProductCode, "-")),
                    ("productName", FirstNotEmpty(line.Source.ProductName, line.Source.ProductCode, "Legacy Ürün")),
                    ("variantInfo", $"{line.Source.Color} {line.Source.VariantValue}".Trim()),
                    ("quantity", line.Source.Quantity), ("unitPrice", line.Source.SellingPrice),
                    ("subtotal", subtotal), ("discount", line.Source.DiscountAmount), ("total", total),
                    ("status", order.Status), ("updatedAt", order.SourceDateUtc));
            }
        }

        var unmatchedCount = unmatched.Values.Sum(x => x.Count);
        if (order.Exists && unmatchedCount > 0)
            throw new InvalidOperationException(
                $"Legacy sipariş {order.Source.Id} için {unmatchedCount} kimliksiz hedef satır güvenle eşleştirilemedi; transaction geri alındı.");

        foreach (var removed in existing.Where(x => x.LegacyId.HasValue && !sourceIds.Contains(x.LegacyId.Value) && !x.IsDeleted))
            changed += await ExecuteAsync(connection, transaction, """
                UPDATE "order".ord_order_items
                   SET "IsDeleted"=true,"DeletedAt"=now(),"UpdatedAt"=now()
                 WHERE "Id"=@id AND "OrderId"=@orderId AND "LegacyOrderLineId"=@legacyId
                """, ct, ("id", removed.Id), ("orderId", order.Id), ("legacyId", removed.LegacyId));
        return changed;
    }

    private static async Task<int> ReconcilePaymentsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, PreparedOrder order, CancellationToken ct)
    {
        var existing = new Dictionary<int, TargetPayment>();
        var untrackedPaymentCount = 0;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                SELECT "Id","LegacyOrderPaymentId","IsDeleted"
                  FROM "order".ord_order_payments
                 WHERE "OrderId"=@orderId
                 FOR UPDATE
                """;
            command.Parameters.AddWithValue("orderId", order.Id);
            await using var dbReader = await command.ExecuteReaderAsync(ct);
            while (await dbReader.ReadAsync(ct))
            {
                if (dbReader.IsDBNull(1))
                {
                    if (!dbReader.GetBoolean(2)) untrackedPaymentCount++;
                    continue;
                }
                existing[dbReader.GetInt32(1)] = new(dbReader.GetGuid(0), dbReader.GetInt32(1), dbReader.GetBoolean(2));
            }
        }

        if (order.Exists && untrackedPaymentCount > 0 && order.Payments.Count > 0)
            throw new InvalidOperationException(
                $"Legacy sipariş {order.Source.Id} için {untrackedPaymentCount} kimliksiz hedef ödeme bulundu; transaction geri alındı.");

        var changed = 0;
        var sourceIds = order.Payments.Select(x => x.Source.Id).ToHashSet();
        foreach (var payment in order.Payments)
        {
            existing.TryGetValue(payment.Source.Id, out var target);
            if (target is { IsDeleted: true })
                throw new InvalidOperationException($"Legacy ödeme silinmiş; yeniden açılmadı: {payment.Source.Id}");
            var details = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["legacyPaymentTypeId"] = payment.Source.PaymentTypeId,
                ["legacyPaymentTypeCode"] = payment.Source.PaymentTypeCode,
                ["legacyPaymentTypeTitle"] = payment.Source.PaymentTypeTitle,
                ["description"] = payment.Source.Description,
                ["installmentCount"] = payment.Source.InstallmentCount,
                ["gibCode"] = payment.Source.GibCode
            });
            var status = payment.Source.IsPaid ? "completed" : "pending";
            if (target is null)
            {
                changed += await ExecuteAsync(connection, transaction, """
                    INSERT INTO "order".ord_order_payments
                        ("Id","LegacyOrderPaymentId","OrderId","PaymentMethodId","Amount","CurrencyCode",
                         "Status","Details","CreatedAt","IsDeleted")
                    VALUES (@id,@legacyId,@orderId,@methodId,@amount,@currency,@status,CAST(@details AS jsonb),@createdAt,false)
                    """, ct,
                    ("id", Guid.NewGuid()), ("legacyId", payment.Source.Id), ("orderId", order.Id),
                    ("methodId", payment.PaymentMethodId), ("amount", payment.Source.Amount),
                    ("currency", string.IsNullOrWhiteSpace(order.Source.Currency) ? "TRY" : order.Source.Currency[..Math.Min(3, order.Source.Currency.Length)].ToUpperInvariant()),
                    ("status", status), ("details", details), ("createdAt", order.SourceDateUtc));
            }
            else
            {
                changed += await ExecuteAsync(connection, transaction, """
                    UPDATE "order".ord_order_payments SET
                        "PaymentMethodId"=@methodId,"Amount"=@amount,"CurrencyCode"=@currency,
                        "Status"=@status,"Details"=CAST(@details AS jsonb),"UpdatedAt"=@updatedAt
                     WHERE "Id"=@id AND "OrderId"=@orderId AND "LegacyOrderPaymentId"=@legacyId AND NOT "IsDeleted"
                       AND (
                           "PaymentMethodId" IS DISTINCT FROM @methodId OR
                           "Amount" IS DISTINCT FROM @amount OR
                           "CurrencyCode" IS DISTINCT FROM @currency OR
                           "Status" IS DISTINCT FROM @status OR
                           "Details" IS DISTINCT FROM CAST(@details AS jsonb)
                       )
                    """, ct,
                    ("id", target.Id), ("legacyId", payment.Source.Id), ("orderId", order.Id),
                    ("methodId", payment.PaymentMethodId), ("amount", payment.Source.Amount),
                    ("currency", string.IsNullOrWhiteSpace(order.Source.Currency) ? "TRY" : order.Source.Currency[..Math.Min(3, order.Source.Currency.Length)].ToUpperInvariant()),
                    ("status", status), ("details", details), ("updatedAt", order.SourceDateUtc));
            }
        }

        foreach (var removed in existing.Values.Where(x => !sourceIds.Contains(x.LegacyId) && !x.IsDeleted))
            changed += await ExecuteAsync(connection, transaction, """
                UPDATE "order".ord_order_payments
                   SET "IsDeleted"=true,"DeletedAt"=now(),"UpdatedAt"=now()
                 WHERE "Id"=@id AND "OrderId"=@orderId AND "LegacyOrderPaymentId"=@legacyId
                """, ct, ("id", removed.Id), ("orderId", order.Id), ("legacyId", removed.LegacyId));
        return changed;
    }

    private static string ItemSignature(TargetItem item) =>
        $"{item.VariantId:N}|{item.Quantity}|{item.UnitPrice:0.00}|{item.DiscountAmount:0.00}";

    private static string ItemSignature(PreparedLine item) =>
        $"{item.VariantId:N}|{item.Source.Quantity}|{item.Source.SellingPrice:0.00}|{item.Source.DiscountAmount:0.00}";

    private static string? PaymentMethodCode(int sourceId) => sourceId switch
    {
        1 => "credit_card",
        2 or 3 => "cash_on_delivery",
        _ => null
    };

    private static string? PaymentMethodValue(int sourceId) => sourceId switch
    {
        1 => "card",
        2 => "kapida-nakit",
        3 => "kapida-kart",
        _ => null
    };

    private static async Task<int> ExecuteAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken ct,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
            AddParameter(command, name, value);
        return await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<Guid?> ScalarGuidAsync(
        NpgsqlConnection connection, string sql, CancellationToken ct,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            AddParameter(command, name, value);
        var result = await command.ExecuteScalarAsync(ct);
        return result is null or DBNull ? null : (Guid)result;
    }

    private static void AddParameter(NpgsqlCommand command, string name, object? value)
    {
        if (value is not null)
        {
            command.Parameters.AddWithValue(name, value);
            return;
        }

        var type = name is "memberId" or "shippingNeighborhoodId"
            ? NpgsqlDbType.Uuid
            : NpgsqlDbType.Text;
        command.Parameters.Add(name, type).Value = DBNull.Value;
    }

    private DateTime? Utc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } utc => utc,
        { Kind: DateTimeKind.Local } local => local.ToUniversalTime(),
        { } unspecified => TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(unspecified, DateTimeKind.Unspecified), _sourceTimeZone)
    };

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) when (id == "Europe/Istanbul")
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
        }
    }

    private static string Key(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = value.Trim().Normalize(NormalizationForm.FormKC).ToUpper(Turkish);
        return string.Join(' ', normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string FirstNotEmpty(params string?[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record GeoCountry(Guid Id, string Code, string Name);
    private sealed record GeoCity(Guid Id, Guid CountryId, string Name);
    private sealed record GeoDistrict(Guid Id, Guid CityId, string Name);
    private sealed record TargetOrder(Guid Id, int? LegacyOrderId, Guid FirmPlatformId, string OrderNumber, bool IsDeleted);
    private sealed record TargetItem(Guid Id, int? LegacyId, Guid VariantId, int Quantity, decimal UnitPrice, decimal DiscountAmount, bool IsDeleted);
    private sealed record TargetPayment(Guid Id, int LegacyId, bool IsDeleted);
    private sealed record ResolvedAddress(
        LegacyOrderAddressSourceRow Source, Guid CountryId, Guid CityId, Guid DistrictId, Guid? NeighborhoodId);
    private sealed record PreparedLine(LegacyOrderLineSourceRow Source, Guid VariantId);
    private sealed record PreparedPayment(LegacyOrderPaymentSourceRow Source, Guid PaymentMethodId);
    private sealed record PreparedOrder(
        LegacyOrderSourceRow Source, Guid Id, bool Exists, Guid PlatformId, Guid? MemberId,
        string Status, string PaymentStatus, string PaymentMethod, ResolvedAddress Shipping,
        ResolvedAddress Billing, DateTime SourceDateUtc, IReadOnlyList<PreparedLine> Lines,
        IReadOnlyList<PreparedPayment> Payments);
    private sealed record TargetReferences(
        Guid PlatformId,
        IReadOnlyDictionary<int, Guid> Members,
        IReadOnlyDictionary<string, Guid> Variants,
        IReadOnlyDictionary<string, Guid> PaymentMethods,
        IReadOnlyDictionary<string, GeoCountry> Countries,
        IReadOnlyDictionary<string, GeoCity> Cities,
        IReadOnlyDictionary<string, GeoDistrict> Districts,
        IReadOnlyDictionary<string, Guid> Neighborhoods,
        IReadOnlyDictionary<int, TargetOrder> ExistingByLegacyId,
        IReadOnlyDictionary<string, TargetOrder> OrderNumberOwners);
}
