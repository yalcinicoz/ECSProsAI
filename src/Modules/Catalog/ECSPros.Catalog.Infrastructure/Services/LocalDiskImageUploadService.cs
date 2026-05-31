using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ECSPros.Catalog.Infrastructure.Services;

/// <summary>
/// Dosyaları yerel diske kaydeder. Kayıt yolu CatalogSettings'deki
/// ImageServer.LocalSavePath anahtarından okunur (varsayılan: publish/uploads/images/products/).
/// Genel URL ImageServer.PublicBaseUrl anahtarından üretilir.
/// </summary>
public class LocalDiskImageUploadService : IImageUploadService
{
    private readonly CatalogDbContext _db;
    private readonly ILogger<LocalDiskImageUploadService> _logger;

    // Fallback — DB ayarı yoksa
    private static readonly string FallbackSavePath =
        Path.Combine(AppContext.BaseDirectory, "uploads", "images", "products");

    public LocalDiskImageUploadService(CatalogDbContext db, ILogger<LocalDiskImageUploadService> logger)
    {
        _db = db;
        _logger = logger;
    }

    private async Task<(string savePath, string publicBaseUrl)> LoadSettingsAsync(CancellationToken ct)
    {
        var settings = await _db.CatalogSettings
            .Where(x => x.Key == "ImageServer.LocalSavePath" || x.Key == "ImageServer.PublicBaseUrl")
            .ToListAsync(ct);

        string Get(string key, string def) =>
            settings.FirstOrDefault(x => x.Key == key)?.Value ?? def;

        var savePath = Get("ImageServer.LocalSavePath", "").Trim();
        if (string.IsNullOrEmpty(savePath))
            savePath = FallbackSavePath;

        var publicBaseUrl = Get("ImageServer.PublicBaseUrl", "").TrimEnd('/');

        return (savePath, publicBaseUrl);
    }

    public async Task<bool> UploadAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        try
        {
            var (savePath, _) = await LoadSettingsAsync(ct);
            Directory.CreateDirectory(savePath);
            var filePath = Path.Combine(savePath, fileName);
            await using var fs = File.Create(filePath);
            await fileStream.CopyToAsync(fs, ct);
            _logger.LogInformation("Local disk upload OK: {FileName} → {FilePath}", fileName, filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local disk upload failed: {FileName}", fileName);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(string fileName, CancellationToken ct = default)
    {
        try
        {
            var (savePath, _) = await LoadSettingsAsync(ct);
            var filePath = Path.Combine(savePath, fileName);
            if (File.Exists(filePath))
                File.Delete(filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local disk delete failed: {FileName}", fileName);
            return false;
        }
    }

    public string GetPublicUrl(string fileName)
    {
        var publicBaseUrl = _db.CatalogSettings
            .Where(x => x.Key == "ImageServer.PublicBaseUrl")
            .Select(x => x.Value)
            .FirstOrDefault() ?? "";

        if (string.IsNullOrWhiteSpace(publicBaseUrl))
            return $"/api/catalog/images/file/{fileName}";

        return publicBaseUrl.TrimEnd('/') + "/" + fileName;
    }
}
