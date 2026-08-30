namespace ECSPros.Api.Services.Storage;

public sealed record StoredFile(string Key, string PublicUrl, string? PhysicalPath);

/// <summary>Node bağımsız dosya sağlayıcılarının ortak yazma sözleşmesi.</summary>
public interface IFileStorage
{
    Task<StoredFile> SavePublicAsync(
        string category, string fileName, Stream content, string contentType,
        CancellationToken ct = default);

    Task<string> GetPrivateReadUrlAsync(
        string key, TimeSpan lifetime, CancellationToken ct = default);

    Task DeletePublicAsync(string key, CancellationToken ct = default);

    string GetPublicUrl(string key);
}
