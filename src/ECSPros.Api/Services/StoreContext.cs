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
    StoreKartAyarlari? KartAyarlari = null,
    // Liste sıralaması (2026-08-10): sitede gösterilecek sıralama seçenekleri —
    // ProductSortCatalog.Tumu'nun Settings."productList"."sortOptions" ile filtrelenmiş hali
    // ("default" her zaman kalır; eksik anahtar = AÇIK). null gelmez, kurucu doldurur.
    IReadOnlyList<(string Kod, string Ad)>? SiralamaSecenekleri = null)
{
    public IReadOnlyList<(string Kod, string Ad)> SiralamaListesi =>
        SiralamaSecenekleri ?? ECSPros.Shared.Contracts.ProductSortCatalog.Tumu;
}

/// <summary>
/// Ürün Kartı F2: bir değişken alanın ayarı — Acik + hangi kaynaklar (kampanya rozetleri /
/// kart mesajları) + rotasyon önceliği (MesajlarOnce). Alanlar: 1 = görsel altı bant,
/// 2 = ürün adı altı satır, 3 = puan altı satır.
/// </summary>
public sealed record KartAlanAyari(
    bool Acik = true,
    bool Kampanyalar = false,
    bool Mesajlar = true,
    bool MesajlarOnce = true,
    // Sosyal kanıt (2026-08-10, yalnız Alan 3'te anlamlı): "X kişinin sepetinde" /
    // "X kişinin favorisi" satırları — varsayılan KAPALI (mevcut kanallar etkilenmez).
    bool SepetSayisi = false,
    bool FavoriSayisi = false);

/// <summary>
/// Kanal bazlı ürün kartı görünüm ayarları (Settings."productCard"). Eksik anahtar = açık
/// (geri uyum: ayar hiç yazılmamış kanalda kart bugünkü haliyle kalır — kampanyalar Alan 1'de).
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
    KartAlanAyari? Alan1 = null,
    KartAlanAyari? Alan2 = null,
    KartAlanAyari? Alan3 = null)
{
    public static readonly StoreKartAyarlari Varsayilan = new();

    public KartAlanAyari Alan(int slot) => slot switch
    {
        2 => Alan2 ?? new KartAlanAyari(),
        3 => Alan3 ?? new KartAlanAyari(),
        _ => Alan1 ?? new KartAlanAyari(Kampanyalar: true),
    };

    /// <summary>Panel/Settings JSON'undan (camelCase) okur — Settings."productCard" ve önizleme
    /// query'si aynı biçim. "areas" yoksa F1 anahtarlarından (campaignBand/campaignBandSlot)
    /// geriye uyumlu türetilir.</summary>
    public static StoreKartAyarlari FromJson(System.Text.Json.JsonElement e)
    {
        bool B(string ad) => !e.TryGetProperty(ad, out var v)
            || v.ValueKind != System.Text.Json.JsonValueKind.False;

        KartAlanAyari? A1 = null, A2 = null, A3 = null;
        if (e.TryGetProperty("areas", out var areas)
            && areas.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            KartAlanAyari? Oku(string ad)
            {
                if (!areas.TryGetProperty(ad, out var a)
                    || a.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
                bool AB(string alan, bool vars) => a.TryGetProperty(alan, out var v)
                    ? v.ValueKind == System.Text.Json.JsonValueKind.True : vars;
                return new KartAlanAyari(
                    Acik: AB("enabled", true),
                    Kampanyalar: AB("campaigns", false),
                    Mesajlar: AB("messages", true),
                    MesajlarOnce: AB("messagesFirst", true),
                    SepetSayisi: AB("showCartCount", false),
                    FavoriSayisi: AB("showFavoriteCount", false));
            }
            A1 = Oku("1"); A2 = Oku("2"); A3 = Oku("3");
        }
        else
        {
            // F1 geriye uyumu: campaignBand + campaignBandSlot → kampanyalar o alanda
            var bandAcik = B("campaignBand");
            var slot = e.TryGetProperty("campaignBandSlot", out var s)
                       && s.ValueKind == System.Text.Json.JsonValueKind.Number
                       && s.TryGetInt32(out var si) && si is >= 1 and <= 3
                ? si : 1;
            A1 = new KartAlanAyari(Kampanyalar: bandAcik && slot == 1);
            A2 = new KartAlanAyari(Kampanyalar: bandAcik && slot == 2);
            A3 = new KartAlanAyari(Kampanyalar: bandAcik && slot == 3);
        }

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
            Alan1: A1, Alan2: A2, Alan3: A3);
    }

    /// <summary>Kart mesajı palet anahtarı → renk (alan 1 bant arka planı ve kampanya
    /// rotasyon JSON'u için hex; alan 2/3 metinleri ms-metin-{anahtar} sınıfını kullanır).</summary>
    public static string? PaletHex(string? anahtar) => anahtar switch
    {
        "yesil" => "#16a34a",
        "turuncu" => "#ea580c",
        "bordo" => "#9f1239",
        "pembe" => "#db2777",
        _ => null,
    };
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

            // Liste sıralama görünürlüğü (Settings."productList"."sortOptions": {kod: false} → gizle);
            // "default" kapatılamaz — sıralama seçilmediğinde düşülen seçenek her zaman listede.
            IReadOnlyList<(string Kod, string Ad)>? siralamalar = null;
            if (platform.Settings.TryGetValue("productList", out var plObj)
                && plObj is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Object } pl
                && pl.TryGetProperty("sortOptions", out var so)
                && so.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                siralamalar = ECSPros.Shared.Contracts.ProductSortCatalog.Tumu
                    .Where(s => s.Kod == "default"
                        || !so.TryGetProperty(s.Kod, out var v)
                        || v.ValueKind != System.Text.Json.JsonValueKind.False)
                    .ToList();
            }

            return new StorePlatformBilgisi(platform.Id, platform.Code, tema!, tokenlar, stokBitenGoster, stokBitenTarih, kartAyarlari, siralamalar);
        });
    }
}
