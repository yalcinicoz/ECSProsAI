using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using ECSPros.Catalog.Application.Services;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Renci.SshNet;

namespace ECSPros.Api.Services.Storage;

public interface ICatalogImageSftpStore
{
    Task UploadAsync(CatalogImageStorageSettings settings, string fileName, byte[] content, CancellationToken ct);
    Task DeleteAsync(CatalogImageStorageSettings settings, string fileName, CancellationToken ct);
}

public interface ICatalogImageObjectStore
{
    Task UploadAsync(CatalogImageStorageSettings settings, string fileName, byte[] content, CancellationToken ct);
    Task DeleteAsync(CatalogImageStorageSettings settings, string fileName, CancellationToken ct);
}

public interface ICatalogImageStorageSettingsProvider
{
    Task<CatalogImageStorageSettings> GetAsync(CancellationToken ct);
}

public sealed record CatalogImageStorageSettings(
    uint ImageQuality,
    string SftpHost,
    int SftpPort,
    string SftpUsername,
    string SftpPassword,
    string SftpBasePath,
    string S3ServiceUrl,
    string S3Bucket,
    string S3AccessKey,
    string S3SecretKey,
    bool S3ForcePathStyle,
    TimeSpan Timeout,
    int MaxErrorRetry);

public sealed class CatalogImageStorageSettingsProvider(
    ICatalogDbContext db,
    ICatalogSettingSecretProtector secretProtector,
    IConfiguration configuration) : ICatalogImageStorageSettingsProvider
{
    private static readonly string[] Keys =
    [
        "ImageServer.UploadQuality",
        "ImageServer.SftpHost", "ImageServer.SftpPort", "ImageServer.SftpUser",
        "ImageServer.SftpPassword", "ImageServer.SftpBasePath",
        "ImageServer.S3ServiceUrl", "ImageServer.S3Bucket", "ImageServer.S3AccessKey",
        "ImageServer.S3SecretKey", "ImageServer.S3ForcePathStyle"
    ];
    private static readonly IReadOnlyDictionary<string, string> ConfigurationKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ImageServer.UploadQuality"] = "CatalogImageStorage:ImageQuality",
            ["ImageServer.SftpHost"] = "CatalogImageStorage:Sftp:Host",
            ["ImageServer.SftpPort"] = "CatalogImageStorage:Sftp:Port",
            ["ImageServer.SftpUser"] = "CatalogImageStorage:Sftp:Username",
            ["ImageServer.SftpPassword"] = "CatalogImageStorage:Sftp:Password",
            ["ImageServer.SftpBasePath"] = "CatalogImageStorage:Sftp:BasePath",
            ["ImageServer.S3ServiceUrl"] = "CatalogImageStorage:S3:ServiceUrl",
            ["ImageServer.S3Bucket"] = "CatalogImageStorage:S3:Bucket",
            ["ImageServer.S3AccessKey"] = "CatalogImageStorage:S3:AccessKey",
            ["ImageServer.S3SecretKey"] = "CatalogImageStorage:S3:SecretKey",
            ["ImageServer.S3ForcePathStyle"] = "CatalogImageStorage:S3:ForcePathStyle"
        };

    public async Task<CatalogImageStorageSettings> GetAsync(CancellationToken ct)
    {
        var values = await db.CatalogSettings
            .Where(x => Keys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase, ct);

        string Get(string key, string fallback = "") =>
            values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : configuration[ConfigurationKeys[key]] ?? fallback;

        string Secret(string key) => secretProtector.Unprotect(Get(key));
        var timeoutSeconds = Math.Clamp(configuration.GetValue("CatalogImageStorage:Sftp:TimeoutSeconds", 30), 5, 300);

        return new CatalogImageStorageSettings(
            ImageQuality: (uint)Math.Clamp(ParseInt(Get("ImageServer.UploadQuality", "80"), 80), 1, 100),
            SftpHost: Required(Get("ImageServer.SftpHost"), "ImageServer.SftpHost"),
            SftpPort: Math.Clamp(ParseInt(Get("ImageServer.SftpPort", "22"), 22), 1, 65535),
            SftpUsername: Required(Get("ImageServer.SftpUser"), "ImageServer.SftpUser"),
            SftpPassword: Required(Secret("ImageServer.SftpPassword"), "ImageServer.SftpPassword"),
            SftpBasePath: Get("ImageServer.SftpBasePath", "/var/www/html/images").TrimEnd('/'),
            S3ServiceUrl: Required(Get("ImageServer.S3ServiceUrl", "https://s3.de.io.cloud.ovh.net/"), "ImageServer.S3ServiceUrl"),
            S3Bucket: Required(Get("ImageServer.S3Bucket"), "ImageServer.S3Bucket"),
            S3AccessKey: Required(Secret("ImageServer.S3AccessKey"), "ImageServer.S3AccessKey"),
            S3SecretKey: Required(Secret("ImageServer.S3SecretKey"), "ImageServer.S3SecretKey"),
            S3ForcePathStyle: bool.TryParse(Get("ImageServer.S3ForcePathStyle", "true"), out var pathStyle) && pathStyle,
            Timeout: TimeSpan.FromSeconds(timeoutSeconds),
            MaxErrorRetry: Math.Clamp(configuration.GetValue("CatalogImageStorage:S3:MaxErrorRetry", 3), 0, 10));
    }

    private static int ParseInt(string value, int fallback) => int.TryParse(value, out var parsed) ? parsed : fallback;

    private static string Required(string? value, string key) =>
        !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidOperationException($"{key} zorunludur.");
}

/// <summary>
/// Legacy görsel origin sözleşmesini korur: WebP SFTP origin'e, aynı basename'in JPEG
/// karşılığı OVH S3-compatible bucket'a yazılır. İki hedef birlikte başarılı değilse
/// başarılı olan taraf telafi amacıyla silinir.
/// </summary>
public sealed class DualTargetCatalogImageUploadService(
    ICatalogImageSftpStore sftp,
    ICatalogImageObjectStore objectStore,
    ICatalogImageStorageSettingsProvider settingsProvider,
    IConfiguration configuration,
    ILogger<DualTargetCatalogImageUploadService> logger) : IImageUploadService
{
    private readonly string _publicBaseUrl =
        (configuration["CatalogImageStorage:PublicBaseUrl"] ?? "https://cdn.misharitalia.com/img/1200/85")
        .TrimEnd('/');

    public string GetStoredFileExtension(string sourceExtension) => "webp";

    public async Task<bool> UploadAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var webpName = WithExtension(fileName, ".webp");
        var jpegName = WithExtension(fileName, ".jpg");

        try
        {
            var settings = await settingsProvider.GetAsync(ct);
            await using var input = new MemoryStream();
            await fileStream.CopyToAsync(input, ct);
            var (webp, jpeg) = ConvertImage(input.ToArray(), settings.ImageQuality);

            var sftpUploaded = false;
            var objectUploaded = false;
            try
            {
                var sftpTask = UploadSftpAsync();
                var objectTask = UploadObjectAsync();
                await Task.WhenAll(sftpTask, objectTask);
                return true;

                async Task UploadSftpAsync()
                {
                    await sftp.UploadAsync(settings, webpName, webp, ct);
                    sftpUploaded = true;
                }

                async Task UploadObjectAsync()
                {
                    await objectStore.UploadAsync(settings, jpegName, jpeg, ct);
                    objectUploaded = true;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Catalog image çift-hedef upload başarısız: {FileName}; SFTP={SftpUploaded}, Object={ObjectUploaded}",
                    webpName, sftpUploaded, objectUploaded);
                await CompensateAsync(settings, webpName, jpegName, sftpUploaded, objectUploaded);
                return false;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Catalog image dönüştürme başarısız: {FileName}", webpName);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(string fileName, CancellationToken ct = default)
    {
        var webpName = WithExtension(fileName, ".webp");
        var jpegName = WithExtension(fileName, ".jpg");
        try
        {
            var settings = await settingsProvider.GetAsync(ct);
            await Task.WhenAll(
                sftp.DeleteAsync(settings, webpName, ct),
                objectStore.DeleteAsync(settings, jpegName, ct));
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Catalog image çift-hedef silme tamamlanamadı: {FileName}", webpName);
            return false;
        }
    }

    public string GetPublicUrl(string fileName) => $"{_publicBaseUrl}/{WithExtension(fileName, ".webp")}";

    private async Task CompensateAsync(
        CatalogImageStorageSettings settings,
        string webpName, string jpegName, bool sftpUploaded, bool objectUploaded)
    {
        try
        {
            var tasks = new List<Task>(2);
            if (sftpUploaded) tasks.Add(sftp.DeleteAsync(settings, webpName, CancellationToken.None));
            if (objectUploaded) tasks.Add(objectStore.DeleteAsync(settings, jpegName, CancellationToken.None));
            await Task.WhenAll(tasks);
        }
        catch (Exception cleanupEx)
        {
            logger.LogWarning(cleanupEx, "Başarısız catalog upload telafi temizliği tamamlanamadı: {FileName}", webpName);
        }
    }

    private static (byte[] Webp, byte[] Jpeg) ConvertImage(byte[] source, uint quality)
    {
        using var original = new MagickImage(source);
        original.AutoOrient();
        original.Strip();

        using var webp = (MagickImage)original.Clone();
        webp.Format = MagickFormat.WebP;
        webp.Quality = quality;

        using var jpeg = (MagickImage)original.Clone();
        jpeg.BackgroundColor = MagickColors.White;
        jpeg.Alpha(AlphaOption.Remove);
        jpeg.Format = MagickFormat.Jpeg;
        jpeg.Quality = quality;

        return (webp.ToByteArray(), jpeg.ToByteArray());
    }

    private static string WithExtension(string fileName, string extension)
    {
        if (Path.GetFileName(fileName) != fileName || string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Geçersiz catalog fileName.", nameof(fileName));
        return Path.ChangeExtension(fileName, extension);
    }
}

public sealed class CatalogImageSftpStore : ICatalogImageSftpStore
{
    private readonly SemaphoreSlim _gate;

    public CatalogImageSftpStore(IConfiguration configuration)
    {
        _gate = new SemaphoreSlim(Math.Clamp(
            configuration.GetValue("CatalogImageStorage:Sftp:MaxConcurrency", 3), 1, 16));
    }

    public Task UploadAsync(
        CatalogImageStorageSettings settings, string fileName, byte[] content, CancellationToken ct) =>
        ExecuteAsync(client =>
        {
            using var input = new MemoryStream(content, writable: false);
            client.UploadFile(input, RemotePath(settings, fileName), canOverride: true);
        }, settings, ct);

    public Task DeleteAsync(CatalogImageStorageSettings settings, string fileName, CancellationToken ct) =>
        ExecuteAsync(client =>
        {
            var path = RemotePath(settings, fileName);
            if (client.Exists(path)) client.DeleteFile(path);
        }, settings, ct);

    private async Task ExecuteAsync(
        Action<SftpClient> action, CatalogImageStorageSettings settings, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var connection = new PasswordConnectionInfo(
                    settings.SftpHost, settings.SftpPort, settings.SftpUsername, settings.SftpPassword)
                {
                    Timeout = settings.Timeout
                };
                using var client = new SftpClient(connection) { OperationTimeout = settings.Timeout };
                client.Connect();
                action(client);
                client.Disconnect();
            }, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string RemotePath(CatalogImageStorageSettings settings, string fileName)
    {
        if (Path.GetFileName(fileName) != fileName || string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Geçersiz catalog fileName.", nameof(fileName));
        return $"{settings.SftpBasePath}/{fileName}";
    }
}

public sealed class CatalogImageObjectStore : ICatalogImageObjectStore
{
    public async Task UploadAsync(
        CatalogImageStorageSettings settings, string fileName, byte[] content, CancellationToken ct)
    {
        using var client = CreateClient(settings);
        await using var input = new MemoryStream(content, writable: false);
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = settings.S3Bucket,
            Key = SafeFileName(fileName),
            InputStream = input,
            ContentType = "image/jpeg",
            CannedACL = S3CannedACL.PublicRead
        }, ct);
    }

    public async Task DeleteAsync(CatalogImageStorageSettings settings, string fileName, CancellationToken ct)
    {
        using var client = CreateClient(settings);
        await client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = settings.S3Bucket,
            Key = SafeFileName(fileName)
        }, ct);
    }

    private static IAmazonS3 CreateClient(CatalogImageStorageSettings settings)
    {
        if (!Uri.TryCreate(settings.S3ServiceUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("ImageServer.S3ServiceUrl HTTPS olmalıdır.");

        var config = new AmazonS3Config
        {
            ServiceURL = settings.S3ServiceUrl,
            ForcePathStyle = settings.S3ForcePathStyle,
            Timeout = settings.Timeout,
            MaxErrorRetry = settings.MaxErrorRetry
        };
        return new AmazonS3Client(
            new BasicAWSCredentials(settings.S3AccessKey, settings.S3SecretKey), config);
    }

    private static string SafeFileName(string fileName) =>
        Path.GetFileName(fileName) == fileName && !string.IsNullOrWhiteSpace(fileName)
            ? fileName
            : throw new ArgumentException("Geçersiz catalog fileName.", nameof(fileName));
}
