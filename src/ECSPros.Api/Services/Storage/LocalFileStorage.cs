namespace ECSPros.Api.Services.Storage;

/// <summary>Development/geçiş sağlayıcısı; temp dosya + atomik move ile yazar.</summary>
public sealed class LocalFileStorage(IConfiguration configuration) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(
        configuration["Storage:Local:RootPath"]
        ?? configuration["Store:MediaRootPath"]
        ?? "/opt/ECSProsAI/media");
    private readonly string _publicBaseUrl =
        (configuration["Storage:PublicBaseUrl"] ?? "/media").TrimEnd('/');

    public async Task<StoredFile> SavePublicAsync(
        string category, string fileName, Stream content, string contentType,
        CancellationToken ct = default)
    {
        var safeCategory = category.Replace('\\', '/').Trim('/');
        if (safeCategory.Length == 0 || safeCategory.Split('/').Any(p => p is "." or ".."))
            throw new ArgumentException("Geçersiz storage category.", nameof(category));
        if (Path.GetFileName(fileName) != fileName)
            throw new ArgumentException("Geçersiz storage fileName.", nameof(fileName));

        var directory = Path.GetFullPath(Path.Combine(
            _root, safeCategory.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = _root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!directory.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Storage hedefi izin verilen kökün dışında.");

        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, fileName);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var output = new FileStream(
                temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
                await content.CopyToAsync(output, ct);
            File.Move(temporary, target, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }

        var key = $"{safeCategory}/{fileName}";
        return new StoredFile(key, GetPublicUrl(key), target);
    }

    public Task<string> GetPrivateReadUrlAsync(
        string key, TimeSpan lifetime, CancellationToken ct = default) =>
        throw new NotSupportedException(
            "Local storage private signed URL desteklemez; production için S3 provider kullanın.");

    public Task DeletePublicAsync(string key, CancellationToken ct = default)
    {
        var path = ResolveKey(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public string GetPublicUrl(string key)
    {
        _ = ResolveKey(key);
        return $"{_publicBaseUrl}/{key.Replace('\\', '/')}";
    }

    private string ResolveKey(string key)
    {
        var normalized = key.Replace('\\', '/').Trim('/');
        if (normalized.Length == 0 || normalized.Split('/').Any(p => p is "." or ".."))
            throw new ArgumentException("Geçersiz storage key.", nameof(key));
        var path = Path.GetFullPath(Path.Combine(
            _root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = _root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Storage hedefi izin verilen kökün dışında.");
        return path;
    }
}
