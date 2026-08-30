using ECSPros.Api.Services.Store;
using ECSPros.Api.Services.Tracking.Feed;
using ECSPros.Api.Services.Storage;
using ECSPros.Core.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Controllers;

/// <summary>
/// İE-5 Faz E: ürün feed'i servis ucu — `GET /feeds/{platformCode}/google-shopping.xml?key=…` ve
/// `meta-catalog.csv`. Anahtar kanal google_merchant ayarındaki feedKey ile eşleşmeli (yoksa 404 —
/// var/yok bilgisi sızmaz). Dosya worker tarafından üretilir (istek anında DB sorgusu YOK);
/// X-Robots-Tag noindex. Merchant Center/Meta bot'u okuyacağı için BotDisiRotalar'a eklenmez.
/// </summary>
[ApiController]
[Route("feeds")]
[AllowAnonymous]
public class FeedController(
    ICoreDbContext coreDb,
    ITrackingSettingsProvider trackingSettings,
    IFeedStatusStore feedStatusStore,
    IFileStorage storage,
    IConfiguration config,
    IHostEnvironment env) : ControllerBase
{
    private static readonly Dictionary<string, string> Dosyalar = new(StringComparer.OrdinalIgnoreCase)
    {
        ["google-shopping.xml"] = "application/xml; charset=utf-8",
        ["meta-catalog.csv"] = "text/csv; charset=utf-8"
    };

    [HttpGet("{platformCode}/{file}")]
    public async Task<IActionResult> Get(string platformCode, string file, [FromQuery] string? key, CancellationToken ct)
    {
        if (!Dosyalar.TryGetValue(file, out var contentType) || string.IsNullOrWhiteSpace(key)) return NotFound();
        var platformId = await coreDb.FirmPlatforms.AsNoTracking()
            .Where(p => p.Code == platformCode && p.IsActive)
            .Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
        if (platformId is null) return NotFound();
        var s = await trackingSettings.GetAsync(platformId.Value, ct);
        var merchant = s.Servis("google_merchant");
        var feedKey = merchant?.Get("feedKey");
        if (merchant is null || string.IsNullOrWhiteSpace(feedKey) || !string.Equals(feedKey, key.Trim(), StringComparison.Ordinal)) return NotFound();

        if (string.Equals(config["Storage:Provider"], "S3", StringComparison.OrdinalIgnoreCase))
        {
            var status = await feedStatusStore.GetAsync(platformId.Value, ct);
            if (status?.LastRunAt is null) return NotFound();
            Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            Response.Headers["Cache-Control"] = "private, no-store";
            var signedUrl = await storage.GetPrivateReadUrlAsync(
                $"feeds/{platformCode}/{file}", TimeSpan.FromMinutes(15), ct);
            return Redirect(signedUrl);
        }

        var path = Path.Combine(FeedPaths.PlatformDir(config, env, platformCode), file);
        if (!System.IO.File.Exists(path)) return NotFound();
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        Response.Headers["Cache-Control"] = "public, max-age=900";
        return PhysicalFile(path, contentType, enableRangeProcessing: false);
    }
}
