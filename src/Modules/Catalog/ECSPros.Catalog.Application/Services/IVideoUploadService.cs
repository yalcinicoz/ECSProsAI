namespace ECSPros.Catalog.Application.Services;

public interface IVideoUploadService
{
    Task<bool> UploadAsync(Stream fileStream, string fileName, CancellationToken ct = default);
    Task<bool> DeleteAsync(string fileName, CancellationToken ct = default);
    string GetPublicUrl(string fileName);
}
