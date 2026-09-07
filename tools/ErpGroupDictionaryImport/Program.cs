using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Npgsql;

// One-off, insert-only maintenance. Both loopback forwards must belong to this task.
var mode = args.SingleOrDefault() ?? "Inspect";
if (mode is not ("Inspect" or "Rehearse" or "Apply")) throw new Exception("Invalid mode");
using var cfg = JsonDocument.Parse(File.ReadAllText("appsettingsTest.json"));
var cs = cfg.RootElement.GetProperty("ConnectionStrings");
var sourceConfig = new SqlConnectionStringBuilder(cs.GetProperty("ErpSource").GetString());
var targetConfig = new NpgsqlConnectionStringBuilder(cs.GetProperty("DefaultConnection").GetString());
if (sourceConfig.DataSource.Split(',')[0] is not ("192.168.0.100" or "135.125.172.93") || targetConfig.Host != "192.168.0.241" || targetConfig.Database != "ecommerce_db")
    throw new Exception("Unexpected source or target; nothing written");
sourceConfig.DataSource = "127.0.0.1,31433";
sourceConfig.ApplicationIntent = ApplicationIntent.ReadOnly;
sourceConfig.ConnectTimeout = 10;
targetConfig.Host = "127.0.0.1"; targetConfig.Port = 35432; targetConfig.Timeout = 10;
targetConfig.Options = "-c statement_timeout=30000 -c lock_timeout=5000" + (mode == "Inspect" ? " -c default_transaction_read_only=on" : "");
var sourceGroups = new List<Group>();
await using (var sql = new SqlConnection(sourceConfig.ConnectionString))
{
    await sql.OpenAsync();
    await using (var identity = new SqlCommand("SELECT DB_NAME(),CONVERT(varchar(48),CONNECTIONPROPERTY('local_net_address'))", sql))
    await using (var row = await identity.ExecuteReaderAsync())
    {
        if (!await row.ReadAsync() || row.GetString(0) != sourceConfig.InitialCatalog || row.GetString(1) != "192.168.0.100")
            throw new Exception("V3 tunnel identity rejected; nothing written");
    }
    await using var command = new SqlCommand("""
        SELECT LTRIM(RTRIM(CONVERT(nvarchar(100),AttributeCode))), LTRIM(RTRIM(AttributeDescription))
        FROM dbo.cdItemAttributeDesc
        WHERE ItemTypeCode=1 AND AttributeTypeCode=2 AND LangCode='TR'
        ORDER BY AttributeCode
        """, sql) { CommandTimeout = 30 };
    await using var rows = await command.ExecuteReaderAsync();
    while (await rows.ReadAsync()) sourceGroups.Add(new(rows.GetString(0), rows.GetString(1)));
}
if (sourceGroups.Count == 0 || sourceGroups.Any(g => string.IsNullOrWhiteSpace(g.Code) || string.IsNullOrWhiteSpace(g.Name)
    || g.Code.Length > 100 || g.Name.Length > 300) || sourceGroups.GroupBy(g => g.Code).Any(g => g.Count() != 1))
    throw new Exception("Empty/duplicate/invalid source dictionary; nothing written");
await using var pg = new NpgsqlConnection(targetConfig.ConnectionString);
await pg.OpenAsync();
await using var tx = await pg.BeginTransactionAsync();
await using (var identity = new NpgsqlCommand("SELECT current_database()='ecommerce_db' AND inet_server_addr()='192.168.0.241'::inet", pg, tx))
    if (await identity.ExecuteScalarAsync() is not true) throw new Exception("Target identity rejected");
async Task Exec(string sql)
{
    await using var c = new NpgsqlCommand(sql, pg, tx); await c.ExecuteNonQueryAsync();
}
if (mode != "Inspect")
{
    await Exec("SELECT pg_advisory_xact_lock(hashtextextended('erp-group-mapping:erp:nebim',8317))");
    await Exec("LOCK TABLE integration.erp_reference_items, integration.marketplace_category_mappings IN SHARE ROW EXCLUSIVE MODE");
    await Exec("LOCK TABLE definition.product_groups IN SHARE MODE");
    await Exec("CREATE TEMP TABLE original_dictionary ON COMMIT DROP AS SELECT * FROM integration.erp_reference_items");
    await Exec("CREATE TEMP TABLE original_mappings ON COMMIT DROP AS SELECT * FROM integration.marketplace_category_mappings");
}
var targets = new List<TargetGroup>();
await using (var c = new NpgsqlCommand("""SELECT "Id","Code",COALESCE("NameI18n"->>'tr','') FROM definition.product_groups WHERE "IsActive" AND NOT "IsDeleted" """, pg, tx))
await using (var r = await c.ExecuteReaderAsync())
    while (await r.ReadAsync()) targets.Add(new(r.GetGuid(0), r.GetString(1), r.GetString(2)));
var existingCodes = new HashSet<string>(StringComparer.Ordinal);
var activeCodes = new HashSet<string>(StringComparer.Ordinal);
var activeDictionaryNames = new List<Group>();
await using (var c = new NpgsqlCommand("""SELECT "Code","Name","IsActive","IsDeleted" FROM integration.erp_reference_items WHERE "TargetSystem"='erp:nebim' AND "Kind"='product_group' """, pg, tx))
await using (var r = await c.ExecuteReaderAsync())
    while (await r.ReadAsync())
    {
        existingCodes.Add(r.GetString(0));
        if (r.GetBoolean(2) && !r.GetBoolean(3)) activeDictionaryNames.Add(new(r.GetString(0), r.GetString(1)));
        if (r.GetBoolean(2) && !r.GetBoolean(3) && sourceGroups.Any(g => g.Code == r.GetString(0) && g.Name == r.GetString(1))) activeCodes.Add(r.GetString(0));
    }
var occupiedGroups = new HashSet<Guid>();
var referencedCodes = new HashSet<string>(StringComparer.Ordinal);
var existingPairs = new HashSet<(string, Guid)>();
await using (var c = new NpgsqlCommand("""SELECT "ProductGroupId","TargetExternalId","RulesJson","PoolJson","IsDeleted","Status" FROM integration.marketplace_category_mappings WHERE "Marketplace"='erp:nebim' AND "FirmPlatformId" IS NULL """, pg, tx))
await using (var r = await c.ExecuteReaderAsync())
    while (await r.ReadAsync())
    {
        var id = r.GetGuid(0); occupiedGroups.Add(id);
        var codes = new HashSet<string>();
        if (!r.IsDBNull(1)) codes.Add(r.GetString(1));
        for (var column = 2; column <= 3; column++)
        {
            if (r.IsDBNull(column)) continue;
            using var json = JsonDocument.Parse(r.GetString(column));
            if (json.RootElement.ValueKind == JsonValueKind.Null) continue;
            foreach (var entry in json.RootElement.EnumerateArray())
                if (entry.TryGetProperty(column == 2 ? "targetExternalId" : "externalId", out var code) && code.ValueKind == JsonValueKind.String) codes.Add(code.GetString()!);
        }
        foreach (var rawCode in codes)
        {
            var code = rawCode.Trim();
            referencedCodes.Add(code);
            if (!r.GetBoolean(4) && r.GetString(5) == "active") existingPairs.Add((code, id));
        }
    }
var sourceByName = sourceGroups.GroupBy(g => Normalize(g.Name)).ToDictionary(g => g.Key, g => g.ToArray());
var combinedNames = activeDictionaryNames.Concat(sourceGroups.Where(g => !existingCodes.Contains(g.Code)))
    .GroupBy(g => Normalize(g.Name)).ToDictionary(g => g.Key, g => g.Select(x => x.Code).Distinct().Count());
var targetByName = targets.GroupBy(g => Normalize(g.Name)).ToDictionary(g => g.Key, g => g.ToArray());
var report = new List<object>(); int dictionaryAdded = 0, mappingsAdded = 0, alreadyMapped = 0;
var newDictionary = new List<Group>();
var newMappings = new List<NewMapping>();
foreach (var source in sourceGroups)
{
    var dictionaryNew = !existingCodes.Contains(source.Code);
    if (dictionaryNew)
    {
        dictionaryAdded++;
        newDictionary.Add(source);
    }
    var matches = targetByName.GetValueOrDefault(Normalize(source.Name)) ?? [];
    string? reason = null;
    if (!dictionaryNew && !activeCodes.Contains(source.Code)) reason = "Mevcut sözlük kaydı farklı/pasif/silinmiş; korundu";
    else if (sourceByName[Normalize(source.Name)].Length != 1) reason = "V3'te aynı adlı birden fazla kod";
    else if (combinedNames.GetValueOrDefault(Normalize(source.Name)) != 1) reason = "ERP sözlüğünde aynı adlı birden fazla kod";
    else if (matches.Length != 1) reason = matches.Length == 0 ? "Birebir aynı adlı ürün grubu yok" : "Hedefte aynı adlı birden fazla grup";
    else if (existingPairs.Contains((source.Code, matches[0].Id)) && existingPairs.Count(p => p.Item1 == source.Code) == 1) { alreadyMapped++; reason = "Zaten eşli"; }
    else if (referencedCodes.Contains(source.Code) || occupiedGroups.Contains(matches[0].Id)) reason = "Mevcut eşleme/korunan geçmiş kayıt var; ezilmedi";
    if (reason is null)
    {
        var target = matches[0]; mappingsAdded++; occupiedGroups.Add(target.Id); referencedCodes.Add(source.Code);
        newMappings.Add(new(target.Id, source.Code, source.Name));
    }
    report.Add(new { source.Code, source.Name, Target = reason is null || reason == "Zaten eşli" ? matches[0].Name : null, Reason = reason });
}
if (mode != "Inspect")
{
    await using var insert = new NpgsqlCommand("""
        INSERT INTO integration.erp_reference_items
        ("Id","TargetSystem","Kind","Code","Name","IsActive","FirstSeenAt","LastSeenAt","Source","RawJson","CreatedAt","IsDeleted")
        SELECT gen_random_uuid(),'erp:nebim','product_group',x."Code",x."Name",true,now(),now(),'sync',
        '{"table":"dbo.cdItemAttributeDesc","itemTypeCode":1,"attributeTypeCode":2,"langCode":"TR","import":"2026-09-07"}'::jsonb,now(),false
        FROM jsonb_to_recordset(@dictionary::jsonb) x("Code" text,"Name" text);
        INSERT INTO integration.marketplace_category_mappings
        ("Id","Marketplace","ProductGroupId","MappingKind","TargetExternalId","TargetName","TargetPath","Status","CreatedAt","IsDeleted")
        SELECT gen_random_uuid(),'erp:nebim',x."GroupId",'direct',x."Code",x."Name",x."Name",'active',now(),false
        FROM jsonb_to_recordset(@mappings::jsonb) x("GroupId" uuid,"Code" text,"Name" text);
        """, pg, tx);
    insert.Parameters.AddWithValue("dictionary", JsonSerializer.Serialize(newDictionary));
    insert.Parameters.AddWithValue("mappings", JsonSerializer.Serialize(newMappings));
    if (await insert.ExecuteNonQueryAsync() != dictionaryAdded + mappingsAdded)
        throw new Exception("Unexpected insert count; transaction will roll back");
    await Exec("""
    DO $$ BEGIN
      IF EXISTS(SELECT * FROM original_dictionary EXCEPT SELECT * FROM integration.erp_reference_items)
        OR EXISTS(SELECT * FROM original_mappings EXCEPT SELECT * FROM integration.marketplace_category_mappings)
      THEN RAISE EXCEPTION 'Existing rows changed'; END IF;
    END $$;
    """);
}
if (mode == "Apply") await tx.CommitAsync(); else await tx.RollbackAsync();
Console.WriteLine("REPORT:" + JsonSerializer.Serialize(new { Mode=mode, SourceCount=sourceGroups.Count, TargetCount=targets.Count, DictionaryAdded=dictionaryAdded, MappingsAdded=mappingsAdded, AlreadyMapped=alreadyMapped, Items=report }));
static string Normalize(string value) => string.Join(' ', value.Trim().ToLower(CultureInfo.GetCultureInfo("tr-TR")).Split(' ', StringSplitOptions.RemoveEmptyEntries));
record Group(string Code, string Name);
record TargetGroup(Guid Id, string Code, string Name);
record NewMapping(Guid GroupId, string Code, string Name);
