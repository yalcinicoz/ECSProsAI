using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using ImageMagick;
using Renci.SshNet;

namespace ECSPros.Api.Services.Storage;

public interface IStorefrontMediaUploadService
{
    Task<StoredFile> UploadAsync(
        string mediaKind, string fileName, Stream content, string contentType,
        CancellationToken ct = default);
}

public interface IStorefrontMediaSftpStore
{
    Task UploadAsync(CatalogImageStorageSettings settings, string basePath, string relativeKey,
        byte[] content, CancellationToken ct);
    Task DeleteAsync(CatalogImageStorageSettings settings, string basePath, string relativeKey,
        CancellationToken ct);
}

public interface IStorefrontMediaObjectStore
{
    Task UploadAsync(CatalogImageStorageSettings settings, string key, byte[] content,
        string contentType, CancellationToken ct);
    Task DeleteAsync(CatalogImageStorageSettings settings, string key, CancellationToken ct);
}

/// <summary>
/// Vitrin medyasını ürün görsellerinin /images kökünden ayrı tutar. Etkin olduğunda
/// dosyalar CDN origin ve object storage'a aynı klasör ağacıyla çift yazılır; yerel
/// geliştirmede mevcut IFileStorage davranışı geriye uyumlu fallback olarak korunur.
/// </summary>
public sealed class StorefrontMediaUploadService(
    IFileStorage fallbackStorage,
    IStorefrontMediaSftpStore sftp,
    IStorefrontMediaObjectStore objectStore,
    ICatalogImageStorageSettingsProvider settingsProvider,
    IConfiguration configuration,
    ILogger<StorefrontMediaUploadService> logger) : IStorefrontMediaUploadService
{
    private static readonly HashSet<string> MediaKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "desktop", "mobile", "menu"
    };

    private readonly bool _enabled = configuration.GetValue("StorefrontMediaStorage:Enabled", false);
    private readonly string _publicBaseUrl =
        (configuration["StorefrontMediaStorage:PublicBaseUrl"] ??
         "https://cdn.misharitalia.com/storefront-v1").TrimEnd('/');
    private readonly string _sftpBasePath =
        (configuration["StorefrontMediaStorage:SftpBasePath"] ??
         "/var/www/html/storefront").TrimEnd('/');
    private readonly string _objectPrefix =
        (configuration["StorefrontMediaStorage:ObjectPrefix"] ?? "storefront").Trim('/');

    public async Task<StoredFile> UploadAsync(
        string mediaKind, string fileName, Stream content, string contentType,
        CancellationToken ct = default)
    {
        var safeKind = NormalizeMediaKind(mediaKind);
        var relativeDirectory = safeKind == "menu"
            ? $"menus/{DateTime.UtcNow:yyyy/MM}"
            : $"pages/{safeKind}/{DateTime.UtcNow:yyyy/MM}";

        if (!_enabled)
        {
            var stored = await fallbackStorage.SavePublicAsync(
                $"storefront/{relativeDirectory}", fileName, content, contentType, ct);
            if (stored.PhysicalPath is { } path &&
                Store.VitrinGorselVaryantlari.Desteklenir(contentType))
            {
                try
                {
                    await Store.VitrinGorselVaryantlari.UretAsync(path, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex,
                        "Yerel vitrin görsel varyantları üretilemedi: {FileName}", fileName);
                }
            }
            return stored;
        }

        var settings = await settingsProvider.GetAsync(ct);
        EnsureSeparateOrigin(settings.SftpBasePath);

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var relativeKey = $"{relativeDirectory}/{SafeFileName(fileName)}";
        var artifacts = CreateArtifacts(relativeKey, buffer.ToArray(), contentType);
        var uploadedSftp = new List<string>();
        var uploadedObjects = new List<string>();

        try
        {
            foreach (var artifact in artifacts)
            {
                var objectKey = ObjectKey(artifact.RelativeKey);
                Task? sftpTask = null;
                Task? objectTask = null;
                try
                {
                    sftpTask = sftp.UploadAsync(
                        settings, _sftpBasePath, artifact.RelativeKey, artifact.Content, ct);
                    objectTask = objectStore.UploadAsync(
                        settings, objectKey, artifact.Content, artifact.ContentType, ct);
                    await Task.WhenAll(sftpTask, objectTask);
                    uploadedSftp.Add(artifact.RelativeKey);
                    uploadedObjects.Add(objectKey);
                }
                catch
                {
                    if (sftpTask?.IsCompletedSuccessfully == true) uploadedSftp.Add(artifact.RelativeKey);
                    if (objectTask?.IsCompletedSuccessfully == true) uploadedObjects.Add(objectKey);
                    throw;
                }
            }
        }
        catch
        {
            await CompensateAsync(settings, uploadedSftp, uploadedObjects);
            throw;
        }

        return new StoredFile(
            ObjectKey(relativeKey), $"{_publicBaseUrl}/{relativeKey}", PhysicalPath: null);
    }

    private IReadOnlyList<MediaArtifact> CreateArtifacts(
        string originalKey, byte[] source, string contentType)
    {
        var artifacts = new List<MediaArtifact> { new(originalKey, source, contentType) };
        if (!Store.VitrinGorselVaryantlari.Desteklenir(contentType)) return artifacts;

        using var original = new MagickImage(source);
        original.AutoOrient();
        original.Strip();
        var quality = (uint)Math.Clamp(
            configuration.GetValue("StorefrontMediaStorage:ImageQuality", 78), 1, 100);
        foreach (var width in Store.VitrinGorselVaryantlari.Genislikler)
        {
            using var variant = (MagickImage)original.Clone();
            variant.Resize(new MagickGeometry((uint)width, 0));
            variant.Format = MagickFormat.WebP;
            variant.Quality = quality;
            var fileName = Store.VitrinGorselVaryantlari.VaryantDosyaAdi(originalKey, width);
            var directory = originalKey[..originalKey.LastIndexOf('/')];
            artifacts.Add(new MediaArtifact(
                $"{directory}/{fileName}", variant.ToByteArray(), "image/webp"));
        }
        return artifacts;
    }

    private async Task CompensateAsync(
        CatalogImageStorageSettings settings,
        IReadOnlyCollection<string> sftpKeys,
        IReadOnlyCollection<string> objectKeys)
    {
        try
        {
            await Task.WhenAll(
                sftpKeys.Select(key => sftp.DeleteAsync(
                    settings, _sftpBasePath, key, CancellationToken.None))
                .Concat(objectKeys.Select(key => objectStore.DeleteAsync(
                    settings, key, CancellationToken.None))));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Başarısız vitrin medya yüklemesinin telafi temizliği tamamlanamadı.");
        }
    }

    private void EnsureSeparateOrigin(string productBasePath)
    {
        var storefront = _sftpBasePath.TrimEnd('/');
        var products = productBasePath.TrimEnd('/');
        if (storefront.Equals(products, StringComparison.OrdinalIgnoreCase) ||
            storefront.StartsWith(products + "/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "StorefrontMediaStorage:SftpBasePath ürün görsel kökünden ayrı olmalıdır.");
    }

    private string ObjectKey(string relativeKey) =>
        string.IsNullOrEmpty(_objectPrefix) ? relativeKey : $"{_objectPrefix}/{relativeKey}";

    private static string NormalizeMediaKind(string mediaKind) =>
        MediaKinds.Contains(mediaKind) ? mediaKind.ToLowerInvariant() :
            throw new ArgumentException("Vitrin medya türü desktop, mobile veya menu olmalıdır.", nameof(mediaKind));

    private static string SafeFileName(string fileName) =>
        Path.GetFileName(fileName) == fileName && !string.IsNullOrWhiteSpace(fileName)
            ? fileName
            : throw new ArgumentException("Geçersiz vitrin medya dosya adı.", nameof(fileName));

    private sealed record MediaArtifact(string RelativeKey, byte[] Content, string ContentType);
}

public sealed class StorefrontMediaSftpStore(IConfiguration configuration) : IStorefrontMediaSftpStore
{
    private readonly SemaphoreSlim _gate = new(Math.Clamp(
        configuration.GetValue("StorefrontMediaStorage:SftpMaxConcurrency", 2), 1, 8));

    public Task UploadAsync(CatalogImageStorageSettings settings, string basePath, string relativeKey,
        byte[] content, CancellationToken ct) => ExecuteAsync(client =>
        {
            var path = RemotePath(basePath, relativeKey);
            EnsureDirectory(client, path[..path.LastIndexOf('/')]);
            using var input = new MemoryStream(content, writable: false);
            client.UploadFile(input, path, canOverride: true);
        }, settings, ct);

    public Task DeleteAsync(CatalogImageStorageSettings settings, string basePath, string relativeKey,
        CancellationToken ct) => ExecuteAsync(client =>
        {
            var path = RemotePath(basePath, relativeKey);
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
                    settings.SftpHost, settings.SftpPort,
                    settings.SftpUsername, settings.SftpPassword) { Timeout = settings.Timeout };
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

    private static void EnsureDirectory(SftpClient client, string path)
    {
        var current = path.StartsWith('/') ? "/" : string.Empty;
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = current == "/" ? $"/{segment}" : $"{current}/{segment}";
            if (client.Exists(current)) continue;
            try
            {
                client.CreateDirectory(current);
            }
            catch
            {
                // Başka API düğümü aynı klasörü arada oluşturmuş olabilir.
                if (!client.Exists(current)) throw;
            }
        }
    }

    private static string RemotePath(string basePath, string relativeKey) =>
        $"{NormalizeBasePath(basePath)}/{NormalizeRelativeKey(relativeKey)}";

    private static string NormalizeBasePath(string path) =>
        !string.IsNullOrWhiteSpace(path) && path.StartsWith('/') &&
        !path.Split('/').Any(part => part is "." or "..")
            ? path.TrimEnd('/')
            : throw new ArgumentException("Geçersiz storefront SFTP kökü.", nameof(path));

    private static string NormalizeRelativeKey(string key)
    {
        var normalized = key.Replace('\\', '/').Trim('/');
        if (normalized.Length == 0 || normalized.Split('/').Any(part => part is "." or ".."))
            throw new ArgumentException("Geçersiz storefront medya anahtarı.", nameof(key));
        return normalized;
    }
}

public sealed class StorefrontMediaObjectStore : IStorefrontMediaObjectStore
{
    public async Task UploadAsync(CatalogImageStorageSettings settings, string key, byte[] content,
        string contentType, CancellationToken ct)
    {
        using var client = CreateClient(settings);
        await using var input = new MemoryStream(content, writable: false);
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = settings.S3Bucket,
            Key = NormalizeKey(key),
            InputStream = input,
            ContentType = contentType,
            CannedACL = S3CannedACL.PublicRead
        }, ct);
    }

    public async Task DeleteAsync(CatalogImageStorageSettings settings, string key, CancellationToken ct)
    {
        using var client = CreateClient(settings);
        await client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = settings.S3Bucket,
            Key = NormalizeKey(key)
        }, ct);
    }

    private static IAmazonS3 CreateClient(CatalogImageStorageSettings settings)
    {
        if (!Uri.TryCreate(settings.S3ServiceUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("ImageServer.S3ServiceUrl HTTPS olmalıdır.");
        return new AmazonS3Client(
            new BasicAWSCredentials(settings.S3AccessKey, settings.S3SecretKey),
            new AmazonS3Config
            {
                ServiceURL = settings.S3ServiceUrl,
                ForcePathStyle = settings.S3ForcePathStyle,
                Timeout = settings.Timeout,
                MaxErrorRetry = settings.MaxErrorRetry
            });
    }

    private static string NormalizeKey(string key)
    {
        var normalized = key.Replace('\\', '/').Trim('/');
        if (normalized.Length == 0 || normalized.Split('/').Any(part => part is "." or ".."))
            throw new ArgumentException("Geçersiz storefront object key.", nameof(key));
        return normalized;
    }
}
