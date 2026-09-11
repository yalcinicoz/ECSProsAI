using System.Data;
using System.Text.Json;
using ECSPros.Api.Services.AiReporting;
using Npgsql;
using ECSPros.Catalog.Infrastructure.Persistence;
using ECSPros.Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Authorization;

namespace ECSPros.Api.Tests.Acceptance;

/// <summary>Explicit opt-in, source SELECT only. Never runs seeding/startup or report audit writes.</summary>
[TestClass]
public sealed class AiStockReadAcceptanceTests
{
    [TestMethod]
    public async Task CompiledStockQueriesMatchIndependentReadOnlyTotals()
    {
        var raw = Environment.GetEnvironmentVariable("ECSPROS_ACCEPTANCE_AI_STOCK_READ");
        if (string.IsNullOrWhiteSpace(raw)) Assert.Inconclusive("Salt-okunur AI stok kabul bağlantısı verilmedi.");
        var builder = new NpgsqlConnectionStringBuilder(raw)
        {
            Options = "-c default_transaction_read_only=on -c statement_timeout=15000",
            Timeout = 10, CommandTimeout = 20, Pooling = false,
            ApplicationName = "ECSPros-AI-Stock-Readonly-Acceptance"
        };
        Assert.AreEqual("127.0.0.1", builder.Host, "Yalnız onaylı loopback SSH tüneli kullanılabilir.");
        Assert.AreEqual("ecommerce_db", builder.Database);
        await using var source = new NpgsqlDataSourceBuilder(builder.ConnectionString).EnableDynamicJson().Build();
        await using var connection = await source.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await using (var guard = new NpgsqlCommand("SELECT current_database(), host(inet_server_addr()), current_setting('transaction_read_only')", connection, transaction))
        await using (var reader = await guard.ExecuteReaderAsync())
        {
            Assert.IsTrue(await reader.ReadAsync());
            Assert.AreEqual("ecommerce_db", reader.GetString(0));
            Assert.AreEqual("192.168.0.241", reader.GetString(1), "Beklenen yeni sistem DB hedefi değil.");
            Assert.AreEqual("on", reader.GetString(2));
        }

        var permissions = new HashSet<string> { "reports.ai.use", "inventory.view", "catalog.products.view" };
        await using var catalogContext = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(connection).Options);
        await catalogContext.Database.UseTransactionAsync(transaction);
        var currentCatalog = await new ReportAttributeCatalog(catalogContext).LoadAsync(permissions, default);
        var hintedCatalog = await new ReportAttributeCatalog(catalogContext).LoadForPromptAsync(permissions, "urun grubu tesettur olan urunleri listele", default);
        Assert.IsTrue(hintedCatalog.Any(f => f.Label == "Ortam" && f.Description.Contains("eşleşen gerçek seçenekler")),
            "Gerçek seçenek sözlüğünde tesettürün hangi özelliğe ait olduğu bulunmalı.");
        Assert.IsTrue(hintedCatalog.Any(f => f.Label == "Ortam" && f.MatchedValues?.Count > 0));
        var bindingQuestion = await new ReportAttributeCatalog(catalogContext).CheckBindingsAsync(new DynamicReportPlan {
            Source = "stock", Predicate = new ReportPredicate { Kind = "compare", Field = "productGroup", Operator = "eq", Values = ["tesettur"] }
        }, permissions, default);
        // Compare the binding decision with an independent source lookup.
        await using (var groupCheck = new NpgsqlCommand("""
            SELECT EXISTS (SELECT 1 FROM definition.product_groups WHERE NOT "IsDeleted"
              AND lower("NameI18n"->>'tr') IN ('tesettür', 'tesettur'))
            """, connection, transaction))
        {
            if ((bool)(await groupCheck.ExecuteScalarAsync())!) Assert.IsNull(bindingQuestion);
            else { Assert.IsNotNull(bindingQuestion); StringAssert.Contains(bindingQuestion, "Ortam"); }
        }
        Assert.IsTrue(currentCatalog.Count > 0, "Gerçek özellik sözlüğü boş olamaz.");
        var attributes = new List<ReportField>();
        await using (var definitions = new NpgsqlCommand("""
            SELECT "Id", "Code", "NameI18n"->>'tr' FROM definition.attribute_types
            WHERE NOT "IsDeleted" AND "IsActive" AND "DataType" IN ('select','multi_select')
              AND lower("NameI18n"->>'tr') IN ('cinsiyet','sezon','beden','renk') ORDER BY "Id" LIMIT 8
            """, connection, transaction))
        await using (var reader = await definitions.ExecuteReaderAsync())
            while (await reader.ReadAsync()) attributes.Add(ReportAttributeCatalog.Field(reader.GetGuid(0), reader.GetString(2), reader.GetString(1)));
        Assert.IsTrue(attributes.All(a => currentCatalog.Any(c => c.Id == a.Id)), "Sözlük gerçek örnek özellikleri içermeli.");
        async Task<decimal[]> Run(string[] dimensions, ReportFilter[] filters)
        {
            var json = JsonSerializer.Serialize(new ReportDefinition
            {
                Version = 1, Subject = "stock", Metrics = new[] { "stock.quantity", "stock.reserved", "stock.available" },
                Dimensions = dimensions, Filters = filters, Limit = 1000, Presentation = "table"
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await using var command = StockReportQuery.Create(json, permissions, attributes);
            command.Connection = connection;
            command.Transaction = transaction;
            await using var reader = await command.ExecuteReaderAsync();
            var total = new decimal[3];
            var count = 0;
            while (await reader.ReadAsync())
            {
                Assert.IsTrue(++count <= 1000, "Örnek kabul sınırı aşıldı.");
                for (var i = 0; i < 3; i++) total[i] += Convert.ToDecimal(reader.GetValue(dimensions.Length + i));
            }
            return total;
        }
        async Task<decimal[]> Reference(string predicate, string? code = null)
        {
            await using var command = new NpgsqlCommand("""
                SELECT COALESCE(SUM(s."Quantity"::bigint),0), COALESCE(SUM(s."ReservedQuantity"::bigint),0)
                FROM inventory.inv_stocks s WHERE NOT s."IsDeleted"
                """ + predicate, connection, transaction);
            if (code is not null) command.Parameters.AddWithValue("code", code);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.IsTrue(await reader.ReadAsync());
            var quantity = Convert.ToDecimal(reader.GetValue(0));
            var reserved = Convert.ToDecimal(reader.GetValue(1));
            return new[] { quantity, reserved, quantity - reserved };
        }
        var expected = await Reference("");
        Assert.IsTrue(attributes.Count > 0, "Dinamik özellik kabulü için örnek özellik yok.");
        foreach (var attribute in attributes)
            CollectionAssert.AreEqual(expected, await Run(new[] { attribute.Id, "stockType" }, Array.Empty<ReportFilter>()),
                "Özellik gruplaması stok toplamını değiştiremez.");
        CollectionAssert.AreEqual(expected, await Run(Array.Empty<string>(), Array.Empty<ReportFilter>()));
        CollectionAssert.AreEqual(expected, await Run(new[] { "warehouseId", "stockType" }, Array.Empty<ReportFilter>()));
        CollectionAssert.AreEqual(await Reference(" AND s.\"StockType\"='physical'"),
            await Run(Array.Empty<string>(), new[] { new ReportFilter { Field = "stockType", Operator = "eq", Values = new[] { "physical" } } }));

        string? product;
        await using (var sample = new NpgsqlCommand("""
            SELECT p."Code" FROM inventory.inv_stocks s
            JOIN catalog.product_variants v ON v."Id"=s."VariantId" AND NOT v."IsDeleted"
            JOIN catalog.products p ON p."Id"=v."ProductId" AND NOT p."IsDeleted"
            WHERE NOT s."IsDeleted" LIMIT 1
            """, connection, transaction))
            product = await sample.ExecuteScalarAsync() as string;
        Assert.IsFalse(string.IsNullOrWhiteSpace(product), "Ürün örneği bulunamadı; ürün kırılımı doğrulanamadı.");
        var productExpected = await Reference("""
             AND EXISTS (SELECT 1 FROM catalog.product_variants v
             JOIN catalog.products p ON p."Id"=v."ProductId" AND NOT p."IsDeleted"
             WHERE v."Id"=s."VariantId" AND NOT v."IsDeleted" AND p."Code"=@code)
            """, product);
        CollectionAssert.AreEqual(productExpected, await Run(new[] { "productCode" },
            new[] { new ReportFilter { Field = "productCode", Operator = "eq", Values = new[] { product! } } }));
        await CheckSyntheticAttributeRows(connection, transaction);
        await CheckGridRows(connection, transaction);
        await CheckMovementQueries(connection, transaction);
        await CheckReturnQueries(connection, transaction);
        await CheckCustomerQueries(connection, transaction);
        await CheckStaffQueries(connection, transaction);
        await CheckProductCardQueries(connection, transaction);
        await CheckDynamicStock(connection, transaction, currentCatalog);
        await CheckOrderExecutor(source);
        await transaction.RollbackAsync();
    }

    private static async Task CheckDynamicStock(NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<ReportField> attributes)
    {
        await using var db = new ECSPros.Order.Infrastructure.Persistence.OrderDbContext(
            new DbContextOptionsBuilder<ECSPros.Order.Infrastructure.Persistence.OrderDbContext>().UseNpgsql(connection).Options);
        await db.Database.UseTransactionAsync(transaction);
        var effective = new EfektifYetkiler(true, new());
        var permissions = ReportSourceCatalog.ResolvePermissions(effective);
        var environment = attributes.Single(a => a.Label.Equals("Ortam", StringComparison.OrdinalIgnoreCase));
        var color = attributes.Single(a => a.Label.Equals("Renk", StringComparison.OrdinalIgnoreCase));
        var size = attributes.Single(a => a.Label.Equals("Beden", StringComparison.OrdinalIgnoreCase));
        var plan = DynamicStockDetailTests.Plan([environment, color, size]);
        var dictionary = DynamicStockSource.Dictionary(attributes);
        var rows = DynamicStockSource.Query(db, plan, effective, attributes);
        var actualCount = await rows.LongCountAsync();
        var detail = dictionary.Details(_ => true, r => r.Id).Build(rows, plan.Detail!, new(PageSize: 20), permissions);
        var actual = await detail.Page.ToListAsync();
        Assert.IsTrue(actual.All(r => r.Length == 7 && (decimal)r[5]! >= 5));
        // Independent reference: EXISTS gives set membership without joining multiple assignments into stock.
        await using var command = new NpgsqlCommand("""
            SELECT count(*) FROM (
              SELECT s."VariantId", s."StockType" FROM inventory.inv_stocks s
              WHERE NOT s."IsDeleted" GROUP BY s."VariantId", s."StockType" HAVING sum(s."Quantity"::numeric)>=5
            ) q
            JOIN catalog.product_variants v ON v."Id"=q."VariantId" AND NOT v."IsDeleted"
            LEFT JOIN catalog.products p ON p."Id"=v."ProductId" AND NOT p."IsDeleted"
            WHERE EXISTS (
              SELECT 1 FROM definition.attribute_values av
              WHERE av."AttributeTypeId"=@type AND NOT av."IsDeleted" AND av."IsActive"
                AND lower(translate(btrim(av."NameI18n"->>'tr'),'Iİ','ıi'))='tesettür'
                AND (EXISTS (SELECT 1 FROM catalog.product_variant_attributes va WHERE va."VariantId"=v."Id"
                     AND va."AttributeTypeId"=@type AND NOT va."IsDeleted" AND va."AttributeValueId"=av."Id")
                  OR (NOT EXISTS (SELECT 1 FROM catalog.product_variant_attributes va WHERE va."VariantId"=v."Id"
                     AND va."AttributeTypeId"=@type AND NOT va."IsDeleted")
                    AND EXISTS (SELECT 1 FROM catalog.product_attributes pa WHERE pa."ProductId"=p."Id"
                     AND pa."AttributeTypeId"=@type AND NOT pa."IsDeleted" AND pa."AttributeValueId"=av."Id"))))
            """, connection, transaction);
        command.Parameters.AddWithValue("type", Guid.Parse(environment.Id[10..]));
        Assert.AreEqual(Convert.ToInt64(await command.ExecuteScalarAsync()), actualCount);
        var filtered = dictionary.Details(_ => true, r => r.Id).Build(rows, plan.Detail!,
            new(Filters: [new("stock.quantity", "gte", "10")]), permissions);
        Assert.IsTrue((await filtered.Page.ToListAsync()).All(r => (decimal)r[5]! >= 10));
        var noAttributePlan = new DynamicReportPlan { Version = 2, Source = "stock", StockGrain = "variant",
            Aggregate = new() { Dimensions = ["stockType"], Measures = ["stock.quantity", "stock.reserved", "stock.available"] } };
        var summary = await dictionary.Aggregates(_ => true).Build(DynamicStockSource.Query(db, noAttributePlan, effective, attributes),
            noAttributePlan.Aggregate, permissions).Rows.ToListAsync();
        await using var totals = new NpgsqlCommand("""
            SELECT "StockType", sum("Quantity"::numeric), sum("ReservedQuantity"::numeric)
            FROM inventory.inv_stocks WHERE NOT "IsDeleted" GROUP BY "StockType"
            """, connection, transaction);
        await using var reader = await totals.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = summary.Single(r => r.D0 == reader.GetString(0));
            Assert.AreEqual(reader.GetDecimal(1), row.M0); Assert.AreEqual(reader.GetDecimal(2), row.M1);
            Assert.AreEqual(row.M0 - row.M1, row.M2);
        }
    }

    private static async Task CheckProductCardQueries(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        await using var db = new ECSPros.Order.Infrastructure.Persistence.OrderDbContext(
            new DbContextOptionsBuilder<ECSPros.Order.Infrastructure.Persistence.OrderDbContext>().UseNpgsql(connection).Options);
        await db.Database.UseTransactionAsync(transaction);
        var to = DateTime.UtcNow; var from = to.AddDays(-30);
        var plan = new DynamicReportPlan { Version = 2, Source = ProductCardReportSource.Id, From = from.ToString("O"), To = to.ToString("O"),
            Predicate = new() { Kind = "notExists", Relation = "cards.movements", Children = [new() {
                Kind = "compare", Field = "cardMovements.createdAt", Operator = "gte", Values = [from.ToString("O")] }] },
            Aggregate = new() { Dimensions = [], Measures = ["cards.count"], Direction = "asc" } };
        var rights = new EfektifYetkiler(true, new());
        var count = await ProductCardReportSource.Query(db, plan, rights).LongCountAsync();
        await using var cmd = new NpgsqlCommand("""
            SELECT count(*) FROM catalog.products p WHERE NOT p."IsDeleted"
              AND NOT EXISTS (SELECT 1 FROM catalog.product_variants v
                JOIN inventory.inv_stock_movements m ON m."VariantId"=v."Id"
                WHERE v."ProductId"=p."Id" AND m."CreatedAt">=@from AND m."CreatedAt"<@to)
            """, connection, transaction);
        cmd.Parameters.AddWithValue("from", from); cmd.Parameters.AddWithValue("to", to);
        Assert.AreEqual((long)(await cmd.ExecuteScalarAsync())!, count);
        var withMovement = plan with { Predicate = plan.Predicate! with { Kind = "exists" } };
        var total = await ProductCardReportSource.Query(db, plan with { Predicate = null }, rights).LongCountAsync();
        Assert.AreEqual(total, count + await ProductCardReportSource.Query(db, withMovement, rights).LongCountAsync());
        var cohortFrom = to.AddMonths(-12); var cohortTo = to.AddMonths(-6);
        var initial = plan with { CardWindowMonths = 6, From = cohortFrom.ToString("O"), To = cohortTo.ToString("O"),
            Predicate = plan.Predicate! with { Children = [new() { Kind = "compare", Field = "cardMovements.createdAt", Operator = "gte", Values = [cohortFrom.ToString("O")] }] } };
        var initialCount = await ProductCardReportSource.Query(db, initial, rights, observedAt: new(to)).LongCountAsync();
        await using var initialCmd = new NpgsqlCommand("""
            SELECT count(*) FROM catalog.products p
            WHERE NOT p."IsDeleted" AND p."CreatedAt">=@from AND p."CreatedAt"<@to
              AND ((p."CreatedAt" AT TIME ZONE 'Europe/Istanbul') + interval '6 months') AT TIME ZONE 'Europe/Istanbul' <= @now
              AND NOT EXISTS (SELECT 1 FROM catalog.product_variants v
                JOIN inventory.inv_stock_movements m ON m."VariantId"=v."Id"
                WHERE v."ProductId"=p."Id" AND m."CreatedAt">=p."CreatedAt"
                  AND m."CreatedAt"<((p."CreatedAt" AT TIME ZONE 'Europe/Istanbul') + interval '6 months') AT TIME ZONE 'Europe/Istanbul')
            """, connection, transaction);
        initialCmd.Parameters.AddWithValue("from", cohortFrom); initialCmd.Parameters.AddWithValue("to", cohortTo); initialCmd.Parameters.AddWithValue("now", to);
        Assert.AreEqual((long)(await initialCmd.ExecuteScalarAsync())!, initialCount);
        // Literal-only read: month-end clamping, exclusive boundary, and incomplete-window guard.
        await using var boundary = new NpgsqlCommand("""
            SELECT (timestamp '2024-08-31 12:00' + interval '6 months' = timestamp '2025-02-28 12:00')
              AND NOT (timestamp '2025-02-28 12:00' < timestamp '2024-08-31 12:00' + interval '6 months')
              AND NOT (timestamp '2025-01-31 12:00' + interval '6 months' <= timestamp '2025-06-30 12:00')
            """, connection, transaction);
        Assert.IsTrue((bool)(await boundary.ExecuteScalarAsync())!);
    }

    private static async Task CheckStaffQueries(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        await using var db = new ECSPros.Order.Infrastructure.Persistence.OrderDbContext(
            new DbContextOptionsBuilder<ECSPros.Order.Infrastructure.Persistence.OrderDbContext>().UseNpgsql(connection).Options);
        await db.Database.UseTransactionAsync(transaction);
        var to = DateTime.UtcNow; var from = to.AddDays(-30);
        var plan = new DynamicReportPlan { Version = 2, Source = StaffActivitySource.Id, From = from.ToString("O"), To = to.ToString("O"),
            Aggregate = new() { Dimensions = ["staff.activity"], Measures = ["staff.count"], Direction = "asc" } };
        var rights = new EfektifYetkiler(true, new());
        var rows = StaffActivitySource.Query(db, plan, rights);
        var count = await rows.LongCountAsync();
        // Independent counts; no personnel names or other personal data are returned/logged.
        await using var cmd = new NpgsqlCommand("""
            SELECT (SELECT count(*) FROM fulfillment.ful_operation_logs l
                WHERE NOT l."IsDeleted" AND l."ActorId" <> '00000000-0000-0000-0000-000000000000'::uuid
                  AND l."Action" IN ('line_picked','package_packed') AND l."CreatedAt">=@from AND l."CreatedAt"<@to
                  AND EXISTS (SELECT 1 FROM "order".ord_orders o WHERE o."Id"=l."OrderId" AND NOT o."IsDeleted"))
              + (SELECT count(*) FROM "order".ord_invoices i
                WHERE NOT i."IsDeleted" AND i."LegacyInvoiceId" IS NULL AND i."NumberSource"='internal'
                  AND i."CreatedBy" IS NOT NULL AND i."CreatedBy" <> '00000000-0000-0000-0000-000000000000'::uuid
                  AND i."CreatedAt">=@from AND i."CreatedAt"<@to
                  AND EXISTS (SELECT 1 FROM "order".ord_orders o WHERE o."Id"=i."OrderId" AND NOT o."IsDeleted"))
            """, connection, transaction);
        cmd.Parameters.AddWithValue("from", from); cmd.Parameters.AddWithValue("to", to);
        Assert.AreEqual((long)(await cmd.ExecuteScalarAsync())!, count);
        var summary = await StaffActivitySource.Dictionary.Aggregates(_ => true).Build(rows, plan.Aggregate,
            ReportSourceCatalog.ResolvePermissions(rights)).Rows.ToListAsync();
        Assert.AreEqual((decimal)count, summary.Sum(r => r.M0));
        var none = new EfektifYetkiler(false, new() { ["reports.ai.use"] = [], ["iam.users.view"] = null,
            ["fulfillment.view"] = null, ["orders.invoices.view"] = null });
        Assert.AreEqual(0L, await StaffActivitySource.Query(db, plan, none).LongCountAsync());
    }

    private static async Task CheckCustomerQueries(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        await using var db = new ECSPros.Order.Infrastructure.Persistence.OrderDbContext(
            new DbContextOptionsBuilder<ECSPros.Order.Infrastructure.Persistence.OrderDbContext>().UseNpgsql(connection).Options);
        await db.Database.UseTransactionAsync(transaction);
        var effective = new EfektifYetkiler(true, new());
        var permissions = ReportSourceCatalog.ResolvePermissions(effective);
        var to = DateTime.UtcNow; var from = to.AddDays(-180);
        var plan = new DynamicReportPlan { Version = 2, Source = "customers", From = from.ToString("O"), To = to.ToString("O"),
            Aggregate = new() { Dimensions = [], Measures = ["customers.count", "customers.orderCount", "customers.returnCount"], Direction = "asc" } };
        var rows = CustomerReportSource.Query(db, plan, effective);
        var actual = await CustomerReportSource.Dictionary.Aggregates(_ => true).Build(rows, plan.Aggregate, permissions).Rows.SingleAsync();
        // Independent direct counts, not the report's joins/projection. No names are read or logged here.
        await using (var command = new NpgsqlCommand("""
            SELECT
              (SELECT count(*)::numeric FROM crm.crm_members m WHERE NOT m."IsDeleted" AND m."AnonymizedAt" IS NULL),
              (SELECT count(*)::numeric FROM "order".ord_orders o JOIN crm.crm_members m ON m."Id"=o."MemberId"
               WHERE NOT o."IsDeleted" AND NOT m."IsDeleted" AND m."AnonymizedAt" IS NULL
                 AND o."CreatedAt">=@from AND o."CreatedAt"<@to),
              (SELECT count(*)::numeric FROM "order".ord_returns r JOIN "order".ord_orders o ON o."Id"=r."OrderId"
               JOIN crm.crm_members m ON m."Id"=o."MemberId"
               WHERE NOT r."IsDeleted" AND NOT o."IsDeleted" AND NOT m."IsDeleted" AND m."AnonymizedAt" IS NULL
                 AND r."CreatedAt">=@from AND r."CreatedAt"<@to)
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("from", from); command.Parameters.AddWithValue("to", to);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.IsTrue(await reader.ReadAsync());
            Assert.AreEqual(reader.GetDecimal(0), actual.M0);
            Assert.AreEqual(reader.GetDecimal(1), actual.M1);
            Assert.AreEqual(reader.GetDecimal(2), actual.M2);
        }
        var detail = CustomerReportSource.Dictionary.Details(_ => true, r => r.Id).Build(rows,
            new() { Columns = ["customers.orderCount", "customers.returnCount"], Sort = "customers.returnCount", Direction = "desc", Top = 20 }, new(), permissions);
        Assert.IsTrue((await detail.Page.ToListAsync()).All(r => r.Length == 2 && r.All(v => v is decimal)));
        var noChannels = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["crm.members.view"] = null,
            ["orders.view"] = [], ["orders.returns.view"] = [] });
        Assert.IsFalse(await CustomerReportSource.Query(db, plan, noChannels).AnyAsync(r => r.OrderCount != 0 || r.ReturnCount != 0));
        var crmOnly = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["crm.members.view"] = null });
        Assert.AreEqual((long)actual.M0, await CustomerReportSource.Query(db, plan, crmOnly).LongCountAsync());
        var related = plan with { Predicate = new() { Kind = "exists", Relation = CustomerReportRelations.Orders,
            Children = [new() { Kind = "compare", Field = "orders.status", Operator = "eq", Values = ["delivered"] }] } };
        var selectedCount = await CustomerReportSource.Query(db, related, effective).LongCountAsync();
        await using (var command = new NpgsqlCommand("""
            SELECT count(*) FROM crm.crm_members m
            WHERE NOT m."IsDeleted" AND m."AnonymizedAt" IS NULL
              AND EXISTS (SELECT 1 FROM "order".ord_orders o WHERE o."MemberId"=m."Id"
                AND NOT o."IsDeleted" AND o."Status"='delivered'
                AND o."CreatedAt">=@from AND o."CreatedAt"<@to)
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("from", from); command.Parameters.AddWithValue("to", to);
            Assert.AreEqual((long)(await command.ExecuteScalarAsync())!, selectedCount);
        }
        Assert.AreEqual(0L, await CustomerReportSource.Query(db, related, noChannels).LongCountAsync());
        var excluded = related with { Predicate = related.Predicate! with { Kind = "notExists" } };
        Assert.AreEqual((long)actual.M0, selectedCount + await CustomerReportSource.Query(db, excluded, effective).LongCountAsync());
    }

    private static async Task CheckReturnQueries(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        await using var db = new ECSPros.Order.Infrastructure.Persistence.OrderDbContext(
            new DbContextOptionsBuilder<ECSPros.Order.Infrastructure.Persistence.OrderDbContext>().UseNpgsql(connection).Options);
        await db.Database.UseTransactionAsync(transaction);
        db.Database.SetCommandTimeout(15);
        var allowed = new HashSet<string> { "reports.ai.use", "orders.returns.view" };
        var effective = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["orders.returns.view"] = null });
        var to = DateTime.UtcNow; var from = to.AddDays(-180);
        var plan = new DynamicReportPlan { Version = 2, Source = "returns", From = from.ToString("O"), To = to.ToString("O"),
            Aggregate = new() { Dimensions = ["returns.currencyCode"], Measures = ["returns.count", "returns.amount"] } };
        var rows = ReturnReportSource.Apply(db.Returns, plan, effective);
        var actual = await ReturnReportSource.Dictionary.Aggregates(_ => true).Build(rows, plan.Aggregate, allowed).Rows.ToListAsync();
        var expected = new Dictionary<string, (decimal Count, decimal Amount)>();
        await using (var command = new NpgsqlCommand("""
            SELECT o."CurrencyCode", count(*)::numeric, COALESCE(sum(r."RefundAmount"),0)::numeric
            FROM "order".ord_returns r JOIN "order".ord_orders o ON o."Id"=r."OrderId"
            WHERE NOT r."IsDeleted" AND NOT o."IsDeleted" AND r."CreatedAt">=@from AND r."CreatedAt"<@to
            GROUP BY o."CurrencyCode"
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("from", from); command.Parameters.AddWithValue("to", to);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) expected.Add(reader.GetString(0), (reader.GetDecimal(1), reader.GetDecimal(2)));
        }
        Assert.AreEqual(expected.Count, actual.Count);
        foreach (var row in actual)
        {
            Assert.IsTrue(expected.TryGetValue(row.D0!, out var reference));
            Assert.AreEqual(reference.Count, row.M0); Assert.AreEqual(reference.Amount, row.M1);
        }
        var detail = ReturnReportSource.Dictionary.Details(_ => true, r => r.Id).Build(rows,
            new() { Columns = ["returns.orderNumber", "returns.createdAt"], Direction = "asc" }, new(PageSize: 3), allowed);
        Assert.AreEqual(expected.Values.Sum(x => x.Count), (decimal)await detail.Filtered.LongCountAsync());
        Assert.IsTrue((await detail.Page.ToListAsync()).All(r => r.Length == 2 && r[0] is string && r[1] is DateTime));
    }

    private static async Task CheckMovementQueries(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        // Reuse the target identity/read-only guards and SAME snapshot as the reference SQL.
        await using var db = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>().UseNpgsql(connection).Options);
        await db.Database.UseTransactionAsync(transaction);
        db.Database.SetCommandTimeout(15);
        var permissions = new HashSet<string> { "reports.ai.use", "inventory.view" };
        var effective = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["inventory.view"] = null });
        var to = DateTime.UtcNow; var from = to.AddDays(-30);
        var plan = new DynamicReportPlan { Version = 2, Source = MovementReportSource.Id, From = from.ToString("O"), To = to.ToString("O"),
            Aggregate = new() { Dimensions = ["movements.type"], Measures = ["movements.count", "movements.quantity"] } };
        var rows = MovementReportSource.Apply(db.StockMovements, plan, effective);
        var actual = await MovementReportSource.Dictionary.Aggregates(_ => true).Build(rows, plan.Aggregate, permissions).Rows.ToListAsync();
        var expected = new Dictionary<string, (decimal Count, decimal Quantity)>();
        await using (var command = new NpgsqlCommand("""
            SELECT "MovementType", count(*)::numeric, COALESCE(sum("Quantity"),0)::numeric
            FROM inventory.inv_stock_movements
            WHERE "CreatedAt">=@from AND "CreatedAt"<@to GROUP BY "MovementType"
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("from", from); command.Parameters.AddWithValue("to", to);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) expected.Add(reader.GetString(0), (reader.GetDecimal(1), reader.GetDecimal(2)));
        }
        Assert.AreEqual(expected.Count, actual.Count);
        foreach (var row in actual)
        {
            Assert.IsTrue(expected.TryGetValue(row.D0!, out var reference));
            Assert.AreEqual(reference.Count, row.M0); Assert.AreEqual(reference.Quantity, row.M1);
        }
        var detail = MovementReportSource.Dictionary.Details(_ => true, m => m.Id).Build(rows,
            new() { Columns = ["movements.createdAt", "movements.quantity"], Sort = "movements.createdAt", Direction = "desc" },
            new(PageSize: 3), permissions);
        Assert.AreEqual(expected.Values.Sum(v => v.Count), (decimal)await detail.Filtered.LongCountAsync());
        var page = await detail.Page.ToListAsync();
        Assert.IsTrue(page.Count <= 3);
        Assert.IsTrue(page.All(row => row.Length == 2 && row[0] is DateTime && row[1] is decimal));
        var denied = new EfektifYetkiler(false, new() { ["reports.ai.use"] = [Guid.NewGuid()], ["inventory.view"] = null });
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => MovementReportSource.Apply(db.StockMovements, plan, denied));
        // VALUES-only, not a temp table: also exercise nonempty data even when recent history is empty.
        var fixture = db.StockMovements.FromSqlRaw("""
            SELECT lpad(i::text,32,'0')::uuid AS "Id", lpad('1',32,'0')::uuid AS "VariantId",
                NULL::uuid AS "FromWarehouseId", NULL::uuid AS "ToWarehouseId",
                NULL::uuid AS "FromLocationId", NULL::uuid AS "ToLocationId",
                NULL::uuid AS "FromBinId", NULL::uuid AS "ToBinId",
                'transfer'::text AS "MovementType", quantity AS "Quantity",
                NULL::text AS "ReferenceType", NULL::uuid AS "ReferenceId",
                NULL::text AS "Notes", created AS "CreatedAt", NULL::uuid AS "CreatedBy"
            FROM (VALUES (1,7,TIMESTAMPTZ '2026-09-01T00:00:00Z'),
                         (2,3,TIMESTAMPTZ '2026-09-02T00:00:00Z'),
                         (3,99,TIMESTAMPTZ '2026-10-01T00:00:00Z')) fixture(i,quantity,created)
            """);
        var fixturePlan = plan with { From = "2026-09-01T00:00:00Z", To = "2026-10-01T00:00:00Z" };
        var fixtureRows = MovementReportSource.Apply(fixture, fixturePlan, effective);
        var fixtureResult = await MovementReportSource.Dictionary.Aggregates(_ => true).Build(fixtureRows, plan.Aggregate, permissions).Rows.SingleAsync();
        Assert.AreEqual(2m, fixtureResult.M0); Assert.AreEqual(10m, fixtureResult.M1);
        // No writes, audit, startup/seeding or OpenAI call.
    }

    private static async Task CheckOrderExecutor(NpgsqlDataSource source)
    {
        // The data source is guarded above and enforces default_transaction_read_only.
        var executor = new OrderReportExecutor(source, new OrderAuthorization());
        var summary = await executor.ExecuteAsync(Guid.NewGuid(), OrderReportRecipeTests.Json,
            new ReportGridState(Filters: [new("orders.amount", "gte", "0")]), default);
        Assert.IsTrue(summary.TotalCount >= summary.Rows.Count);
        Assert.IsNotNull(summary.CurrencyTotals);
        Assert.AreEqual(0, summary.Totals!.Count, "Para birimleri tek sayıda toplanamaz.");
        var detailsJson = OrderReportRecipeTests.Json.Replace("[\"orders.status\",\"orders.currencyCode\"]",
            "[\"orders.orderNumber\",\"orders.createdAt\",\"orders.status\",\"orders.currencyCode\"]");
        var details = await executor.ExecuteAsync(Guid.NewGuid(), detailsJson, new ReportGridState(PageSize: 1), default);
        Assert.IsTrue(details.Rows.Count <= 1);
        Assert.AreEqual(details.TotalCount, details.CurrencyTotals!.Sum(t => t.Count));
        var deniedScope = new OrderReportExecutor(source, new OrderAuthorization(empty: true));
        var empty = await deniedScope.ExecuteAsync(Guid.NewGuid(), detailsJson, new ReportGridState(), default);
        Assert.AreEqual(0L, empty.TotalCount);
        Assert.AreEqual(0, empty.Rows.Count);
    }

    private sealed class OrderAuthorization(bool empty = false) : IEtkinYetkiServisi
    {
        public Task<EfektifYetkiler> GetirAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(
            new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["orders.view"] = empty ? new HashSet<Guid>() : null }));
        public Task GecersizKilAsync(Guid userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task GrupIcinGecersizKilAsync(Guid roleId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static async Task CheckGridRows(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        // Generated SELECT fixture only: validates paging beyond the old 1000-row cutoff without writes.
        const string fixture = """
            WITH fs AS (SELECT i AS "VariantId", i AS "Quantity", 0 AS "ReservedQuantity",
              false AS "IsDeleted" FROM generate_series(1,1505) i),
            fv AS (SELECT i AS "Id", i AS "ProductId", false AS "IsDeleted" FROM generate_series(1,1505) i),
            fp AS (SELECT i AS "Id", 'P-' || lpad(i::text,4,'0') AS "Code", false AS "IsDeleted" FROM generate_series(1,1505) i),
            """;
        async Task<(long Count, decimal Total, List<string> Codes)> Run(ReportGridState grid)
        {
            await using var command = ReportGridQuery.Create(ReportGridQueryTests.Recipe,
                new HashSet<string> { "reports.ai.use", "inventory.view" }, [], grid);
            command.CommandText = fixture + command.CommandText[5..]
                .Replace("inventory.inv_stocks", "fs").Replace("catalog.product_variants", "fv").Replace("catalog.products", "fp");
            command.Connection = connection; command.Transaction = transaction;
            await using var reader = await command.ExecuteReaderAsync();
            long count = -1; decimal total = -1;
            var codes = new List<string>();
            while (await reader.ReadAsync())
            {
                count = reader.GetInt64(0); total = reader.GetFieldValue<decimal>(1);
                if (!reader.IsDBNull(4)) codes.Add(reader.GetString(5));
            }
            return (count, total, codes);
        }
        var page = await Run(new(Page: 41));
        Assert.AreEqual(1505L, page.Count);
        Assert.AreEqual(1505m * 1506m / 2m, page.Total);
        Assert.AreEqual(25, page.Codes.Count);
        Assert.AreEqual("P-1001", page.Codes[0]);
        var export = await Run(ReportGridState.ForExport(new()));
        Assert.AreEqual(1505L, export.Count);
        Assert.AreEqual(1505, export.Codes.Count);
        Assert.AreEqual(page.Total, export.Total);
        var match = await Run(new(Search: "P-1505"));
        Assert.AreEqual(1L, match.Count); Assert.AreEqual(1505m, match.Total);
        var numeric = await Run(new(Sort: "stock.quantity", Dir: "desc", Filters: [new("stock.quantity", "between", "1500;1505")]));
        Assert.AreEqual(6L, numeric.Count); Assert.AreEqual(9015m, numeric.Total);
        Assert.AreEqual("P-1505", numeric.Codes[0]);
        var missing = await Run(new(Search: "DOES-NOT-EXIST"));
        Assert.AreEqual(0L, missing.Count); Assert.AreEqual(0m, missing.Total); Assert.AreEqual(0, missing.Codes.Count);
        var pastEnd = await Run(new(Page: 100));
        Assert.AreEqual(1505L, pastEnd.Count); Assert.AreEqual(page.Total, pastEnd.Total); Assert.AreEqual(0, pastEnd.Codes.Count);
    }

    private static async Task CheckSyntheticAttributeRows(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        // VALUES-only CTE fixture: no tables, inserts, DDL, temporary tables or business writes.
        var type = ReportAttributeCatalog.Field(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Cinsiyet", "cinsiyet");
        const string fixture = """
            WITH fs AS (
              SELECT lpad(i::text,32,'0')::uuid AS "VariantId", q AS "Quantity", r AS "ReservedQuantity",
                false AS "IsDeleted", 'physical'::text AS "StockType"
              FROM (VALUES (1,10,1),(2,20,2),(3,30,3)) t(i,q,r)
            ), fv AS (
              SELECT lpad(i::text,32,'0')::uuid AS "Id", lpad('9',32,'0')::uuid AS "ProductId", false AS "IsDeleted"
              FROM (VALUES (1),(2)) t(i)
            ), fp AS (
              SELECT lpad('9',32,'0')::uuid AS "Id", false AS "IsDeleted"
            ), ft AS (
              SELECT 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid AS "Id", false AS "IsDeleted", true AS "IsActive"
            ), fav AS (
              SELECT lpad(i::text,32,'0')::uuid AS "Id", 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid AS "AttributeTypeId",
                jsonb_build_object('tr',label) AS "NameI18n", false AS "IsDeleted", active AS "IsActive"
              FROM (VALUES (11,'Kadın',true),(12,'Erkek',true),(13,'Unisex',true),(14,'Eski',false)) t(i,label,active)
            ), fva AS (
              SELECT lpad('1',32,'0')::uuid AS "VariantId", lpad(i::text,32,'0')::uuid AS "AttributeValueId",
                'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid AS "AttributeTypeId", false AS "IsDeleted"
              FROM (VALUES (12),(12),(13)) t(i)
            ), fpa AS (
              SELECT lpad('9',32,'0')::uuid AS "ProductId", lpad(i::text,32,'0')::uuid AS "AttributeValueId",
                'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid AS "AttributeTypeId", false AS "IsDeleted"
              FROM (VALUES (11),(14)) t(i)
            )
            """;
        async Task<List<(string? Label, decimal Quantity)>> Execute(ReportFilter[] filters)
        {
            await using var command = StockReportQuery.Create(DynamicStockAttributeTests.Recipe(new[] { type.Id }, filters),
                new HashSet<string> { "reports.ai.use", "inventory.view", "catalog.products.view" }, new[] { type });
            command.CommandText = fixture + command.CommandText
                .Replace("inventory.inv_stocks", "fs").Replace("catalog.product_variants", "fv")
                .Replace("catalog.products", "fp").Replace("catalog.product_variant_attributes", "fva")
                .Replace("catalog.product_attributes", "fpa").Replace("definition.attribute_values", "fav")
                .Replace("definition.attribute_types", "ft");
            command.Connection = connection; command.Transaction = transaction;
            var rows = new List<(string?, decimal)>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) rows.Add((reader.IsDBNull(0) ? null : reader.GetString(0), Convert.ToDecimal(reader.GetValue(1))));
            return rows;
        }
        var rows = await Execute(Array.Empty<ReportFilter>());
        Assert.AreEqual(3, rows.Count);
        Assert.AreEqual(60m, rows.Sum(r => r.Quantity));
        Assert.AreEqual(10m, rows.Single(r => r.Label == "Erkek / Unisex").Quantity);
        Assert.AreEqual(20m, rows.Single(r => r.Label == "Kadın").Quantity);
        Assert.AreEqual(30m, rows.Single(r => r.Label is null).Quantity);
        var filtered = await Execute(new[] { new ReportFilter { Field = type.Id, Operator = "eq", Values = new[] { "KADIN" } } });
        Assert.AreEqual(20m, filtered.Single().Quantity);
    }
}
