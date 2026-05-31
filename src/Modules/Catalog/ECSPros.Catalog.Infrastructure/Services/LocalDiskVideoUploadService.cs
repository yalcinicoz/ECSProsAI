using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ECSPros.Catalog.Infrastructure.Services;

public class LocalDiskVideoUploadService : IVideoUploadService
{
    private readonly CatalogDbContext _db;
    private readonly ILogger<LocalDiskVideoUploadService> _logger;

    private static readonly string FallbackSavePath =
        Path.Combine(AppContext.BaseDirectory, "uploads", "videos", "products");

    public LocalDiskVideoUploadService(CatalogDbContext db, ILogger<LocalDiskVideoUploadService> logger)
    {
        _db = db;
        _logger = logger;
    }

    private async Task<(string savePath, string publicBaseUrl)> LoadSettingsAsync(CancellationToken ct)
    {
        var settings = await _db.CatalogSettings
            .Where(x => x.Key == "VideoServer.LocalSavePath" || x.Key == "VideoServer.PublicBaseUrl")
            .ToListAsync(ct);

        string Get(string key, string def) =>
            settings.FirstOrDefault(x => x.Key == key)?.Value ?? def;

        var savePath = Get("VideoServer.LocalSavePath", "").Trim();
        if (string.IsNullOrEmpty(savePath))
            savePath = FallbackSavePath;

        var publicBaseUrl = Get("VideoServer.PublicBaseUrl", "").TrimEnd('/');

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
            _logger.LogInformation("Local disk video upload OK: {FileName} → {FilePath}", fileName, filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local disk video upload failed: {FileName}", fileName);
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
            _logger.LogWarning(ex, "Local disk video delete failed: {FileName}", fileName);
            return false;
        }
    }

    public string GetPublicUrl(string fileName)
    {
        var publicBaseUrl = _db.CatalogSettings
            .Where(x => x.Key == "VideoServer.PublicBaseUrl")
            .Select(x => x.Value)
            .FirstOrDefault() ?? "";

        if (string.IsNullOrWhiteSpace(publicBaseUrl))
            return $"/api/catalog/videos/file/{fileName}";

        return publicBaseUrl.TrimEnd('/') + "/" + fileName;
    }
}
