using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Npgsql;

var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
var checkOnly = args.Contains("--check", StringComparer.OrdinalIgnoreCase);
if (apply == checkOnly)
    throw new ArgumentException("Tam olarak bir çalışma modu seçilmelidir: --check veya --apply.");
var configPath = Argument("--config") ?? "/opt/ECSProsAI/config/appsettings.Production.json";

var configuration = new ConfigurationBuilder()
    .AddJsonFile(configPath, optional: false)
    .AddEnvironmentVariables()
    .Build();
var connectionString = Required(
    configuration.GetConnectionString("DefaultConnection"), "ConnectionStrings:DefaultConnection");
var publicBase = (configuration["StorefrontMediaStorage:PublicBaseUrl"] ??
                  "https://cdn.misharitalia.com/storefront-v1").TrimEnd('/');
var sourceBase = (configuration["StorefrontMediaMigration:SourceBaseUrl"] ??
                  "https://www.misharitalia.com").TrimEnd('/');

await using var db = new NpgsqlConnection(connectionString);
await db.OpenAsync();
var mappings = await LoadMappingsAsync(db, publicBase);
Console.WriteLine($"MODE={(apply ? "APPLY" : "CHECK")} MAPPINGS={mappings.Count} DISTINCT_SOURCE={mappings.Select(x => x.OldUrl).Distinct().Count()}");
if (mappings.Count == 0) return;

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
foreach (var mapping in mappings)
{
    var source = await http.GetByteArrayAsync(sourceBase + mapping.OldUrl);
    using var response = await http.GetAsync(mapping.NewUrl);
    if (!response.IsSuccessStatusCode)
        throw new InvalidOperationException(
            $"CDN URL erişilemiyor ({(int)response.StatusCode}): {mapping.NewUrl}");
    var cdnBytes = await response.Content.ReadAsByteArrayAsync();
    if (!SHA256.HashData(cdnBytes).SequenceEqual(SHA256.HashData(source)))
        throw new InvalidOperationException($"CDN checksum eşleşmedi: {mapping.NewUrl}");
    Console.WriteLine($"OK {mapping.Kind} {Path.GetFileName(mapping.OldUrl)}");
}

if (!apply)
{
    Console.WriteLine("CHECK_OK: eski kaynak ve yeni CDN URL içerikleri birebir doğrulandı; yazma yapılmadı.");
    return;
}

await ApplyDatabaseAsync(db, mappings);
Console.WriteLine($"APPLY_OK: mappings={mappings.Count}; yeni published snapshot oluşturuldu.");

string? Argument(string name)
{
    var index = Array.FindIndex(args, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

async Task<List<MediaMapping>> LoadMappingsAsync(NpgsqlConnection connection, string cdnBase)
{
    const string sql = """
        SELECT 'desktop', "ImageUrl" FROM storefront.page_block_items
        WHERE NOT "IsDeleted" AND "ImageUrl" LIKE '/media/vitrin/%'
        UNION
        SELECT 'mobile', "MobileImageUrl" FROM storefront.page_block_items
        WHERE NOT "IsDeleted" AND "MobileImageUrl" LIKE '/media/vitrin/%'
        ORDER BY 1, 2
        """;
    var result = new List<MediaMapping>();
    await using var command = new NpgsqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var kind = reader.GetString(0);
        var oldUrl = reader.GetString(1);
        var parts = oldUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || parts[0] != "media" || parts[1] != "vitrin" ||
            parts[2].Length != 6 || !int.TryParse(parts[2], out _))
            throw new InvalidOperationException($"Beklenmeyen eski vitrin URL'si: {oldUrl}");
        var relative = $"pages/{kind}/{parts[2][..4]}/{parts[2][4..]}/{SafeFileName(parts[3])}";
        result.Add(new MediaMapping(kind, oldUrl, relative, $"{cdnBase}/{relative}"));
    }
    return result;
}

async Task ApplyDatabaseAsync(NpgsqlConnection connection, IReadOnlyList<MediaMapping> allMappings)
{
    await using var transaction = await connection.BeginTransactionAsync();
    await new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext('ecspros:storefront-media-migration'))",
        connection, transaction).ExecuteNonQueryAsync();

    foreach (var mapping in allMappings)
    {
        var column = mapping.Kind == "desktop" ? "ImageUrl" : "MobileImageUrl";
        await using var update = new NpgsqlCommand(
            $"UPDATE storefront.page_block_items SET \"{column}\"=$1, \"UpdatedAt\"=now() " +
            $"WHERE NOT \"IsDeleted\" AND \"{column}\"=$2", connection, transaction);
        update.Parameters.AddWithValue(mapping.NewUrl);
        update.Parameters.AddWithValue(mapping.OldUrl);
        await update.ExecuteNonQueryAsync();
    }

    const string activeSql = """
        SELECT "Id", "FirmPlatformId", "Version", "JsonData", "PublishedBy"
        FROM storefront.published_snapshots
        WHERE "IsActive" AND NOT "IsDeleted" FOR UPDATE
        """;
    var snapshots = new List<Snapshot>();
    await using (var command = new NpgsqlCommand(activeSql, connection, transaction))
    await using (var reader = await command.ExecuteReaderAsync())
        while (await reader.ReadAsync())
            snapshots.Add(new Snapshot(reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2),
                reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetGuid(4)));

    foreach (var snapshot in snapshots)
    {
        var json = JsonNode.Parse(snapshot.JsonData) ?? throw new InvalidOperationException("Snapshot JSON boş.");
        ReplaceSnapshotUrls(json, allMappings);
        var nextVersionCommand = new NpgsqlCommand(
            "SELECT coalesce(max(\"Version\"),0)+1 FROM storefront.published_snapshots WHERE \"FirmPlatformId\"=$1",
            connection, transaction);
        nextVersionCommand.Parameters.AddWithValue(snapshot.FirmPlatformId);
        var nextVersion = (int)(await nextVersionCommand.ExecuteScalarAsync() ?? 1);
        var now = DateTime.UtcNow;

        await using var supersede = new NpgsqlCommand(
            "UPDATE storefront.published_snapshots SET \"IsActive\"=false, \"Status\"='superseded', \"UpdatedAt\"=$2 WHERE \"Id\"=$1",
            connection, transaction);
        supersede.Parameters.AddWithValue(snapshot.Id);
        supersede.Parameters.AddWithValue(now);
        await supersede.ExecuteNonQueryAsync();

        await using var insert = new NpgsqlCommand("""
            INSERT INTO storefront.published_snapshots
            ("Id","FirmPlatformId","Version","JsonData","PublishedAt","PublishedBy","IsActive","Status","Note","CreatedAt","IsDeleted")
            VALUES ($1,$2,$3,$4::jsonb,$5,$6,true,'published','Vitrin görselleri CDN taşıması',$5,false)
            """, connection, transaction);
        insert.Parameters.AddWithValue(Guid.NewGuid());
        insert.Parameters.AddWithValue(snapshot.FirmPlatformId);
        insert.Parameters.AddWithValue(nextVersion);
        insert.Parameters.AddWithValue(json.ToJsonString());
        insert.Parameters.AddWithValue(now);
        insert.Parameters.AddWithValue((object?)snapshot.PublishedBy ?? DBNull.Value);
        await insert.ExecuteNonQueryAsync();

        await using var log = new NpgsqlCommand("""
            INSERT INTO storefront.publish_logs
            ("Id","FirmPlatformId","Version","PreviousVersion","PublishedBy","PublishedAt","Status","Note","CreatedAt","IsDeleted")
            VALUES ($1,$2,$3,$4,$5,$6,'success','Vitrin görselleri CDN taşıması',$6,false)
            """, connection, transaction);
        log.Parameters.AddWithValue(Guid.NewGuid());
        log.Parameters.AddWithValue(snapshot.FirmPlatformId);
        log.Parameters.AddWithValue(nextVersion);
        log.Parameters.AddWithValue(snapshot.Version);
        log.Parameters.AddWithValue((object?)snapshot.PublishedBy ?? DBNull.Value);
        log.Parameters.AddWithValue(now);
        await log.ExecuteNonQueryAsync();
    }
    await transaction.CommitAsync();
}

void ReplaceSnapshotUrls(JsonNode node, IReadOnlyList<MediaMapping> allMappings)
{
    if (node is JsonObject obj)
    {
        foreach (var property in obj.ToList())
        {
            var kind = property.Key.Equals("imageUrl", StringComparison.OrdinalIgnoreCase) ? "desktop"
                : property.Key.Equals("mobileImageUrl", StringComparison.OrdinalIgnoreCase) ? "mobile" : null;
            if (kind is not null && property.Value is JsonValue value && value.TryGetValue<string>(out var oldUrl))
            {
                var mapping = allMappings.FirstOrDefault(x => x.Kind == kind && x.OldUrl == oldUrl);
                if (mapping is not null) obj[property.Key] = mapping.NewUrl;
            }
            else if (property.Value is not null) ReplaceSnapshotUrls(property.Value, allMappings);
        }
    }
    else if (node is JsonArray array)
        foreach (var child in array.Where(x => x is not null)) ReplaceSnapshotUrls(child! , allMappings);
}

string SafeFileName(string value) => Path.GetFileName(value) == value && !string.IsNullOrWhiteSpace(value)
    ? value : throw new InvalidOperationException("Geçersiz medya dosya adı.");
string Required(string? value, string key) => !string.IsNullOrWhiteSpace(value)
    ? value.Trim() : throw new InvalidOperationException($"{key} zorunludur.");

record MediaMapping(string Kind, string OldUrl, string RelativeKey, string NewUrl);
record Snapshot(Guid Id, Guid FirmPlatformId, int Version, string JsonData, Guid? PublishedBy);
