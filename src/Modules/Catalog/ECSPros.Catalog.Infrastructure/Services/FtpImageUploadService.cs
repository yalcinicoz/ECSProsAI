using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;

namespace ECSPros.Catalog.Infrastructure.Services;

public class FtpImageUploadService : IImageUploadService
{
    private readonly CatalogDbContext _db;
    private readonly ILogger<FtpImageUploadService> _logger;

    public FtpImageUploadService(CatalogDbContext db, ILogger<FtpImageUploadService> logger)
    {
        _db = db;
        _logger = logger;
    }

    private async Task<ImageServerSettings> LoadSettingsAsync(CancellationToken ct = default)
    {
        var settings = await _db.CatalogSettings
            .Where(x => x.Key.StartsWith("ImageServer."))
            .ToListAsync(ct);

        string Get(string key, string def) =>
            settings.FirstOrDefault(x => x.Key == key)?.Value ?? def;

        return new ImageServerSettings(
            FtpHost: Get("ImageServer.FtpHost", "localhost"),
            FtpPort: int.TryParse(Get("ImageServer.FtpPort", "21"), out var p) ? p : 21,
            FtpUser: Get("ImageServer.FtpUser", "anonymous"),
            FtpPassword: Get("ImageServer.FtpPassword", ""),
            FtpBasePath: Get("ImageServer.FtpBasePath", "/images/products/"),
            PublicBaseUrl: Get("ImageServer.PublicBaseUrl", "http://localhost/images/products/")
        );
    }

    public async Task<bool> UploadAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var s = await LoadSettingsAsync(ct);
        var uri = new Uri($"ftp://{s.FtpHost}:{s.FtpPort}{s.FtpBasePath}{fileName}");

#pragma warning disable SYSLIB0014
        var request = (FtpWebRequest)WebRequest.Create(uri);
#pragma warning restore SYSLIB0014
        request.Method = WebRequestMethods.Ftp.UploadFile;
        request.Credentials = new NetworkCredential(s.FtpUser, s.FtpPassword);
        request.UseBinary = true;
        request.UsePassive = true;
        request.KeepAlive = false;

        try
        {
            using var requestStream = await request.GetRequestStreamAsync();
            await fileStream.CopyToAsync(requestStream, ct);
            using var response = (FtpWebResponse)await request.GetResponseAsync();
            _logger.LogInformation("FTP upload OK: {FileName}, Status: {Status}", fileName, response.StatusDescription);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FTP upload failed: {FileName}", fileName);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(string fileName, CancellationToken ct = default)
    {
        var s = await LoadSettingsAsync(ct);
        var uri = new Uri($"ftp://{s.FtpHost}:{s.FtpPort}{s.FtpBasePath}{fileName}");

#pragma warning disable SYSLIB0014
        var request = (FtpWebRequest)WebRequest.Create(uri);
#pragma warning restore SYSLIB0014
        request.Method = WebRequestMethods.Ftp.DeleteFile;
        request.Credentials = new NetworkCredential(s.FtpUser, s.FtpPassword);

        try
        {
            using var response = (FtpWebResponse)await request.GetResponseAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FTP delete failed: {FileName}", fileName);
            return false;
        }
    }

    public string GetPublicUrl(string fileName)
    {
        // Sync fallback — sync context'te çağrılıyorsa DB'den okuyamayız, cache yok
        // GetPublicUrl genellikle query response'larında kullanılır; burası nadiren çağrılır
        var baseUrl = _db.CatalogSettings
            .Where(x => x.Key == "ImageServer.PublicBaseUrl")
            .Select(x => x.Value)
            .FirstOrDefault() ?? "http://localhost/images/products/";

        return baseUrl.TrimEnd('/') + "/" + fileName;
    }

    private record ImageServerSettings(
        string FtpHost,
        int FtpPort,
        string FtpUser,
        string FtpPassword,
        string FtpBasePath,
        string PublicBaseUrl);
}
