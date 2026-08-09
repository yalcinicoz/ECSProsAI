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
    // 2026-07-14: stok artık HER ZAMAN dikkate alınır (eski stockControlEnabled emekli).
    // Kanal ayarı yalnız "stoğu biten ürünleri listede göster" kuralını yönetir:
    //   StokBitenGoster (Settings."showOutOfStock") — aç/kapa (müşteri kararı);
    //   StokBitenGosterTarih (Settings."outOfStockVisibleSince") — yalnız bu tarihten SONRA
    //   açılmış stok kartlarının (Product.CreatedAt) stoğu bitenleri gösterilir (null = kısıt yok).
    bool StokBitenGoster = false,
    DateTime? StokBitenGosterTarih = null,
    // Ürün Kartı F1 (2026-08-09): kart elementleri aç/kapat (Settings."productCard") —
    // panel Storefront → Ürün Kartı ekranından yönetilir; yoksa hepsi açık.
    StoreKartAyarlari? KartAyarlari = null);

/// <summary>
/// Kanal bazlı ürün kartı görünüm ayarları (Settings."productCard"). Eksik anahtar = açık
/// (geri uyum: ayar hiç yazılmamış kanalda kart bugünkü haliyle kalır). KampanyaBandiSlot:
/// 1 = görsel altı bant, 2 = ürün adı altı, 3 = puan satırı altı.
/// </summary>
public sealed record StoreKartAyarlari(
    bool VideoRozeti = true,
    bool SponsorRozeti = true,
    bool RenkRozeti = true,
    bool GaleriNoktalari = true,
    bool FavoriButonu = true,
    bool KoleksiyonButonu = true,
    bool Puan = true,
    bool IndirimSatiri = true,
    bool KampanyaFiyatSatiri = true,
    bool KampanyaBandi = true,
    int KampanyaBandiSlot = 1)
{
    public static readonly StoreKartAyarlari Varsayilan = new();

    /// <summary>Panel/Settings JSON'undan (camelCase anahtarlar) ayarları okur —
    /// hem Settings."productCard" hem önizleme query'si aynı biçimi kullanır.</summary>
    public static StoreKartAyarlari FromJson(System.Text.Json.JsonElement e)
    {
        bool B(string ad) => !e.TryGetProperty(ad, out var v)
            || v.ValueKind != System.Text.Json.JsonValueKind.False;
        var slot = e.TryGetProperty("campaignBandSlot", out var s)
                   && s.ValueKind == System.Text.Json.JsonValueKind.Number
                   && s.TryGetInt32(out var si) && si is >= 1 and <= 3
            ? si : 1;
        return new StoreKartAyarlari(
            VideoRozeti: B("videoBadge"),
            SponsorRozeti: B("sponsorBadge"),
            RenkRozeti: B("colorBadge"),
            GaleriNoktalari: B("galleryDots"),
            FavoriButonu: B("favoriteButton"),
            KoleksiyonButonu: B("collectionButton"),
            Puan: B("rating"),
            IndirimSatiri: B("discountRow"),
            KampanyaFiyatSatiri: B("campaignPriceRow"),
            KampanyaBandi: B("campaignBand"),
            KampanyaBandiSlot: slot);
    }
}

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

            var stokBitenGoster = platform.Settings.TryGetValue("showOutOfStock", out var sbgObj)
                && sbgObj is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.True };

            DateTime? stokBitenTarih = null;
            if (platform.Settings.TryGetValue("outOfStockVisibleSince", out var tObj)
                && tObj is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } te
                && DateTime.TryParse(te.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var td))
                stokBitenTarih = td;

            var kartAyarlari = platform.Settings.TryGetValue("productCard", out var pcObj)
                && pcObj is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Object } pc
                ? StoreKartAyarlari.FromJson(pc)
                : StoreKartAyarlari.Varsayilan;

            return new StorePlatformBilgisi(platform.Id, platform.Code, tema!, tokenlar, stokBitenGoster, stokBitenTarih, kartAyarlari);
        });
    }
}
