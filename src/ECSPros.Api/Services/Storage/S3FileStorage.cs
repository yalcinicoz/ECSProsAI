using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace ECSPros.Api.Services.Storage;

/// <summary>AWS S3 ve path-style uyumlu MinIO/OVH Object Storage sağlayıcısı.</summary>
public sealed class S3FileStorage : IFileStorage, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;
    private readonly string _publicBaseUrl;

    public S3FileStorage(IConfiguration configuration)
    {
        var section = configuration.GetSection("Storage:S3");
        var endpoint = Required(section["ServiceUrl"], "Storage:S3:ServiceUrl");
        _bucket = Required(section["Bucket"], "Storage:S3:Bucket");
        var accessKey = Required(section["AccessKey"], "Storage:S3:AccessKey");
        var secretKey = Required(section["SecretKey"], "Storage:S3:SecretKey");
        _publicBaseUrl = Required(configuration["Storage:PublicBaseUrl"], "Storage:PublicBaseUrl")
            .TrimEnd('/');

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) ||
            endpointUri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("Storage:S3:ServiceUrl geçerli bir HTTP(S) URL olmalıdır.");
        if (!endpointUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) &&
            !section.GetValue("AllowHttp", false))
            throw new InvalidOperationException("Storage:S3 HTTP endpoint için AllowHttp=true açıkça verilmelidir.");

        var s3Config = new AmazonS3Config
        {
            ServiceURL = endpointUri.ToString().TrimEnd('/'),
            ForcePathStyle = section.GetValue("ForcePathStyle", true),
            AuthenticationRegion = section["Region"] ?? "us-east-1",
            Timeout = TimeSpan.FromSeconds(section.GetValue("TimeoutSeconds", 30)),
            MaxErrorRetry = section.GetValue("MaxErrorRetry", 3)
        };
        _client = new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), s3Config);
    }

    public async Task<StoredFile> SavePublicAsync(
        string category, string fileName, Stream content, string contentType,
        CancellationToken ct = default)
    {
        var key = SafeKey(category, fileName);
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        }, ct);
        return new StoredFile(key, GetPublicUrl(key), null);
    }

    public Task<string> GetPrivateReadUrlAsync(
        string key, TimeSpan lifetime, CancellationToken ct = default)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromDays(7))
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Signed URL ömrü 0-7 gün aralığında olmalıdır.");
        var safeKey = SafeKey(Path.GetDirectoryName(key)?.Replace('\\', '/') ?? string.Empty, Path.GetFileName(key));
        var url = _client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = safeKey,
            Expires = DateTime.UtcNow.Add(lifetime),
            Verb = HttpVerb.GET
        });
        return Task.FromResult(url);
    }

    public async Task DeletePublicAsync(string key, CancellationToken ct = default)
    {
        var safeKey = NormalizeKey(key);
        await _client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _bucket,
            Key = safeKey
        }, ct);
    }

    public string GetPublicUrl(string key) => $"{_publicBaseUrl}/{NormalizeKey(key)}";

    public void Dispose() => _client.Dispose();

    private static string SafeKey(string category, string fileName)
    {
        var safeCategory = category.Replace('\\', '/').Trim('/');
        if (safeCategory.Length == 0 || safeCategory.Split('/').Any(p => p is "." or ".."))
            throw new ArgumentException("Geçersiz storage category.", nameof(category));
        if (Path.GetFileName(fileName) != fileName || string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Geçersiz storage fileName.", nameof(fileName));
        return $"{safeCategory}/{fileName}";
    }

    private static string NormalizeKey(string key)
    {
        var normalized = key.Replace('\\', '/').Trim('/');
        if (normalized.Length == 0 || normalized.Split('/').Any(p => p is "." or ".."))
            throw new ArgumentException("Geçersiz storage key.", nameof(key));
        return normalized;
    }

    private static string Required(string? value, string key) =>
        !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidOperationException($"{key} zorunludur.");
}
