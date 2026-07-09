using ECSPros.Core.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Services;

/// <summary>
/// Razor storefront istekleri için aktif FirmPlatform çözümü (plan 3.5 / A11–A12).
/// Öncelik: Host header eşlemesi (Store:Hosts:{host} = platform kodu) →
/// Store:DefaultFirmPlatformCode. Tema bilgisi FirmPlatform.Settings JSONB'sinden okunur:
/// "theme" (tema kodu, yoksa varsayılan misharix) ve "themeTokens"
/// ({"--ms-renk-primary":"#..."} gibi CSS custom property override'ları).
/// Ayrı ThemeCode kolonu açılmadı — mevcut Settings alanı yeterli (bilinçli karar, migration yok).
/// </summary>
public interface IStoreContext
{
    Task<StorePlatformBilgisi?> GetPlatformAsync(CancellationToken ct = default);
}

public sealed record StorePlatformBilgisi(
    Guid Id,
    string Code,
    string Theme,
    IReadOnlyDictionary<string, string> ThemeTokens,
    // B12 (stok kararı): Settings."stockControlEnabled" — açıkken satılabilirlik gerçek
    // stoktan okunur; kapalıyken (varsayılan — bugünkü veri durumu) her şey satılabilir.
    // Stok verisi dolunca anahtar açılır, kod değişmez.
    bool StokKontrolu = false);

public sealed class StoreContext(
    ICoreDbContext coreDb,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    IMemoryCache cache) : IStoreContext
{
    private static readonly TimeSpan CacheSuresi = TimeSpan.FromMinutes(5);

    public async Task<StorePlatformBilgisi?> GetPlatformAsync(CancellationToken ct = default)
    {
        var host = httpContextAccessor.HttpContext?.Request.Host.Host?.ToLowerInvariant();
        var kod = (host is not null ? configuration[$"Store:Hosts:{host}"] : null)
                  ?? configuration["Store:DefaultFirmPlatformCode"];

        if (string.IsNullOrWhiteSpace(kod))
            return null;

        return await cache.GetOrCreateAsync($"store-platform:{kod}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheSuresi;

            var platform = await coreDb.FirmPlatforms
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Code == kod && p.IsActive, ct);

            if (platform is null)
                return null;

            var tema = platform.Settings.TryGetValue("theme", out var temaObj)
                ? temaObj?.ToString() ?? Extensions.StoreThemeViewLocationExpander.DefaultTheme
                : Extensions.StoreThemeViewLocationExpander.DefaultTheme;

            var tokenlar = new Dictionary<string, string>();
            if (platform.Settings.TryGetValue("themeTokens", out var tokenObj)
                && tokenObj is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Object } json)
            {
                foreach (var alan in json.EnumerateObject())
                {
                    var deger = alan.Value.ToString();
                    // CSS injection'a kapı açmamak için yalnızca --ms-* anahtarları ve güvenli değerler
                    if (alan.Name.StartsWith("--ms-", StringComparison.Ordinal)
                        && !deger.Contains(';') && !deger.Contains('}') && !deger.Contains('<'))
                    {
                        tokenlar[alan.Name] = deger;
                    }
                }
            }

            var stokKontrolu = platform.Settings.TryGetValue("stockControlEnabled", out var stokObj)
                && stokObj is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.True };

            return new StorePlatformBilgisi(platform.Id, platform.Code, tema!, tokenlar, stokKontrolu);
        });
    }
}
