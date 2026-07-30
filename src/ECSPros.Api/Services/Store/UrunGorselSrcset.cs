using System.Text.RegularExpressions;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// Ürün görselleri için srcset üretici (PageSpeed varyant hattı — ürün tarafı, 2026-07-30).
/// Vitrin'den FARKLI: ürün görselleri diskte tutulmaz, cdn.misharitalia.com anında
/// yeniden boyutlandıran bir sunucudur — URL kalıbı "{taban}/img/{genislik}/{kalite}/{dosya}".
/// Dosya üretmeyiz; yalnız URL'deki genişlik segmentini değiştirerek srcset kurarız
/// (CDN her genişliği anında üretir, 2:3 oranı korunur). Kalıba uymayan URL'de null döner
/// (no-image.svg, dış/legacy URL vb. — davranış bugünkü tek kaynakla aynı kalır).
/// </summary>
public static partial class UrunGorselSrcset
{
    // .../img/640/85/dosya.webp → grup1=".../img/", grup2="640", grup3="/85/dosya.webp"
    [GeneratedRegex(@"^(https?://[^\s""]+/img/)(\d+)(/\d+/[^\s""]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex CdnKalibi();

    /// <summary>Görselin gösterileceği bağlama göre genişlik merdiveni.</summary>
    public static readonly int[] KartGenislikleri = [240, 360, 480, 640];      // liste/carousel kartı (ekranda ~180-240px)
    public static readonly int[] DetayGenislikleri = [480, 768, 1024, 1440];   // detay ana görseli (büyük + zoom)
    public static readonly int[] ThumbGenislikleri = [120, 240, 360];          // detay küçük görselleri

    /// <summary>Verilen CDN görsel URL'inden srcset dizesi ("url 240w, url 360w, ...");
    /// URL CDN kalıbına uymuyorsa null (çağıran srcset basmaz).</summary>
    public static string? Srcset(string? gorselUrl, int[] genislikler)
    {
        if (string.IsNullOrEmpty(gorselUrl)) return null;
        var m = CdnKalibi().Match(gorselUrl);
        if (!m.Success) return null;
        var taban = m.Groups[1].Value;   // ".../img/"
        var kuyruk = m.Groups[3].Value;  // "/85/dosya.webp"
        return string.Join(", ", genislikler.Select(w => $"{taban}{w}{kuyruk} {w}w"));
    }

    public static string? Kart(string? url) => Srcset(url, KartGenislikleri);
    public static string? Detay(string? url) => Srcset(url, DetayGenislikleri);
    public static string? Thumb(string? url) => Srcset(url, ThumbGenislikleri);
}
