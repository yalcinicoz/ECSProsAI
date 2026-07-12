using ECSPros.Catalog.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Helpers;

public static class CdnHelper
{
    private const string FallbackBase = "https://cdn.misharitalia.com/img";
    private const string FallbackQuality = "85";
    private const string FallbackListHeight = "640";
    private const string FallbackThumbHeight = "240";
    private const string FallbackZoomHeight = "1200";

    public static async Task<string> BuildListUrlAsync(ICatalogDbContext db, CancellationToken ct = default)
        => await BuildAsync(db, "ImageServer.CdnListHeight", FallbackListHeight, ct);

    public static async Task<string> BuildThumbUrlAsync(ICatalogDbContext db, CancellationToken ct = default)
        => await BuildAsync(db, "ImageServer.CdnThumbHeight", FallbackThumbHeight, ct);

    public static async Task<string> BuildZoomUrlAsync(ICatalogDbContext db, CancellationToken ct = default)
        => await BuildAsync(db, "ImageServer.CdnZoomHeight", FallbackZoomHeight, ct);

    /// <summary>H5: FTP'yle yüklenen video dosyalarının servis tabanı ("VideoServer.CdnBaseUrl"
    /// ayarı) — sonu /'sız döner; ayar yoksa null (dosya kayıtları storefront'ta atlanır;
    /// URL tabanlı kayıtlar — K15 birincil yol — bundan bağımsız çalışır).</summary>
    public static async Task<string?> BuildVideoBaseAsync(ICatalogDbContext db, CancellationToken ct = default)
    {
        var deger = await db.CatalogSettings
            .Where(x => x.Key == "VideoServer.CdnBaseUrl")
            .Select(x => x.Value)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(deger) ? null : deger.Trim().TrimEnd('/');
    }

    private static async Task<string> BuildAsync(ICatalogDbContext db, string heightKey, string fallbackHeight, CancellationToken ct)
    {
        var keys = new[] { "ImageServer.CdnBaseUrl", "ImageServer.CdnQuality", heightKey };
        var settings = await db.CatalogSettings
            .Where(x => keys.Contains(x.Key))
            .Select(x => new { x.Key, x.Value })
            .ToListAsync(ct);

        string Get(string k, string def) => settings.FirstOrDefault(s => s.Key == k)?.Value?.Trim() is { Length: > 0 } v ? v : def;

        var baseUrl = Get("ImageServer.CdnBaseUrl", FallbackBase).TrimEnd('/');
        var quality = Get("ImageServer.CdnQuality", FallbackQuality);
        var height  = Get(heightKey, fallbackHeight);

        return $"{baseUrl}/{height}/{quality}/";
    }
}
