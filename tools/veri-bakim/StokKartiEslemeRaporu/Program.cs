using Microsoft.Data.SqlClient;
using Npgsql;
using NpgsqlTypes;
using System.Text.Json;
// Default SELECT-only. Explicit rehearsed/apply operation affects only group and missing classification.
var action=args.FirstOrDefault() ?? "Read";
if(action is not ("Read" or "Rehearse" or "Apply"))throw new ArgumentException("Unknown action");
var cfg=JsonSerializer.Deserialize<Dictionary<string,string>>(Console.In.ReadToEnd())!;
var vb=new SqlConnectionStringBuilder(cfg["v3"]);
if(vb.DataSource!="135.125.172.93" || vb.InitialCatalog!="Eldi_V3")throw new InvalidOperationException("Source target rejected");
vb.DataSource="127.0.0.1,11433";vb.TrustServerCertificate=true;
cfg["v3"]=vb.ConnectionString;
var pb=new NpgsqlConnectionStringBuilder(cfg["pg"]);
if(pb.Host!="192.168.0.241" || pb.Database!="ecommerce_db")throw new InvalidOperationException("Target rejected");
pb.Host="127.0.0.1";pb.Port=15432;cfg["pg"]=pb.ConnectionString;
var source=new List<object>();
await using(var v3=new SqlConnection(cfg["v3"])) {
 await v3.OpenAsync();
 // Resolve the canonical code using V3's own equality/collation, not guessed lower/upper aliases.
 await using var cmd=new SqlCommand("SELECT a.ItemCode,COALESCE(d.AttributeCode,a.AttributeCode) FROM prItemAttribute a LEFT JOIN cdItemAttributeDesc d ON d.ItemTypeCode=a.ItemTypeCode AND d.AttributeTypeCode=a.AttributeTypeCode AND d.AttributeCode=a.AttributeCode AND d.LangCode='TR' WHERE a.ItemTypeCode=1 AND a.AttributeTypeCode=2",v3){CommandTimeout=60};
 await using var r=await cmd.ExecuteReaderAsync();
 while(await r.ReadAsync())source.Add(new {product=r.GetString(0).Trim(),erp=r.GetString(1).Trim()});
}
await using var pg=new NpgsqlConnection(cfg["pg"]);await pg.OpenAsync();
await using var tx=await pg.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
await using(var ro=new NpgsqlCommand(action=="Read" ? "SET TRANSACTION READ ONLY" : "SET LOCAL lock_timeout='5s'; SET LOCAL statement_timeout='30s'",pg,tx))await ro.ExecuteNonQueryAsync();
await using(var guard=new NpgsqlCommand("SELECT current_database()='ecommerce_db' AND inet_server_addr()=inet '192.168.0.241' AND NOT pg_is_in_recovery()"+(action=="Read" ? " AND current_setting('transaction_read_only')='on'" : ""),pg,tx))
 if(!Equals(await guard.ExecuteScalarAsync(),true))throw new InvalidOperationException("Target identity rejected");
await using var query=new NpgsqlCommand(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,action=="Read" ? "report.sql" : "apply.sql")),pg,tx){CommandTimeout=60};
query.Parameters.Add("source",NpgsqlDbType.Jsonb).Value=JsonSerializer.Serialize(source);
await using var rows=await query.ExecuteReaderAsync();
do { while(await rows.ReadAsync())Console.WriteLine(rows.GetString(0)); } while(await rows.NextResultAsync());
await rows.DisposeAsync();
if(action=="Apply") { await tx.CommitAsync();Console.WriteLine("COMMIT completed"); }
else { await tx.RollbackAsync();Console.WriteLine("ROLLBACK completed"); }
