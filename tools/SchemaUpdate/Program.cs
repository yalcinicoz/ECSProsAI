using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using ECSPros.Core.Infrastructure.Persistence;
using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Inventory.Infrastructure.Persistence;
using ECSPros.Catalog.Infrastructure.Persistence;

var cs = Environment.GetEnvironmentVariable("ECSPROS_SCHEMA_TARGET") ?? throw new Exception("Target required");
var b = new NpgsqlConnectionStringBuilder(cs);
if (b.Host != "127.0.0.1" || b.Port != 15432 || b.Database != "ecommerce_db") throw new Exception("Unexpected target");
await using var ds = new NpgsqlDataSourceBuilder(cs).EnableDynamicJson().Build();
DbContextOptions<T> Opt<T>(string schema) where T : DbContext => new DbContextOptionsBuilder<T>()
    .UseNpgsql(ds, o => o.MigrationsHistoryTable("__ef_migrations_" + schema, schema).CommandTimeout(120)).Options;
var contexts = new DbContext[] { new CoreDbContext(Opt<CoreDbContext>("core")), new OrderDbContext(Opt<OrderDbContext>("order")),
    new InventoryDbContext(Opt<InventoryDbContext>("inventory")), new CatalogDbContext(Opt<CatalogDbContext>("catalog")) };
var allowed = new HashSet<string> {
    "20260910170000_SeedUndeliveredReturnReason", "20260910190000_SeedLegacyReturnReasons",
    "20260910142406_AddOrderInstallment", "20260910171457_AddReturnFlowFields", "20260910204534_AddReturnRefundBank",
    "20260910200527_AddBinCounts", "20260911111739_AddProductStatsMaterializedView" };
try {
    if (args.Contains("--return-fix")) {
        await using var connection = await ds.OpenConnectionAsync();
        await using var tx = await connection.BeginTransactionAsync();
        async Task<int> Execute(string sql) {
            await using var command = new NpgsqlCommand(sql, connection, tx);
            command.CommandTimeout=30;
            return await command.ExecuteNonQueryAsync();
        }
        await using (var check = new NpgsqlCommand("""
            SELECT count(*) FROM core.core_lookup_values
            WHERE "Id"='a1d3c0de-7e51-4d1e-9a9e-0000f1ade001' AND NOT "IsDeleted"
            """, connection, tx)) {
            if (Convert.ToInt64(await check.ExecuteScalarAsync()) != 1) throw new Exception("Required reason missing");
        }
        // Only the 13 audited imported returns; metadata confirms original source type and identity.
        await Execute("""
            SELECT "Id" FROM "order".ord_returns
            WHERE "LegacyReturnId" IN (197069,199473,200369,209811,214171,214195,214799,215265,215401,217572,218529,219491,221201)
            FOR UPDATE
            """);
        var headers = await Execute("""
            UPDATE "order".ord_returns SET "ReturnType" = CASE "ReturnType"
                WHEN 'legacy_type_1' THEN 'undelivered' ELSE 'customer' END, "UpdatedAt"=now()
            WHERE NOT "IsDeleted"
              AND "LegacyReturnId" IN (197069,199473,200369,209811,214171,214195,214799,215265,215401,217572,218529,219491,221201)
              AND "ReturnType" IN ('legacy_type_1','legacy_type_2')
              AND "InspectionNotes"::jsonb->'legacyImport'->>'returnId' = "LegacyReturnId"::text
              AND "ReturnType" = 'legacy_type_' || ("InspectionNotes"::jsonb->'legacyImport'->>'rawType')
            """);
        var items = await Execute("""
            UPDATE "order".ord_return_items i SET "ReturnReasonId"='a1d3c0de-7e51-4d1e-9a9e-0000f1ade001', "UpdatedAt"=now()
            FROM "order".ord_returns r, core.core_return_reasons reason
            WHERE i."ReturnId"=r."Id" AND i."ReturnReasonId"=reason."Id"
              AND reason."Code"='legacy_not_delivered' AND NOT reason."IsDeleted"
              AND NOT i."IsDeleted" AND NOT r."IsDeleted" AND i."LegacyReturnItemId" IS NOT NULL
              AND r."LegacyReturnId" IN (197069,199473,200369,209811,217572,218529,219491,221201)
              AND r."ReturnType"='undelivered'
              AND r."InspectionNotes"::jsonb->'legacyImport'->>'returnId'=r."LegacyReturnId"::text
              AND r."InspectionNotes"::jsonb->'legacyImport'->>'rawType'='1'
            """);
        if (headers != 13 || items != 14) throw new Exception($"Changed counts unexpected ({headers}/{items}); rolling back");
        await tx.CommitAsync();
        Console.WriteLine($"Committed: return types={headers}; undelivered item reasons={items}; monetary/status fields unchanged");
        return;
    }
    if (args.Contains("--return-audit")) {
        await using var connection = await ds.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            SELECT r."LegacyReturnId", r."ReturnType", r."Status",
                   i."LegacyReturnItemId", rr."Code", i."ReturnReasonId",
                   r."InspectionNotes"
            FROM "order".ord_returns r
            LEFT JOIN "order".ord_return_items i ON i."ReturnId"=r."Id" AND NOT i."IsDeleted"
            LEFT JOIN core.core_return_reasons rr ON rr."Id"=i."ReturnReasonId"
            WHERE NOT r."IsDeleted" AND r."LegacyReturnId" IS NOT NULL
            ORDER BY r."LegacyReturnId", i."LegacyReturnItemId"
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) {
            string raw = "unknown";
            if (!reader.IsDBNull(6)) try {
                using var json = System.Text.Json.JsonDocument.Parse(reader.GetString(6));
                if (json.RootElement.TryGetProperty("legacyImport", out var legacy) && legacy.TryGetProperty("rawType", out var type)) raw=type.ToString();
            } catch (System.Text.Json.JsonException) { }
            Console.WriteLine(string.Join(" | ", Enumerable.Range(0,6).Select(i=>reader.IsDBNull(i)?"null":reader.GetValue(i).ToString())) + " | rawType=" + raw);
        }
        return;
    }
    if (args.Contains("--return-seeds")) {
        var core = contexts[0];
        await using var transaction = await core.Database.BeginTransactionAsync();
        await core.Database.ExecuteSqlRawAsync("LOCK TABLE core.__ef_migrations_core IN EXCLUSIVE MODE");
        var assembly = core.GetService<IMigrationsAssembly>();
        var history = core.GetService<IHistoryRepository>();
        var applied = (await core.Database.GetAppliedMigrationsAsync()).ToHashSet();
        foreach (var id in new[] { "20260910170000_SeedUndeliveredReturnReason", "20260910190000_SeedLegacyReturnReasons" }) {
            if (applied.Contains(id)) { Console.WriteLine(id + " already applied"); continue; }
            var migration = assembly.CreateMigration(assembly.Migrations[id], core.Database.ProviderName!);
            foreach (var operation in migration.UpOperations) {
                if (operation is not SqlOperation sql || sql.SuppressTransaction) throw new Exception("Unexpected seed operation");
                await using var command = core.Database.GetDbConnection().CreateCommand();
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = sql.Sql;
                command.CommandTimeout = 120;
                var rows = await command.ExecuteNonQueryAsync();
                Console.WriteLine(id + " inserted=" + rows);
            }
            await core.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(id, ProductInfo.GetVersion())));
        }
        var undelivered = await core.Database.SqlQueryRaw<int>("""
            SELECT count(*)::int AS "Value" FROM core.core_lookup_values
            WHERE "Id" = 'a1d3c0de-7e51-4d1e-9a9e-0000f1ade001' AND NOT "IsDeleted"
            """).SingleAsync();
        var legacy = await core.Database.SqlQueryRaw<int>("""
            SELECT count(*)::int AS "Value" FROM core.core_return_reasons
            WHERE "Code" IN ('legacy_defective','legacy_low_quality') AND NOT "IsDeleted"
            """).SingleAsync();
        var preserved = await core.Database.SqlQueryRaw<int>("""
            SELECT count(*)::int AS "Value" FROM information_schema.columns
            WHERE table_schema='core' AND table_name='core_firm_platforms' AND column_name='InvoiceSeriesId'
            """).SingleAsync();
        if (undelivered != 1 || legacy != 2 || preserved != 1) throw new Exception("Seed verification failed; transaction rolled back");
        await transaction.CommitAsync();
        Console.WriteLine("Verified: undelivered=1 legacy=2 InvoiceSeriesId preserved=1");
        Console.WriteLine("Core pending: " + string.Join(", ", await core.Database.GetPendingMigrationsAsync()));
        return;
    }
    var reviewed = new List<DbContext>();
    foreach (var db in contexts) {
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        Console.WriteLine(db.GetType().Name + ": " + string.Join(", ", pending));
        if (pending.Any(x => !allowed.Contains(x))) {
            Console.WriteLine("BLOCKED: unreviewed migration; this context will not be changed");
        } else reviewed.Add(db);
    }
    if (args.Contains("--apply")) foreach (var db in reviewed) {
        await db.Database.MigrateAsync();
        Console.WriteLine(db.GetType().Name + " pending=" + (await db.Database.GetPendingMigrationsAsync()).Count());
    }
} finally { foreach (var db in contexts) await db.DisposeAsync(); }
