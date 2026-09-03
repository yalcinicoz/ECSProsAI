using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Renci.SshNet;
using ECSPros.Iam.Infrastructure.Persistence;

var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
var checkOnly = args.Contains("--check", StringComparer.OrdinalIgnoreCase);
var copy = args.Contains("--copy", StringComparer.OrdinalIgnoreCase);
var repairSftp = args.Contains("--repair-sftp-settings", StringComparer.OrdinalIgnoreCase);
if (new[] { apply, checkOnly, copy, repairSftp }.Count(x => x) != 1)
    throw new ArgumentException("Tam olarak bir çalışma modu seçilmelidir: --copy, --check, --apply veya --repair-sftp-settings.");
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
var sftpBasePath = (configuration["StorefrontMediaStorage:SftpBasePath"] ??
                    "/var/www/html/storefront").TrimEnd('/');

await using var db = new NpgsqlConnection(connectionString);
await db.OpenAsync();
if (repairSftp)
{
    await RepairSftpSettingsAsync(db, configuration, connectionString);
    return;
}
var mappings = await LoadMappingsAsync(db, publicBase);
Console.WriteLine($"MODE={(apply ? "APPLY" : copy ? "COPY" : "CHECK")} MAPPINGS={mappings.Count}");
if (mappings.Count == 0) return;

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
if (copy)
{
    var sftp = await LoadSftpSettingsAsync(db, configuration);
    await CopyFilesAsync(sftp, sftpBasePath, mappings, http, sourceBase);
}

foreach (var mapping in mappings)
{
    using var sourceResponse = await http.GetAsync(sourceBase + mapping.OldUrl);
    if (!sourceResponse.IsSuccessStatusCode)
        throw new InvalidOperationException(
            $"Eski menü görseli erişilemiyor ({(int)sourceResponse.StatusCode}): {mapping.OldUrl}");
    var source = await sourceResponse.Content.ReadAsByteArrayAsync();

    using var cdnResponse = await http.GetAsync(mapping.NewUrl);
    if (!cdnResponse.IsSuccessStatusCode)
        throw new InvalidOperationException(
            $"CDN URL erişilemiyor ({(int)cdnResponse.StatusCode}): {mapping.NewUrl}");
    var cdn = await cdnResponse.Content.ReadAsByteArrayAsync();
    if (!SHA256.HashData(cdn).SequenceEqual(SHA256.HashData(source)))
        throw new InvalidOperationException($"CDN checksum eşleşmedi: {mapping.NewUrl}");
    Console.WriteLine($"OK {mapping.RelativeKey}");
}

if (!apply)
{
    Console.WriteLine(copy
        ? "COPY_OK: eksik dosyalar SFTP origin'e kopyalandı ve public CDN içeriği doğrulandı."
        : "CHECK_OK: eski kaynak ve yeni CDN URL içerikleri birebir doğrulandı; yazma yapılmadı.");
    return;
}

await using var transaction = await db.BeginTransactionAsync();
await new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext('ecspros:menu-media-migration'))",
    db, transaction).ExecuteNonQueryAsync();
var updated = 0;
foreach (var mapping in mappings)
{
    await using var update = new NpgsqlCommand("""
        UPDATE storefront.nav_nodes
        SET "ImageUrl"=$1, "UpdatedAt"=now()
        WHERE NOT "IsDeleted" AND "ImageUrl"=$2
        """, db, transaction);
    update.Parameters.AddWithValue(mapping.NewUrl);
    update.Parameters.AddWithValue(mapping.OldUrl);
    updated += await update.ExecuteNonQueryAsync();
}
await transaction.CommitAsync();
Console.WriteLine($"APPLY_OK: mappings={mappings.Count}; updated_nodes={updated}.");

string? Argument(string name)
{
    var index = Array.FindIndex(args, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

async Task<List<MenuMediaMapping>> LoadMappingsAsync(NpgsqlConnection connection, string cdnBase)
{
    const string sql = """
        SELECT DISTINCT "ImageUrl"
        FROM storefront.nav_nodes
        WHERE NOT "IsDeleted" AND "ImageUrl" LIKE '/media/menu/%'
        ORDER BY "ImageUrl"
        """;
    var result = new List<MenuMediaMapping>();
    await using var command = new NpgsqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var oldUrl = reader.GetString(0);
        const string prefix = "/media/menu/";
        var suffix = oldUrl[prefix.Length..].Replace('\\', '/').Trim('/');
        if (suffix.Length == 0 || suffix.Split('/').Any(x => x is "." or ".." || x.Length == 0))
            throw new InvalidOperationException($"Beklenmeyen eski menü URL'si: {oldUrl}");
        var relativeKey = $"menus/legacy/{suffix}";
        result.Add(new MenuMediaMapping(oldUrl, relativeKey, $"{cdnBase}/{relativeKey}"));
    }
    return result;
}

async Task<SftpSettings> LoadSftpSettingsAsync(NpgsqlConnection connection, IConfiguration config)
{
    var keys = new[]
    {
        "ImageServer.SftpHost", "ImageServer.SftpPort", "ImageServer.SftpUser",
        "ImageServer.SftpPassword"
    };
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    await using var command = new NpgsqlCommand("""
        SELECT "Key", "Value" FROM definition.settings WHERE "Key" = ANY($1)
        """, connection);
    command.Parameters.AddWithValue(keys);
    await using (var reader = await command.ExecuteReaderAsync())
        while (await reader.ReadAsync()) values[reader.GetString(0)] = reader.GetString(1);

    string Get(string key, string? configKey = null, string? fallback = null) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : configKey is not null && !string.IsNullOrWhiteSpace(config[configKey])
                ? config[configKey]!.Trim()
                : fallback is not null
                    ? fallback
                : throw new InvalidOperationException($"{key} zorunludur.");

    string Override(string name, Func<string> fallback) =>
        !string.IsNullOrWhiteSpace(config[$"MenuMediaMigration:Sftp:{name}"])
            ? config[$"MenuMediaMigration:Sftp:{name}"]!.Trim()
            : fallback();

    var protectedPassword = Override("Password",
        () => Get("ImageServer.SftpPassword", "CatalogImageStorage:Sftp:Password"));
    var password = protectedPassword;
    const string protectedPrefix = "dp:v1:";
    if (protectedPassword.StartsWith(protectedPrefix, StringComparison.Ordinal))
    {
        var keysPath = config["DataProtection:KeysPath"] ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ecspros", "dp-keys");
        var provider = DataProtectionProvider.Create(new DirectoryInfo(keysPath),
            options => options.SetApplicationName("ECSPros"));
        password = provider.CreateProtector("ECSPros.Catalog.Settings.Secrets.v1")
            .Unprotect(protectedPassword[protectedPrefix.Length..]);
    }

    return new SftpSettings(
        Override("Host", () => Get("ImageServer.SftpHost", "CatalogImageStorage:Sftp:Host")),
        int.TryParse(Override("Port", () => Get("ImageServer.SftpPort", "CatalogImageStorage:Sftp:Port", "22")), out var port) ? port : 22,
        Override("Username", () => Get("ImageServer.SftpUser", "CatalogImageStorage:Sftp:Username")),
        password);
}

async Task CopyFilesAsync(
    SftpSettings settings, string basePath, IReadOnlyList<MenuMediaMapping> allMappings,
    HttpClient client, string oldBase)
{
    var connection = new PasswordConnectionInfo(
        settings.Host, settings.Port, settings.Username, settings.Password)
    { Timeout = TimeSpan.FromSeconds(30) };
    using var sftp = new SftpClient(connection) { OperationTimeout = TimeSpan.FromSeconds(30) };
    sftp.Connect();
    foreach (var mapping in allMappings)
    {
        using var response = await client.GetAsync(oldBase + mapping.OldUrl);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Eski menü görseli erişilemiyor ({(int)response.StatusCode}): {mapping.OldUrl}");
        var source = await response.Content.ReadAsByteArrayAsync();
        var remotePath = $"{basePath}/{mapping.RelativeKey}";
        EnsureDirectory(sftp, remotePath[..remotePath.LastIndexOf('/')]);
        if (sftp.Exists(remotePath))
        {
            using var existing = new MemoryStream();
            sftp.DownloadFile(remotePath, existing);
            if (!SHA256.HashData(existing.ToArray()).SequenceEqual(SHA256.HashData(source)))
                throw new InvalidOperationException($"Hedefte farklı içerikli dosya var; üzerine yazılmadı: {mapping.RelativeKey}");
            Console.WriteLine($"SKIP {mapping.RelativeKey}");
            continue;
        }

        using var input = new MemoryStream(source, writable: false);
        sftp.UploadFile(input, remotePath, canOverride: false);
        Console.WriteLine($"COPIED {mapping.RelativeKey}");
    }
    sftp.Disconnect();
}

void EnsureDirectory(SftpClient client, string path)
{
    var current = path.StartsWith('/') ? "/" : string.Empty;
    foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
    {
        current = current == "/" ? $"/{segment}" : $"{current}/{segment}";
        if (!client.Exists(current)) client.CreateDirectory(current);
    }
}

async Task RepairSftpSettingsAsync(
    NpgsqlConnection connection, IConfiguration config, string targetConnectionString)
{
    string RequiredMigrationSetting(string name) =>
        !string.IsNullOrWhiteSpace(config[$"MenuMediaMigration:Sftp:{name}"])
            ? config[$"MenuMediaMigration:Sftp:{name}"]!.Trim()
            : throw new InvalidOperationException($"MenuMediaMigration:Sftp:{name} zorunludur.");

    var host = RequiredMigrationSetting("Host");
    var username = RequiredMigrationSetting("Username");
    var password = RequiredMigrationSetting("Password");
    var portText = config["MenuMediaMigration:Sftp:Port"] ?? "22";
    if (!int.TryParse(portText, out var port) || port is < 1 or > 65535)
        throw new InvalidOperationException("MenuMediaMigration:Sftp:Port geçersizdir.");

    var services = new ServiceCollection();
    services.AddDbContext<IamDbContext>(options => options.UseNpgsql(targetConnectionString));
    services.AddDataProtection()
        .PersistKeysToDbContext<IamDbContext>()
        .SetApplicationName("ECSPros");
    await using var serviceProvider = services.BuildServiceProvider();
    var protector = serviceProvider.GetRequiredService<IDataProtectionProvider>()
        .CreateProtector("ECSPros.Catalog.Settings.Secrets.v1");
    var protectedPassword = "dp:v1:" + protector.Protect(password);

    var settings = new Dictionary<string, string>
    {
        ["ImageServer.SftpHost"] = host,
        ["ImageServer.SftpPort"] = port.ToString(),
        ["ImageServer.SftpUser"] = username,
        ["ImageServer.SftpPassword"] = protectedPassword,
    };
    await using var transaction = await connection.BeginTransactionAsync();
    await new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext('ecspros:menu-sftp-settings-repair'))",
        connection, transaction).ExecuteNonQueryAsync();
    foreach (var setting in settings)
    {
        await using var update = new NpgsqlCommand("""
            INSERT INTO definition.settings ("Key", "Value", "UpdatedAt")
            VALUES ($1, $2, now())
            ON CONFLICT ("Key") DO UPDATE SET "Value"=EXCLUDED."Value", "UpdatedAt"=now()
            """, connection, transaction);
        update.Parameters.AddWithValue(setting.Key);
        update.Parameters.AddWithValue(setting.Value);
        await update.ExecuteNonQueryAsync();
    }
    await transaction.CommitAsync();
    Console.WriteLine("REPAIR_OK: SFTP host/port/user ve şifreli parola ayarları güncellendi.");
}

string Required(string? value, string key) => !string.IsNullOrWhiteSpace(value)
    ? value.Trim() : throw new InvalidOperationException($"{key} zorunludur.");

record MenuMediaMapping(string OldUrl, string RelativeKey, string NewUrl);
record SftpSettings(string Host, int Port, string Username, string Password);
