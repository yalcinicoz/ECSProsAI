using ECSPros.Catalog.Application.Services;

namespace ECSPros.Api.Services.Storage;

/// <summary>
/// Katalog modülünün mevcut upload sözleşmelerini ortak Local/S3 storage sağlayıcısına bağlar.
/// DB'de saklanan dosya adı değişmez; kategori yalnız fiziksel/object key ayrımıdır.
/// </summary>
public sealed class CatalogStorageUploadService(
    IFileStorage storage,
    ILogger<CatalogStorageUploadService> logger) : IImageUploadService, IVideoUploadService
{
    private const string ImageCategory = "catalog/images/products";
    private const string VideoCategory = "catalog/videos/products";

    string IImageUploadService.GetStoredFileExtension(string sourceExtension) =>
        sourceExtension.Trim().TrimStart('.').ToLowerInvariant();

    Task<bool> IImageUploadService.UploadAsync(
        Stream fileStream, string fileName, CancellationToken ct) =>
        UploadAsync(ImageCategory, fileStream, fileName, ContentType(fileName), ct);

    Task<bool> IVideoUploadService.UploadAsync(
        Stream fileStream, string fileName, CancellationToken ct) =>
        UploadAsync(VideoCategory, fileStream, fileName, ContentType(fileName), ct);

    Task<bool> IImageUploadService.DeleteAsync(string fileName, CancellationToken ct) =>
        DeleteAsync(ImageCategory, fileName, ct);

    Task<bool> IVideoUploadService.DeleteAsync(string fileName, CancellationToken ct) =>
        DeleteAsync(VideoCategory, fileName, ct);

    string IImageUploadService.GetPublicUrl(string fileName) =>
        storage.GetPublicUrl(Key(ImageCategory, fileName));

    string IVideoUploadService.GetPublicUrl(string fileName) =>
        storage.GetPublicUrl(Key(VideoCategory, fileName));

    private async Task<bool> UploadAsync(
        string category, Stream content, string fileName, string contentType, CancellationToken ct)
    {
        try
        {
            await storage.SavePublicAsync(category, SafeFileName(fileName), content, contentType, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Catalog storage upload başarısız: {Category}/{FileName}", category, fileName);
            return false;
        }
    }

    private async Task<bool> DeleteAsync(string category, string fileName, CancellationToken ct)
    {
        try
        {
            await storage.DeletePublicAsync(Key(category, fileName), ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Catalog storage delete başarısız: {Category}/{FileName}", category, fileName);
            return false;
        }
    }

    private static string Key(string category, string fileName) => $"{category}/{SafeFileName(fileName)}";

    private static string SafeFileName(string fileName) =>
        Path.GetFileName(fileName) == fileName && !string.IsNullOrWhiteSpace(fileName)
            ? fileName
            : throw new ArgumentException("Geçersiz catalog fileName.", nameof(fileName));

    private static string ContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        ".mov" => "video/quicktime",
        _ => "application/octet-stream"
    };
}
