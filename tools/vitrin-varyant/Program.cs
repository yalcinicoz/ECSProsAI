using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using ECSPros.Iam.Infrastructure.Persistence;
using ImageMagick;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Renci.SshNet;

// Mevcut CDN vitrin görselleri için güvenli ve tekrar çalıştırılabilir responsive varyant backfill'i.
// Orijinallere dokunmaz; yalnız eksik _w480/_w800/_w1200/_w1920.webp dosyalarını SFTP + S3'e yazar.
var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
var check = args.Contains("--check", StringComparer.OrdinalIgnoreCase);
if (apply == check)
    throw new ArgumentException("Tam olarak bir çalışma modu seçilmelidir: --check veya --apply.");

var configPath = Argument("--config") ?? "/opt/ECSProsAI/current/appsettings.Production.json";
var configuration = new ConfigurationBuilder()
    .AddJsonFile(configPath, optional: false)
    .AddEnvironmentVariables()
    .Build();
var connectionString = Required(configuration.GetConnectionString("DefaultConnection"),
    "ConnectionStrings:DefaultConnection");
var publicBase = (configuration["StorefrontMediaStorage:PublicBaseUrl"] ??
                  "https://cdn.misharitalia.com/storefront-v1").TrimEnd('/');
var sftpBase = (configuration["StorefrontMediaStorage:SftpBasePath"] ??
                "/var/www/html/storefront").TrimEnd('/');
var objectPrefix = (configuration["StorefrontMediaStorage:ObjectPrefix"] ?? "storefront").Trim('/');
var quality = (uint)Math.Clamp(configuration.GetValue("StorefrontMediaStorage:ImageQuality", 78), 1, 100);

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.EnableDynamicJson();
await using var dataSource = dataSourceBuilder.Build();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddDbContext<IamDbContext>(options => options.UseNpgsql(dataSource));
services.AddDataProtection()
    .PersistKeysToDbContext<IamDbContext>()
    .SetApplicationName("ECSPros")
    .DisableAutomaticKeyGeneration();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>()
    .CreateProtector("ECSPros.Catalog.Settings.Secrets.v1");
var settings = await LoadSettingsAsync(dataSource, configuration, protector);
var sources = await LoadSourcesAsync(dataSource, publicBase);
Console.WriteLine($"MODE={(apply ? "APPLY" : "CHECK")} SOURCES={sources.Count}");

var connectionInfo = new PasswordConnectionInfo(settings.SftpHost, settings.SftpPort,
    settings.SftpUsername, settings.SftpPassword) { Timeout = settings.Timeout };
using var sftp = new SftpClient(connectionInfo) { OperationTimeout = settings.Timeout };
sftp.Connect();
using var s3 = new AmazonS3Client(
    new BasicAWSCredentials(settings.S3AccessKey, settings.S3SecretKey),
    new AmazonS3Config
    {
        ServiceURL = settings.S3ServiceUrl,
        ForcePathStyle = settings.S3ForcePathStyle,
        Timeout = settings.Timeout,
        MaxErrorRetry = settings.MaxErrorRetry
    });
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

var missingBefore = 0;
var uploadedSftp = 0;
var uploadedS3 = 0;
var missingOriginalS3 = 0;
var uploadedOriginalS3 = 0;
foreach (var source in sources)
{
    var originalSftp = RemotePath(sftpBase, source.RelativeKey);
    var originalS3 = ObjectKey(objectPrefix, source.RelativeKey);
    if (!sftp.Exists(originalSftp))
        throw new InvalidOperationException($"SFTP orijinali bulunamadı: {source.RelativeKey}");
    byte[]? originalBytes = null;
    if (!await S3ExistsAsync(s3, settings.S3Bucket, originalS3))
    {
        missingOriginalS3++;
        Console.WriteLine($"EKSİK_ORIJINAL_S3 {source.RelativeKey}");
        if (apply)
        {
            originalBytes = await http.GetByteArrayAsync(source.Url);
            await using var originalInput = new MemoryStream(originalBytes, writable: false);
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = settings.S3Bucket,
                Key = originalS3,
                InputStream = originalInput,
                ContentType = ContentType(source.RelativeKey),
                CannedACL = S3CannedACL.PublicRead
            });
            uploadedOriginalS3++;
        }
    }
    foreach (var width in new[] { 480, 800, 1200, 1920 })
    {
        var variantKey = VariantKey(source.RelativeKey, width);
        var variantSftp = RemotePath(sftpBase, variantKey);
        var variantS3 = ObjectKey(objectPrefix, variantKey);
        var hasSftp = sftp.Exists(variantSftp);
        var hasS3 = await S3ExistsAsync(s3, settings.S3Bucket, variantS3);
        if (hasSftp && hasS3) continue;

        missingBefore++;
        Console.WriteLine($"EKSİK {variantKey} sftp={!hasSftp} s3={!hasS3}");
        if (!apply) continue;

        originalBytes ??= await http.GetByteArrayAsync(source.Url);
        var variantBytes = CreateVariant(originalBytes, width, quality);
        var wroteSftp = false;
        var wroteS3 = false;
        try
        {
            if (!hasSftp)
            {
                EnsureDirectory(sftp, variantSftp[..variantSftp.LastIndexOf('/')]);
                using var input = new MemoryStream(variantBytes, writable: false);
                sftp.UploadFile(input, variantSftp, canOverride: false);
                wroteSftp = true;
                uploadedSftp++;
            }
            if (!hasS3)
            {
                await using var input = new MemoryStream(variantBytes, writable: false);
                await s3.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = settings.S3Bucket,
                    Key = variantS3,
                    InputStream = input,
                    ContentType = "image/webp",
                    CannedACL = S3CannedACL.PublicRead
                });
                wroteS3 = true;
                uploadedS3++;
            }
        }
        catch
        {
            if (wroteSftp && !hasSftp && sftp.Exists(variantSftp)) sftp.DeleteFile(variantSftp);
            if (wroteS3 && !hasS3)
                await s3.DeleteObjectAsync(settings.S3Bucket, variantS3);
            throw;
        }
    }
}

if (apply)
{
    var missingAfter = await CountMissingAsync(sources, sftp, s3, settings.S3Bucket, sftpBase, objectPrefix);
    if (missingAfter != 0)
        throw new InvalidOperationException($"Backfill sonrası eksik hedef kaldı: {missingAfter}");
    var invalidPublic = await CountInvalidPublicAsync(sources, http);
    if (invalidPublic != 0)
        throw new InvalidOperationException($"Public CDN doğrulamasında geçersiz varyant kaldı: {invalidPublic}");
    Console.WriteLine($"APPLY_OK sources={sources.Count} missing_original_s3={missingOriginalS3} " +
                      $"uploaded_original_s3={uploadedOriginalS3} missing_variants={missingBefore} " +
                      $"uploaded_sftp={uploadedSftp} uploaded_s3={uploadedS3} missing_after=0 public_ok=168");
}
else
{
    var invalidPublic = missingOriginalS3 == 0 && missingBefore == 0
        ? await CountInvalidPublicAsync(sources, http)
        : -1;
    Console.WriteLine($"CHECK_OK missing_original_s3={missingOriginalS3} missing_variant_pairs={missingBefore} " +
                      $"public_invalid={invalidPublic}; yazma yapılmadı.");
}

sftp.Disconnect();

string? Argument(string name)
{
    var index = Array.FindIndex(args, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static async Task<List<SourceImage>> LoadSourcesAsync(NpgsqlDataSource source, string publicBase)
{
    const string sql = """
        SELECT DISTINCT url FROM (
            SELECT "ImageUrl" AS url FROM storefront.page_block_items
            WHERE NOT "IsDeleted" AND "ImageUrl" IS NOT NULL
            UNION
            SELECT "MobileImageUrl" AS url FROM storefront.page_block_items
            WHERE NOT "IsDeleted" AND "MobileImageUrl" IS NOT NULL
        ) x WHERE url LIKE $1 ORDER BY url
        """;
    var result = new List<SourceImage>();
    await using var connection = await source.OpenConnectionAsync();
    await using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue(publicBase + "/pages/%");
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var url = reader.GetString(0);
        var relative = Uri.UnescapeDataString(new Uri(url).AbsolutePath)
            .Replace(new Uri(publicBase).AbsolutePath.TrimEnd('/') + "/", string.Empty,
                StringComparison.OrdinalIgnoreCase);
        if (!relative.StartsWith("pages/", StringComparison.Ordinal) ||
            relative.Split('/').Any(part => part is "." or ".."))
            throw new InvalidOperationException($"Beklenmeyen CDN yolu: {url}");
        var extension = Path.GetExtension(relative);
        if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
            result.Add(new SourceImage(url, relative));
    }
    return result;
}

static async Task<BackfillSettings> LoadSettingsAsync(NpgsqlDataSource source,
    IConfiguration configuration, IDataProtector protector)
{
    string[] keys =
    [
        "ImageServer.SftpHost", "ImageServer.SftpPort", "ImageServer.SftpUser",
        "ImageServer.SftpPassword", "ImageServer.S3ServiceUrl", "ImageServer.S3Bucket",
        "ImageServer.S3AccessKey", "ImageServer.S3SecretKey", "ImageServer.S3ForcePathStyle"
    ];
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    await using var connection = await source.OpenConnectionAsync();
    await using var command = new NpgsqlCommand(
        "SELECT \"Key\", \"Value\" FROM definition.settings WHERE \"Key\" = ANY($1)", connection);
    command.Parameters.AddWithValue(keys);
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync()) values[reader.GetString(0)] = reader.GetString(1);

    string Get(string key, string fallback = "") =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : configuration[key switch
            {
                "ImageServer.SftpHost" => "CatalogImageStorage:Sftp:Host",
                "ImageServer.SftpPort" => "CatalogImageStorage:Sftp:Port",
                "ImageServer.SftpUser" => "CatalogImageStorage:Sftp:Username",
                "ImageServer.SftpPassword" => "CatalogImageStorage:Sftp:Password",
                "ImageServer.S3ServiceUrl" => "CatalogImageStorage:S3:ServiceUrl",
                "ImageServer.S3Bucket" => "CatalogImageStorage:S3:Bucket",
                "ImageServer.S3AccessKey" => "CatalogImageStorage:S3:AccessKey",
                "ImageServer.S3SecretKey" => "CatalogImageStorage:S3:SecretKey",
                "ImageServer.S3ForcePathStyle" => "CatalogImageStorage:S3:ForcePathStyle",
                _ => key
            }] ?? fallback;
    string Secret(string key)
    {
        var value = Get(key);
        return value.StartsWith("dp:v1:", StringComparison.Ordinal)
            ? protector.Unprotect(value["dp:v1:".Length..])
            : value;
    }
    var timeoutSeconds = Math.Clamp(configuration.GetValue("CatalogImageStorage:Sftp:TimeoutSeconds", 30), 5, 300);
    return new BackfillSettings(
        Required(Get("ImageServer.SftpHost"), "ImageServer.SftpHost"),
        Math.Clamp(int.TryParse(Get("ImageServer.SftpPort", "22"), out var port) ? port : 22, 1, 65535),
        Required(Get("ImageServer.SftpUser"), "ImageServer.SftpUser"),
        Required(Secret("ImageServer.SftpPassword"), "ImageServer.SftpPassword"),
        Required(Get("ImageServer.S3ServiceUrl", "https://s3.de.io.cloud.ovh.net/"),
            "ImageServer.S3ServiceUrl"),
        Required(Get("ImageServer.S3Bucket"), "ImageServer.S3Bucket"),
        Required(Secret("ImageServer.S3AccessKey"), "ImageServer.S3AccessKey"),
        Required(Secret("ImageServer.S3SecretKey"), "ImageServer.S3SecretKey"),
        bool.TryParse(Get("ImageServer.S3ForcePathStyle", "true"), out var forcePathStyle) && forcePathStyle,
        TimeSpan.FromSeconds(timeoutSeconds),
        Math.Clamp(configuration.GetValue("CatalogImageStorage:S3:MaxErrorRetry", 3), 0, 10));
}

static byte[] CreateVariant(byte[] original, int width, uint quality)
{
    using var image = new MagickImage(original);
    image.AutoOrient();
    image.Strip();
    image.Resize(new MagickGeometry((uint)width, 0));
    image.Format = MagickFormat.WebP;
    image.Quality = quality;
    return image.ToByteArray();
}

static async Task<bool> S3ExistsAsync(IAmazonS3 s3, string bucket, string key)
{
    try
    {
        await s3.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = bucket, Key = key });
        return true;
    }
    catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound ||
                                       ex.ErrorCode is "NoSuchKey" or "NotFound")
    {
        return false;
    }
}

static async Task<int> CountMissingAsync(IReadOnlyCollection<SourceImage> sources, SftpClient sftp,
    IAmazonS3 s3, string bucket, string sftpBase, string objectPrefix)
{
    var missing = 0;
    foreach (var source in sources)
    {
        if (!await S3ExistsAsync(s3, bucket, ObjectKey(objectPrefix, source.RelativeKey))) missing++;
        foreach (var width in new[] { 480, 800, 1200, 1920 })
        {
            var key = VariantKey(source.RelativeKey, width);
            if (!sftp.Exists(RemotePath(sftpBase, key)) ||
                !await S3ExistsAsync(s3, bucket, ObjectKey(objectPrefix, key))) missing++;
        }
    }
    return missing;
}

static async Task<int> CountInvalidPublicAsync(IReadOnlyCollection<SourceImage> sources, HttpClient http)
{
    var urls = sources.SelectMany(source => new[] { 480, 800, 1200, 1920 }
        .Select(width => $"{source.Url[..source.Url.LastIndexOf('/')]}/{Path.GetFileName(VariantKey(source.RelativeKey, width))}"));
    var invalid = 0;
    await Parallel.ForEachAsync(urls,
        new ParallelOptions { MaxDegreeOfParallelism = 8 },
        async (url, ct) =>
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode ||
                response.Content.Headers.ContentType?.MediaType != "image/webp")
                Interlocked.Increment(ref invalid);
        });
    return invalid;
}

static string VariantKey(string key, int width)
{
    var slash = key.LastIndexOf('/');
    var directory = key[..slash];
    var name = Path.GetFileNameWithoutExtension(key[(slash + 1)..]);
    return $"{directory}/{name}_w{width}.webp";
}

static string RemotePath(string basePath, string key) => $"{basePath}/{key}";
static string ObjectKey(string prefix, string key) => string.IsNullOrEmpty(prefix) ? key : $"{prefix}/{key}";
static string ContentType(string key) => Path.GetExtension(key).ToLowerInvariant() switch
{
    ".jpg" or ".jpeg" => "image/jpeg",
    ".png" => "image/png",
    ".webp" => "image/webp",
    _ => "application/octet-stream"
};

static void EnsureDirectory(SftpClient client, string path)
{
    var current = "/";
    foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
    {
        current = current == "/" ? $"/{segment}" : $"{current}/{segment}";
        if (!client.Exists(current)) client.CreateDirectory(current);
    }
}

static string Required(string? value, string key) => !string.IsNullOrWhiteSpace(value)
    ? value.Trim()
    : throw new InvalidOperationException($"{key} zorunludur.");

internal sealed record SourceImage(string Url, string RelativeKey);
internal sealed record BackfillSettings(
    string SftpHost,
    int SftpPort,
    string SftpUsername,
    string SftpPassword,
    string S3ServiceUrl,
    string S3Bucket,
    string S3AccessKey,
    string S3SecretKey,
    bool S3ForcePathStyle,
    TimeSpan Timeout,
    int MaxErrorRetry);
